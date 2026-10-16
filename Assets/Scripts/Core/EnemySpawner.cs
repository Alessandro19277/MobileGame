using System.Collections;
using UnityEngine;
using MobileGame.Utilities;
using MobileGame.Entities;
using MobileGame.UI;

namespace MobileGame.Core
{
    /// <summary>
    /// Spawna ondate progressive di nemici con pool separati (Microbi base, Virus, Batteri corazzati, Batteri di ghiaccio, Boss, Treasure Pod).
    /// Gestisce eventi improvvisi casuali (Swarm Wave con avviso in inglese e Treasure Pod).
    /// Composizione dinamica per wave, zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public static EnemySpawner Instance { get; private set; }

        [Header("Prefabs Nemici (Assegna da Inspector)")]
        [SerializeField] private MicrobeEnemy   _enemyPrefab;         // prefab base compatibile
        [SerializeField] private VirusEnemy     _virusPrefab;         // veloce, ondulatorio (da Wave 2)
        [SerializeField] private ArmoredBacteria _armoredPrefab;      // tank resistente (da Wave 3)
        [SerializeField] private IceBacteria    _iceBacteriaPrefab;   // sparatutto di ghiaccio (da Wave 6)
        [SerializeField] private BossEnemy      _bossPrefab;          // mega boss (ogni 5 Wave)
        [SerializeField] private TreasurePod    _treasurePodPrefab;   // capsula del tesoro biologica

        [Header("Pool Capacita'")]
        [SerializeField] private int _basicPoolSize    = 40;
        [SerializeField] private int _virusPoolSize    = 30;
        [SerializeField] private int _armoredPoolSize  = 10;
        [SerializeField] private int _icePoolSize      = 15;
        [SerializeField] private int _bossPoolSize     = 2;
        [SerializeField] private int _treasurePoolSize = 3;

        [Header("Spawning")]
        [SerializeField] private float _spawnRadius   = 5f;
        [SerializeField] private float _spawnInterval = 1.8f;
        [SerializeField] private int   _maxPerWave    = 8;

        // Pools GC-friendly pre-allocati in Awake
        private ObjectPool<MicrobeEnemy>    _basicPool;
        private ObjectPool<VirusEnemy>      _virusPool;
        private ObjectPool<ArmoredBacteria> _armoredPool;
        private ObjectPool<IceBacteria>     _icePool;
        private ObjectPool<BossEnemy>       _bossPool;
        private ObjectPool<TreasurePod>     _treasurePool;

        // Delegates pre-allocati in Awake per zero GC
        private System.Action<MicrobeEnemy> _returnBasicCallback;
        private System.Action<MicrobeEnemy> _returnVirusCallback;
        private System.Action<MicrobeEnemy> _returnArmoredCallback;
        private System.Action<MicrobeEnemy> _returnIceCallback;
        private System.Action<MicrobeEnemy> _returnBossCallback;
        private System.Action<TreasurePod>  _returnTreasureCallback;

        private Transform _playerTransform;
        private float     _spawnTimer;
        private float     _waveTimer;
        private int       _spawnedThisWave;
        private bool      _isRunning;

        // Gestione Eventi Improvvisi (Swarm, Treasure Pod e Cerchio Concentrico Ambush)
        private bool      _swarmTriggeredThisWave;
        private bool      _treasureTriggeredThisWave;
        private bool      _ambushTriggeredThisWave;
        private bool      _majorEventTriggeredThisWave;
        private float     _eventPauseTimer;
        private float     _swarmCheckTimer;
        private float     _treasureCheckTimer;
        private float     _ambushCheckTimer;
        private Coroutine _swarmCoroutine;
        private float     _runningAngle;

        private const float WAVE_DURATION = 30f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _returnBasicCallback    = ReturnBasicEnemy;
            _returnVirusCallback    = ReturnVirusEnemy;
            _returnArmoredCallback  = ReturnArmoredEnemy;
            _returnIceCallback      = ReturnIceEnemy;
            _returnBossCallback     = ReturnBossEnemy;
            _returnTreasureCallback = ReturnTreasurePod;

            // Capacità minima maggiorata per supportare sciami e accerchiamento a 360 gradi
            _basicPoolSize   = Mathf.Max(_basicPoolSize, 50);
            _virusPoolSize   = Mathf.Max(_virusPoolSize, 40);
            _armoredPoolSize = Mathf.Max(_armoredPoolSize, 20);
            _icePoolSize     = Mathf.Max(_icePoolSize, 20);

            Transform poolsRoot = new GameObject("EnemyPoolsRoot").transform;
            poolsRoot.SetParent(transform);

            // Pool Nemico Base
            if (_enemyPrefab != null)
            {
                var basicContainer = new GameObject("BasicPool").transform;
                basicContainer.SetParent(poolsRoot);
                _basicPool = new ObjectPool<MicrobeEnemy>(
                    factory:         () => Instantiate(_enemyPrefab, basicContainer),
                    onGet:           null,
                    onRelease:       e  => { e.gameObject.SetActive(false); e.transform.SetParent(basicContainer); },
                    initialCapacity: _basicPoolSize
                );
            }

            // Pool Virus
            if (_virusPrefab != null)
            {
                var virusContainer = new GameObject("VirusPool").transform;
                virusContainer.SetParent(poolsRoot);
                _virusPool = new ObjectPool<VirusEnemy>(
                    factory:         () => Instantiate(_virusPrefab, virusContainer),
                    onGet:           null,
                    onRelease:       e  => { e.gameObject.SetActive(false); e.transform.SetParent(virusContainer); },
                    initialCapacity: _virusPoolSize
                );
            }

            // Pool Batterio Corazzato
            if (_armoredPrefab != null)
            {
                var armoredContainer = new GameObject("ArmoredPool").transform;
                armoredContainer.SetParent(poolsRoot);
                _armoredPool = new ObjectPool<ArmoredBacteria>(
                    factory:         () => Instantiate(_armoredPrefab, armoredContainer),
                    onGet:           null,
                    onRelease:       e  => { e.gameObject.SetActive(false); e.transform.SetParent(armoredContainer); },
                    initialCapacity: _armoredPoolSize
                );
            }

            // Pool Batterio di Ghiaccio (Wave 6)
            if (_iceBacteriaPrefab == null)
            {
                var iceTemplateGO = new GameObject("IceBacteriaTemplate");
                iceTemplateGO.SetActive(false);
                iceTemplateGO.transform.SetParent(poolsRoot);
                iceTemplateGO.AddComponent<SpriteRenderer>();
                iceTemplateGO.AddComponent<Rigidbody2D>();
                iceTemplateGO.AddComponent<CircleCollider2D>();
                iceTemplateGO.tag = "Enemy";
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer >= 0) iceTemplateGO.layer = enemyLayer;
                _iceBacteriaPrefab = iceTemplateGO.AddComponent<IceBacteria>();
            }

            var iceContainer = new GameObject("IcePool").transform;
            iceContainer.SetParent(poolsRoot);
            _icePool = new ObjectPool<IceBacteria>(
                factory:         () => Instantiate(_iceBacteriaPrefab, iceContainer),
                onGet:           null,
                onRelease:       e  => { e.gameObject.SetActive(false); e.transform.SetParent(iceContainer); },
                initialCapacity: _icePoolSize
            );

            // Pool Boss
            if (_bossPrefab != null)
            {
                var bossContainer = new GameObject("BossPool").transform;
                bossContainer.SetParent(poolsRoot);
                _bossPool = new ObjectPool<BossEnemy>(
                    factory:         () => Instantiate(_bossPrefab, bossContainer),
                    onGet:           null,
                    onRelease:       e  => { e.gameObject.SetActive(false); e.transform.SetParent(bossContainer); },
                    initialCapacity: _bossPoolSize
                );
            }

            // Pool Capsula del Tesoro (Treasure Pod)
            if (_treasurePodPrefab == null)
            {
                var treasureTemplateGO = new GameObject("TreasurePodTemplate");
                treasureTemplateGO.SetActive(false);
                treasureTemplateGO.transform.SetParent(poolsRoot);
                treasureTemplateGO.AddComponent<SpriteRenderer>();
                treasureTemplateGO.AddComponent<Rigidbody2D>();
                treasureTemplateGO.AddComponent<CircleCollider2D>();
                treasureTemplateGO.AddComponent<HealthComponent>();
                _treasurePodPrefab = treasureTemplateGO.AddComponent<TreasurePod>();
            }

            var treasureContainer = new GameObject("TreasurePool").transform;
            treasureContainer.SetParent(poolsRoot);
            _treasurePool = new ObjectPool<TreasurePod>(
                factory:         () => Instantiate(_treasurePodPrefab, treasureContainer),
                onGet:           null,
                onRelease:       t  => { t.gameObject.SetActive(false); t.transform.SetParent(treasureContainer); },
                initialCapacity: _treasurePoolSize
            );
        }

        private void Start()
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += HandleGameStarted;
                if (GameManager.Instance.CurrentState == GameState.Playing && _playerTransform != null)
                {
                    StartSpawning(_playerTransform);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= HandleGameStarted;
            }
        }

        private void HandleGameStarted()
        {
            if (_playerTransform == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null) _playerTransform = playerGO.transform;
            }

            if (_playerTransform != null)
                StartSpawning(_playerTransform);
        }

        // ── Spawning API ───────────────────────────────────────────────────────

        public void StartSpawning(Transform player)
        {
            _playerTransform = player;
            _spawnTimer      = 0f;
            _waveTimer       = 0f;
            _spawnedThisWave = 0;
            _isRunning       = true;

            int currentWave = GameManager.Instance?.Wave ?? 1;
            CalculateWaveParameters(currentWave);
            ResetWaveEvents();
        }

        private void CalculateWaveParameters(int currentWave)
        {
            // Tetto massimo nemici contemporanei a schermo (sensazione orda viva e dinamica)
            // Wave 1: max 8 | Wave 2: max 12 | Wave 3: max 16 | Wave 4: max 22 | Wave 5+: fino a max 30
            if (currentWave == 1)      _maxPerWave = 8;
            else if (currentWave == 2) _maxPerWave = 12;
            else if (currentWave == 3) _maxPerWave = 16;
            else if (currentWave == 4) _maxPerWave = 22;
            else                       _maxPerWave = Mathf.Min(30, 22 + (currentWave - 4) * 2);

            // Frequenza di spawn sostenuta per alimentare l'azione senza svuotare il campo
            if (currentWave == 1)      _spawnInterval = 2.0f;
            else if (currentWave == 2) _spawnInterval = 1.6f;
            else if (currentWave == 3) _spawnInterval = 1.3f;
            else if (currentWave == 4) _spawnInterval = 1.1f;
            else                       _spawnInterval = Mathf.Max(0.85f, 1.1f - (currentWave - 4) * 0.06f);
        }

        private void ResetWaveEvents()
        {
            _swarmTriggeredThisWave      = false;
            _treasureTriggeredThisWave   = false;
            _ambushTriggeredThisWave     = false;
            _majorEventTriggeredThisWave = false;
            _eventPauseTimer             = 0f;

            // Tempi casuali all'interno della wave
            _swarmCheckTimer    = Random.Range(8f, 13f);
            _treasureCheckTimer = Random.Range(12f, 20f);
            _ambushCheckTimer   = Random.Range(16f, 22f);
        }

        private void Update()
        {
            if (!_isRunning || _playerTransform == null) return;
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            _spawnTimer += Time.deltaTime;
            _waveTimer  += Time.deltaTime;

            if (_eventPauseTimer > 0f)
            {
                _eventPauseTimer -= Time.deltaTime;
            }

            int currentWave = GameManager.Instance.Wave;

            // Spawning cadenzato a grappoli a 360 gradi attorno al giocatore
            // SI METTE IN PAUSA se un evento (Swarm o Ambush) è attivo o ha lasciato una pausa di respiro
            if (_spawnedThisWave >= _maxPerWave)
            {
                // Mantiene il timer a respiro: quando un nemico muore,
                // il giocatore ha tempo (almeno 1-1.5 sec) prima che arrivino nuovi rinforzi
                _spawnTimer = Mathf.Min(_spawnTimer, _spawnInterval * 0.4f);
            }
            else if (_eventPauseTimer <= 0f && _spawnTimer >= _spawnInterval)
            {
                _spawnTimer = 0f;
                SpawnCluster360(currentWave);
            }

            // ── Verifica Eventi Improvvisi Durante la Wave ──────────────────────

            // 1. Evento SWARM (da Wave 3 in poi, per consentire al player di creare la build nelle prime 2 wave):
            // Solo se non è già avvenuto un grande evento in questa ondata
            if (!_majorEventTriggeredThisWave && !_swarmTriggeredThisWave && _waveTimer >= _swarmCheckTimer)
            {
                _swarmTriggeredThisWave = true;
                if (currentWave >= 3)
                {
                    float swarmChance = Mathf.Clamp(0.12f + (currentWave - 2) * 0.06f, 0.15f, 0.55f);
                    if (Random.value < swarmChance)
                    {
                        _majorEventTriggeredThisWave = true;
                        TriggerSwarmEvent(currentWave);
                    }
                }
            }

            // 2. Evento TREASURE POD:
            // Disponibile SOLO a partire dalla WAVE 4 in poi (evento pacifico di ricompensa)
            if (!_treasureTriggeredThisWave && _waveTimer >= _treasureCheckTimer)
            {
                _treasureTriggeredThisWave = true;
                if (currentWave >= 4 && Random.value < 0.38f)
                {
                    TriggerTreasurePodEvent();
                }
            }

            // 3. Evento AMBUSH RING (Cerchio concentrico a 360 gradi):
            // Dalla Wave 3 in poi, e SOLO se non è già avvenuto lo Swarm
            if (!_majorEventTriggeredThisWave && !_ambushTriggeredThisWave && _waveTimer >= _ambushCheckTimer)
            {
                _ambushTriggeredThisWave = true;
                if (currentWave >= 3)
                {
                    float ambushChance = Mathf.Clamp(0.25f + (currentWave - 2) * 0.08f, 0.25f, 0.60f);
                    if (Random.value < ambushChance)
                    {
                        _majorEventTriggeredThisWave = true;
                        TriggerAmbushRingEvent(currentWave);
                    }
                }
            }

            // ── Fine e avanzamento Wave ─────────────────────────────────────────

            if (_waveTimer >= WAVE_DURATION)
            {
                _waveTimer = 0f;
                GameManager.Instance.AdvanceWave();

                int newWave = GameManager.Instance.Wave;
                CalculateWaveParameters(newWave);
                ResetWaveEvents();

                // All'inizio della wave 5, 10, ecc. spawna il boss
                if (newWave % 5 == 0 && _bossPool != null)
                {
                    SpawnBoss(newWave);
                }
            }
        }

        // ── Evento Improvviso: Swarm (Sciame) ──────────────────────────────────

        private void TriggerSwarmEvent(int wave)
        {
            // Avviso visivo a tutto schermo: solo titolo di allerta chiaro, senza sottotitolo lungo
            HUDController.Instance?.ShowEventAnnouncement(
                "WARNING: SWARM INCOMING!",
                string.Empty,
                new Color(1.0f, 0.15f, 0.15f), // Rosso vivo allarme
                displayDuration: 3.0f,
                flashScreen: true
            );

            // Vibrazione tattile di avvertimento
            HapticManager.MediumImpact();

            if (_swarmCoroutine != null) StopCoroutine(_swarmCoroutine);
            _swarmCoroutine = StartCoroutine(SpawnSwarmRoutine(wave));
        }

        private IEnumerator SpawnSwarmRoutine(int wave)
        {
            // Breve preavviso iniziale durante il lampeggio dello schermo
            yield return new WaitForSeconds(0.5f);

            // Conteggio calibrato: tra 8 e 18 nemici (distribuiti bene, senza soffocare il player)
            int swarmCount = Mathf.Clamp(6 + wave * 2, 8, 18);

            // Durata totale dello sciame: distribuito gradualmente tra 5.5 e 7.5 secondi
            float totalSwarmDuration = Random.Range(5.5f, 7.5f);
            float delayBetweenSpawns = totalSwarmDuration / swarmCount;

            // Mette in pausa lo spawn a grappoli per tutta la durata dello sciame + 2.5s di respiro
            _eventPauseTimer = totalSwarmDuration + 2.5f;

            for (int i = 0; i < swarmCount; i++)
            {
                if (GameManager.Instance?.CurrentState != GameState.Playing) yield break;

                // Se ci sono già troppi nemici vivi a schermo, rallenta lo sciame per non accumulare
                if (_spawnedThisWave >= _maxPerWave + 4)
                {
                    yield return new WaitForSeconds(0.6f);
                }

                // Spawna in punti casuali distribuiti in tutta la mappa
                Vector2 spawnPos = GetRandomMapSpawnPosition();

                // Nello sciame prioritizziamo i virus
                if (_virusPool != null && Random.value < 0.60f)
                {
                    PrepareAndActivateEnemy(_virusPool.Get(), spawnPos, wave, _returnVirusCallback);
                }
                else if (_basicPool != null)
                {
                    PrepareAndActivateEnemy(_basicPool.Get(), spawnPos, wave, _returnBasicCallback);
                }

                _spawnedThisWave++;

                // Intervallo scaglionato progressivo
                yield return new WaitForSeconds(delayBetweenSpawns);
            }

            _swarmCoroutine = null;
        }

        // ── Evento Improvviso: Treasure Pod (Capsula del Tesoro - da Wave 4+) ────

        private void TriggerTreasurePodEvent()
        {
            if (_treasurePool == null || _playerTransform == null) return;

            // Avviso visivo: solo titolo di allerta compatto in lingua inglese
            HUDController.Instance?.ShowEventAnnouncement(
                "ALERT: TREASURE FOUND!",
                string.Empty,
                new Color(1.0f, 0.85f, 0.2f), // Oro brillante
                displayDuration: 2.5f,
                flashScreen: false
            );

            HapticManager.LightImpact();

            // Spawna a distanza casuale nell'arena dal giocatore
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(2.8f, 4.5f);
            Vector2 spawnPos = (Vector2)_playerTransform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            if (ArenaBoundary.Instance != null)
            {
                spawnPos = ArenaBoundary.Instance.ClampPosition(spawnPos, 1.2f);
            }

            TreasurePod pod = _treasurePool.Get();
            if (pod != null)
            {
                pod.transform.position = spawnPos;
                if (pod.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.position = spawnPos;
                    rb.linearVelocity = Vector2.zero;
                }
                pod.gameObject.SetActive(true);
                pod.Initialize(spawnPos, _returnTreasureCallback);
            }
        }

        // ── Generatore di Posizioni Casuali su Tutta la Mappa ─────────────────

        /// <summary>
        /// Genera una posizione casuale distribuita su tutta la mappa/arena (zero alloc).
        /// Garantisce che il nemico appaia liberamente in qualsiasi quadrante dell'arena,
        /// ma oltre il perimetro visibile della telecamera del giocatore.
        /// </summary>
        private Vector2 GetRandomMapSpawnPosition()
        {
            Vector2 arenaCenter = ArenaBoundary.Instance != null ? ArenaBoundary.Instance.Center : Vector2.zero;
            float arenaRadius   = ArenaBoundary.Instance != null ? ArenaBoundary.Instance.Radius * 0.88f : 16f;

            float camHalfHeight = 5.2f;
            float camHalfWidth  = 3.2f;
            if (Camera.main != null)
            {
                camHalfHeight = Camera.main.orthographicSize;
                camHalfWidth  = camHalfHeight * Camera.main.aspect;
            }

            // Distanza di sicurezza minima: diagonale della telecamera + margine (nessun nemico spawna visibile a schermo)
            float minSafeDist = Mathf.Sqrt(camHalfWidth * camHalfWidth + camHalfHeight * camHalfHeight) + 1.1f;
            float minSafeDistSq = minSafeDist * minSafeDist;

            // Tentativi per trovare un punto casuale nell'arena fuori dalla telecamera
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 candidate = arenaCenter + Random.insideUnitCircle * arenaRadius;
                Vector2 toPlayer = candidate - (Vector2)_playerTransform.position;
                if (toPlayer.sqrMagnitude >= minSafeDistSq)
                {
                    if (ArenaBoundary.Instance != null)
                    {
                        candidate = ArenaBoundary.Instance.ClampPosition(candidate, 0.85f);
                    }
                    return candidate;
                }
            }

            // Fallback distribuito a 360° lungo l'orizzonte sicuro
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 fallback = (Vector2)_playerTransform.position + dir * (minSafeDist + Random.Range(0.8f, 2.5f));
            if (ArenaBoundary.Instance != null)
            {
                fallback = ArenaBoundary.Instance.ClampPosition(fallback, 0.85f);
            }
            return fallback;
        }

        // ── Logica di Generazione Ondate (Distribuzione Casuale Mappa) ──────────

        private void SpawnCluster360(int wave)
        {
            if (_playerTransform == null) return;

            // Dimensione del gruppo per ondata
            int clusterSize;
            if (wave == 1)      clusterSize = 1;
            else if (wave == 2) clusterSize = 2;
            else if (wave == 3) clusterSize = 3;
            else if (wave <= 5) clusterSize = 4;
            else                clusterSize = 5;

            for (int i = 0; i < clusterSize; i++)
            {
                if (_spawnedThisWave >= _maxPerWave) break;

                // Spawna in tutta la mappa in punti casuali, evitando di seguire/incollarsi dietro al player
                Vector2 spawnPos = GetRandomMapSpawnPosition();
                SpawnSingleEnemyAt(spawnPos, wave);
            }
        }

        private void PrepareAndActivateEnemy(MicrobeEnemy enemy, Vector2 spawnPos, int wave, System.Action<MicrobeEnemy> callback)
        {
            if (enemy == null) return;
            enemy.transform.position = spawnPos;
            if (enemy.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.position = spawnPos;
                rb.linearVelocity = Vector2.zero;
            }
            enemy.gameObject.SetActive(true);
            enemy.Initialize(_playerTransform, wave, callback);
        }

        private void SpawnSingleEnemyAt(Vector2 spawnPos, int wave)
        {
            float roll = Random.value;

            // Selezione probabilistica in base alla Wave
            if (_icePool != null && wave >= 6 && roll < 0.28f)
            {
                PrepareAndActivateEnemy(_icePool.Get(), spawnPos, wave, _returnIceCallback);
            }
            else if (_armoredPool != null && wave >= 3 && roll < (wave >= 6 ? 0.52f : 0.20f))
            {
                PrepareAndActivateEnemy(_armoredPool.Get(), spawnPos, wave, _returnArmoredCallback);
            }
            else if (_virusPool != null && wave >= 2 && roll < (wave >= 6 ? 0.75f : (wave >= 3 ? 0.45f : 0.22f)))
            {
                PrepareAndActivateEnemy(_virusPool.Get(), spawnPos, wave, _returnVirusCallback);
            }
            else if (_basicPool != null)
            {
                PrepareAndActivateEnemy(_basicPool.Get(), spawnPos, wave, _returnBasicCallback);
            }

            _spawnedThisWave++;
        }

        // ── Evento Improvviso: Ambush Ring (Cerchio Concentrico a 360 Gradi) ────

        private void TriggerAmbushRingEvent(int wave)
        {
            if (_playerTransform == null) return;

            // Allerta HUD con vibrazione
            HUDController.Instance?.ShowEventAnnouncement(
                "WARNING: AMBUSH RING!",
                "SURROUNDED - BREAK THROUGH!",
                new Color(1.0f, 0.42f, 0.08f), // Arancio allerta
                displayDuration: 2.8f,
                flashScreen: false
            );

            HapticManager.MediumImpact();

            // Mette in pausa lo spawn a grappoli per 3.5 secondi per dare spazio alla manovra del giocatore
            _eventPauseTimer = 3.5f;

            // Anello contenuto: 6 nemici a Wave 3, fino a max 10 a Wave alte
            int ringCount   = Mathf.Clamp(4 + wave, 6, 10);
            float baseAngle = Random.Range(0f, Mathf.PI * 2f);
            float angleStep = (Mathf.PI * 2f) / ringCount;

            float camHalfHeight = 5.2f;
            float camHalfWidth  = 3.2f;
            if (Camera.main != null)
            {
                camHalfHeight = Camera.main.orthographicSize;
                camHalfWidth  = camHalfHeight * Camera.main.aspect;
            }

            float ringX = camHalfWidth + 1.5f;
            float ringY = camHalfHeight + 1.5f;

            for (int i = 0; i < ringCount; i++)
            {
                float angle = baseAngle + i * angleStep;
                Vector2 offset = new Vector2(
                    Mathf.Cos(angle) * ringX,
                    Mathf.Sin(angle) * ringY
                );
                Vector2 spawnPos = (Vector2)_playerTransform.position + offset;

                if (ArenaBoundary.Instance != null)
                {
                    spawnPos = ArenaBoundary.Instance.ClampPosition(spawnPos, 0.85f);

                    Vector2 toPlayer = spawnPos - (Vector2)_playerTransform.position;
                    if (toPlayer.sqrMagnitude < (camHalfWidth + 0.6f) * (camHalfWidth + 0.6f))
                    {
                        Vector2 toCenter = (ArenaBoundary.Instance.Center - (Vector2)_playerTransform.position).normalized;
                        spawnPos = (Vector2)_playerTransform.position + toCenter * ringX;
                        spawnPos = ArenaBoundary.Instance.ClampPosition(spawnPos, 0.85f);
                    }
                }

                SpawnSingleEnemyAt(spawnPos, wave);
            }
        }

        private void SpawnBoss(int wave)
        {
            if (_bossPool == null || _playerTransform == null) return;
            Vector2 spawnPos = GetRandomSpawnPosition();
            BossEnemy boss = _bossPool.Get();
            if (boss != null)
            {
                boss.transform.position = spawnPos;
                if (boss.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.position = spawnPos;
                    rb.linearVelocity = Vector2.zero;
                }
                boss.gameObject.SetActive(true);
                boss.Initialize(_playerTransform, wave, _returnBossCallback);
            }

            int tier = Mathf.Max(1, wave / 5);

            if (tier >= 4) // Wave 20+
            {
                HUDController.Instance?.ShowEventAnnouncement(
                    "APOCALYPSE WARNING: OMEGA PLAGUE CORE",
                    "EXTINCTION LEVEL PATHOGEN DETECTED!",
                    new Color(1.0f, 0.05f, 0.25f),
                    displayDuration: 4.5f,
                    flashScreen: true
                );
                SpawnEscortMinions(spawnPos, wave, virusCount: 3, armoredCount: 2);
            }
            else if (tier == 3) // Wave 15
            {
                HUDController.Instance?.ShowEventAnnouncement(
                    "CRITICAL HAZARD: ABYSSAL BIO-TITAN",
                    "HYPER-MUTATED APEX THREAT INCOMING!",
                    new Color(0.45f, 0.10f, 0.95f),
                    displayDuration: 4.2f,
                    flashScreen: true
                );
                SpawnEscortMinions(spawnPos, wave, virusCount: 2, armoredCount: 1);
            }
            else if (tier == 2) // Wave 10
            {
                HUDController.Instance?.ShowEventAnnouncement(
                    "CRITICAL ALERT: APEX BIO-TITAN",
                    "MUTATED PATHOGEN PRIME DETECTED!",
                    new Color(0.92f, 0.10f, 0.95f),
                    displayDuration: 4.0f,
                    flashScreen: true
                );
                SpawnEscortMinions(spawnPos, wave, virusCount: 2, armoredCount: 0);
            }
            else // Wave 5
            {
                HUDController.Instance?.ShowEventAnnouncement(
                    "WARNING: MEGA PATHOGEN DETECTED",
                    "COLOSSAL VIRUS INCOMING!",
                    new Color(1.0f, 0.20f, 0.35f),
                    displayDuration: 3.2f,
                    flashScreen: false
                );
            }
        }

        private void SpawnEscortMinions(Vector2 centerPos, int wave, int virusCount, int armoredCount)
        {
            if (_virusPool != null)
            {
                for (int i = 0; i < virusCount; i++)
                {
                    Vector2 minionPos = centerPos + (Vector2)(Random.insideUnitCircle * 1.6f);
                    VirusEnemy minion = _virusPool.Get();
                    minion.transform.position = minionPos;
                    minion.Initialize(_playerTransform, wave, _returnVirusCallback);
                    _spawnedThisWave++;
                }
            }

            if (_armoredPool != null)
            {
                for (int i = 0; i < armoredCount; i++)
                {
                    Vector2 minionPos = centerPos + (Vector2)(Random.insideUnitCircle * 1.8f);
                    ArmoredBacteria minion = _armoredPool.Get();
                    minion.transform.position = minionPos;
                    minion.Initialize(_playerTransform, wave, _returnArmoredCallback);
                    _spawnedThisWave++;
                }
            }
        }

        private Vector2 GetRandomSpawnPosition()
        {
            float angle = Random.Range(0f, 2f * Mathf.PI);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _spawnRadius;
            Vector2 pos = (Vector2)_playerTransform.position + offset;

            if (ArenaBoundary.Instance != null)
            {
                pos = ArenaBoundary.Instance.ClampPosition(pos, 1.2f);
            }

            return pos;
        }

        // ── Callback Ritorno Pool ──────────────────────────────────────────────

        private void ReturnBasicEnemy(MicrobeEnemy enemy)
        {
            _spawnedThisWave = Mathf.Max(0, _spawnedThisWave - 1);
            _spawnTimer = Mathf.Min(_spawnTimer, _spawnInterval * 0.4f);
            _basicPool?.Release(enemy);
        }

        private void ReturnVirusEnemy(MicrobeEnemy enemy)
        {
            _spawnedThisWave = Mathf.Max(0, _spawnedThisWave - 1);
            _spawnTimer = Mathf.Min(_spawnTimer, _spawnInterval * 0.4f);
            if (enemy is VirusEnemy v) _virusPool?.Release(v);
        }

        private void ReturnArmoredEnemy(MicrobeEnemy enemy)
        {
            _spawnedThisWave = Mathf.Max(0, _spawnedThisWave - 1);
            _spawnTimer = Mathf.Min(_spawnTimer, _spawnInterval * 0.4f);
            if (enemy is ArmoredBacteria a) _armoredPool?.Release(a);
        }

        private void ReturnIceEnemy(MicrobeEnemy enemy)
        {
            _spawnedThisWave = Mathf.Max(0, _spawnedThisWave - 1);
            _spawnTimer = Mathf.Min(_spawnTimer, _spawnInterval * 0.4f);
            if (enemy is IceBacteria i) _icePool?.Release(i);
        }

        private void ReturnBossEnemy(MicrobeEnemy enemy)
        {
            if (enemy is BossEnemy b) _bossPool?.Release(b);
        }

        private void ReturnTreasurePod(TreasurePod pod)
        {
            _treasurePool?.Release(pod);
        }
    }
}
