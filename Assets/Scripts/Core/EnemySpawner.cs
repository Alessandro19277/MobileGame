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

        // Gestione Eventi Improvvisi (Swarm e Treasure Pod)
        private bool      _swarmTriggeredThisWave;
        private bool      _treasureTriggeredThisWave;
        private float     _swarmCheckTimer;
        private float     _treasureCheckTimer;
        private Coroutine _swarmCoroutine;

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

            Transform poolsRoot = new GameObject("EnemyPoolsRoot").transform;
            poolsRoot.SetParent(transform);

            // Pool Nemico Base
            if (_enemyPrefab != null)
            {
                var basicContainer = new GameObject("BasicPool").transform;
                basicContainer.SetParent(poolsRoot);
                _basicPool = new ObjectPool<MicrobeEnemy>(
                    factory:         () => Instantiate(_enemyPrefab, basicContainer),
                    onGet:           e  => e.gameObject.SetActive(true),
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
                    onGet:           e  => e.gameObject.SetActive(true),
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
                    onGet:           e  => e.gameObject.SetActive(true),
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
                iceTemplateGO.AddComponent<HealthComponent>();
                _iceBacteriaPrefab = iceTemplateGO.AddComponent<IceBacteria>();
            }

            var iceContainer = new GameObject("IcePool").transform;
            iceContainer.SetParent(poolsRoot);
            _icePool = new ObjectPool<IceBacteria>(
                factory:         () => Instantiate(_iceBacteriaPrefab, iceContainer),
                onGet:           e  => e.gameObject.SetActive(true),
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
                    onGet:           e  => e.gameObject.SetActive(true),
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
                onGet:           t  => t.gameObject.SetActive(true),
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

            ResetWaveEvents();
        }

        private void ResetWaveEvents()
        {
            _swarmTriggeredThisWave    = false;
            _treasureTriggeredThisWave = false;
            // Tempi casuali all'interno della wave (fra il secondo 6 e il secondo 24)
            _swarmCheckTimer    = Random.Range(7f, 16f);
            _treasureCheckTimer = Random.Range(10f, 22f);
        }

        private void Update()
        {
            if (!_isRunning || _playerTransform == null) return;
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            _spawnTimer += Time.deltaTime;
            _waveTimer  += Time.deltaTime;

            // Spawning cadenzato standard
            if (_spawnTimer >= _spawnInterval && _spawnedThisWave < _maxPerWave)
            {
                _spawnTimer = 0f;
                SpawnNextEnemy();
            }

            // ── Verifica Eventi Improvvisi Durante la Wave ──────────────────────

            int currentWave = GameManager.Instance.Wave;

            // 1. Evento SWARM:
            // Raro nelle prime ondate (Wave 2: 15%, Wave 3: 22%), poi probabilità che cresce dinamicamente (fino a 65% nelle wave alte)
            if (!_swarmTriggeredThisWave && _waveTimer >= _swarmCheckTimer)
            {
                _swarmTriggeredThisWave = true;
                if (currentWave >= 2)
                {
                    // Calcolo probabilità progressiva: Wave 2 = 16%, Wave 4 = 28%, Wave 6 = 40%, Wave 8+ = 55-65%
                    float swarmChance = Mathf.Clamp(0.10f + (currentWave - 1) * 0.06f, 0.15f, 0.65f);
                    if (Random.value < swarmChance)
                    {
                        TriggerSwarmEvent(currentWave);
                    }
                }
            }

            // 2. Evento TREASURE POD:
            // Disponibile SOLO a partire dalla WAVE 4 in poi
            if (!_treasureTriggeredThisWave && _waveTimer >= _treasureCheckTimer)
            {
                _treasureTriggeredThisWave = true;
                if (currentWave >= 4 && Random.value < 0.38f)
                {
                    TriggerTreasurePodEvent();
                }
            }

            // ── Fine e avanzamento Wave ─────────────────────────────────────────

            if (_waveTimer >= WAVE_DURATION)
            {
                _waveTimer       = 0f;
                _spawnedThisWave = 0;
                _maxPerWave     += 3;
                _spawnInterval   = Mathf.Max(0.25f, _spawnInterval - 0.12f);
                GameManager.Instance.AdvanceWave();

                ResetWaveEvents();

                // All'inizio della wave 5, 10, ecc. spawna il boss
                int newWave = GameManager.Instance.Wave;
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

            // Numero di nemici per lo sciame (tra 18 e 32)
            int swarmCount = Mathf.Clamp(16 + wave * 3, 18, 34);

            // Durata totale dello sciame: distribuito gradualmente tra 6 e 9 secondi (es. ~7.5s)
            float totalSwarmDuration = Random.Range(6.5f, 8.5f);
            float delayBetweenSpawns = totalSwarmDuration / swarmCount;

            for (int i = 0; i < swarmCount; i++)
            {
                if (GameManager.Instance?.CurrentState != GameState.Playing) yield break;

                // Spawna in punti completamente casuali della mappa attorno all'arena e al giocatore
                Vector2 spawnPos;
                if (Random.value < 0.65f)
                {
                    // 65% spawn dal perimetro a 360 gradi
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float dist  = Random.Range(_spawnRadius * 0.85f, _spawnRadius * 1.4f);
                    spawnPos = (Vector2)_playerTransform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                }
                else
                {
                    // 35% spawn casuale distribuito all'interno dell'arena
                    Vector2 randomOffset = Random.insideUnitCircle * (_spawnRadius * 0.75f);
                    // Mantieni una distanza minima dal giocatore per evitare collisioni immediate ingiuste
                    if (randomOffset.sqrMagnitude < 2.5f)
                    {
                        randomOffset = randomOffset.normalized * 2.2f;
                    }
                    spawnPos = (Vector2)_playerTransform.position + randomOffset;
                }

                if (ArenaBoundary.Instance != null)
                {
                    spawnPos = ArenaBoundary.Instance.ClampPosition(spawnPos, 0.9f);
                }

                // Nello sciame prioritizziamo i virus
                if (_virusPool != null && Random.value < 0.60f)
                {
                    VirusEnemy virus = _virusPool.Get();
                    virus.transform.position = spawnPos;
                    virus.Initialize(_playerTransform, wave, _returnVirusCallback);
                }
                else if (_basicPool != null)
                {
                    MicrobeEnemy basic = _basicPool.Get();
                    basic.transform.position = spawnPos;
                    basic.Initialize(_playerTransform, wave, _returnBasicCallback);
                }

                _spawnedThisWave++;

                // Intervallo scaglionato progressivo per coprire 5-10 secondi totali
                yield return new WaitForSeconds(delayBetweenSpawns + Random.Range(-0.03f, 0.03f));
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
            pod.Initialize(spawnPos, _returnTreasureCallback);
        }

        // ── Logica di Generazione Ondate Standard ──────────────────────────────

        private void SpawnNextEnemy()
        {
            int currentWave = GameManager.Instance?.Wave ?? 1;
            Vector2 spawnPos = GetRandomSpawnPosition();
            float roll = Random.value;

            // Selezione probabilistica in base alla Wave
            if (_icePool != null && currentWave >= 6 && roll < 0.28f)
            {
                IceBacteria ice = _icePool.Get();
                ice.transform.position = spawnPos;
                ice.Initialize(_playerTransform, currentWave, _returnIceCallback);
            }
            else if (_armoredPool != null && currentWave >= 3 && roll < (currentWave >= 6 ? 0.52f : 0.20f))
            {
                ArmoredBacteria armored = _armoredPool.Get();
                armored.transform.position = spawnPos;
                armored.Initialize(_playerTransform, currentWave, _returnArmoredCallback);
            }
            else if (_virusPool != null && currentWave >= 2 && roll < (currentWave >= 6 ? 0.78f : 0.55f))
            {
                VirusEnemy virus = _virusPool.Get();
                virus.transform.position = spawnPos;
                virus.Initialize(_playerTransform, currentWave, _returnVirusCallback);
            }
            else if (_basicPool != null)
            {
                MicrobeEnemy basic = _basicPool.Get();
                basic.transform.position = spawnPos;
                basic.Initialize(_playerTransform, currentWave, _returnBasicCallback);
            }

            _spawnedThisWave++;
        }

        private void SpawnBoss(int wave)
        {
            if (_bossPool == null || _playerTransform == null) return;
            Vector2 spawnPos = GetRandomSpawnPosition();
            BossEnemy boss = _bossPool.Get();
            boss.transform.position = spawnPos;
            boss.Initialize(_playerTransform, wave, _returnBossCallback);

            if (wave >= 10)
            {
                // Annuncio Apex a tutto schermo con flash per il Boss della Wave 10
                HUDController.Instance?.ShowEventAnnouncement(
                    "CRITICAL ALERT: APEX BIO-TITAN",
                    "MUTATED PATHOGEN PRIME DETECTED - LEVEL 10 HAZARD!",
                    new Color(0.92f, 0.10f, 0.95f), // Magenta mutageno
                    displayDuration: 4.2f,
                    flashScreen: true
                );

                // Spawna 2 virus veloci di scorta per proteggere il boss titanico
                if (_virusPool != null)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 minionPos = spawnPos + (Vector2)(Random.insideUnitCircle * 1.5f);
                        VirusEnemy minion = _virusPool.Get();
                        minion.transform.position = minionPos;
                        minion.Initialize(_playerTransform, wave, _returnVirusCallback);
                        _spawnedThisWave++;
                    }
                }
            }
            else
            {
                // Avviso standard per il boss della Wave 5
                HUDController.Instance?.ShowEventAnnouncement(
                    "WARNING: MEGA PATHOGEN DETECTED",
                    "COLOSSAL VIRUS INCOMING!",
                    new Color(1.0f, 0.20f, 0.35f),
                    displayDuration: 3.2f,
                    flashScreen: false
                );
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
            _basicPool?.Release(enemy);
        }

        private void ReturnVirusEnemy(MicrobeEnemy enemy)
        {
            _spawnedThisWave = Mathf.Max(0, _spawnedThisWave - 1);
            if (enemy is VirusEnemy v) _virusPool?.Release(v);
        }

        private void ReturnArmoredEnemy(MicrobeEnemy enemy)
        {
            _spawnedThisWave = Mathf.Max(0, _spawnedThisWave - 1);
            if (enemy is ArmoredBacteria a) _armoredPool?.Release(a);
        }

        private void ReturnIceEnemy(MicrobeEnemy enemy)
        {
            _spawnedThisWave = Mathf.Max(0, _spawnedThisWave - 1);
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
