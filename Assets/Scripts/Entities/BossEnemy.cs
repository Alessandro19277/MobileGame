using UnityEngine;
using MobileGame.Audio;
using MobileGame.Core;
using MobileGame.Utilities;

namespace MobileGame.Entities
{
    public enum BossActionState
    {
        Stalking,
        Telegraphed,
        Dashing,
        MultiDashPause
    }

    /// <summary>
    /// Boss gigante: mega-patogeno con tanti HP, carica periodica e drop multiplo di XP.
    /// Alla Wave 10+ muta in "Bio-Titan Apex":
    /// - 1200 HP (~3.4x)
    /// - Combo Multi-Dash (doppio scatto a catena)
    /// - Anello di 8 spore tossiche a 360° durante la carica
    /// - Modalità Berserk / Overdrive Mutageno sotto il 35% di salute
    /// - Drop di 14 gemme XP + croce curativa garantita
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    public class BossEnemy : MicrobeEnemy
    {
        [Header("Comportamento Boss")]
        [SerializeField] private float _baseDashCooldown       = 4.2f;
        [SerializeField] private float _apexDashCooldown       = 3.2f;
        [SerializeField] private float _enragedDashCooldown    = 2.0f;
        [SerializeField] private float _dashSpeedMultiplier    = 2.6f;

        private BossActionState _state = BossActionState.Stalking;
        private float           _actionTimer;
        private Vector2         _dashDirection;

        private int   _wave;
        private int   _tier = 1;
        private bool  _isApexBoss;
        private bool  _isEnraged;
        private int   _dashesDoneInChain;
        private int   _maxDashesInChain = 1;
        private float _activeDashCooldown;
        private float _sporeDamage = 12f;

        // Palette cromatica dinamica per Tier
        private static readonly Color COLOR_WAVE5_CRIMSON    = new Color(0.95f, 0.12f, 0.38f, 1.0f);
        private static readonly Color COLOR_APEX_MAGENTA     = new Color(0.88f, 0.08f, 0.95f, 1.0f);
        private static readonly Color COLOR_ABYSSAL_PURPLE   = new Color(0.42f, 0.05f, 0.95f, 1.0f);
        private static readonly Color COLOR_OMEGA_BLACK_CORE = new Color(0.16f, 0.04f, 0.28f, 1.0f);
        private static readonly Color COLOR_ENRAGED_ALERT    = new Color(1.00f, 0.15f, 0.15f, 1.0f);

        // 8 Direzioni radiali pre-calcolate (zero boxing o calcoli trigonometrici in loop)
        private static readonly Vector2[] SPORE_DIRECTIONS = new Vector2[8]
        {
            new Vector2( 1.0000f,  0.0000f),
            new Vector2( 0.7071f,  0.7071f),
            new Vector2( 0.0000f,  1.0000f),
            new Vector2(-0.7071f,  0.7071f),
            new Vector2(-1.0000f,  0.0000f),
            new Vector2(-0.7071f, -0.7071f),
            new Vector2( 0.0000f, -1.0000f),
            new Vector2( 0.7071f, -0.7071f)
        };

        // Pool condiviso delle spore tossiche radiali
        private static ObjectPool<BossSporeProjectile> s_sporePool;
        private static System.Action<BossSporeProjectile> s_returnSporeCallback;
        private static Transform s_sporeRoot;

        protected override void Awake()
        {
            base.Awake();
            EnsureSporePool();
        }

        private static void EnsureSporePool()
        {
            if (s_sporePool != null) return;

            if (s_sporeRoot == null)
            {
                var rootGo = new GameObject("SharedBossSporePool");
                Object.DontDestroyOnLoad(rootGo);
                s_sporeRoot = rootGo.transform;
            }

            s_returnSporeCallback = ReturnSpore;

            s_sporePool = new ObjectPool<BossSporeProjectile>(
                factory: () =>
                {
                    var sporeGo = new GameObject("BossSpore");
                    sporeGo.transform.SetParent(s_sporeRoot);
                    sporeGo.AddComponent<SpriteRenderer>();
                    sporeGo.AddComponent<Rigidbody2D>();
                    sporeGo.AddComponent<CircleCollider2D>();
                    return sporeGo.AddComponent<BossSporeProjectile>();
                },
                onGet: p => p.gameObject.SetActive(true),
                onRelease: p =>
                {
                    p.gameObject.SetActive(false);
                    p.transform.SetParent(s_sporeRoot);
                },
                initialCapacity: 24
            );
        }

        private static void ReturnSpore(BossSporeProjectile spore)
        {
            s_sporePool?.Release(spore);
        }

        public override void Initialize(Transform player, int wave, System.Action<MicrobeEnemy> onDied)
        {
            base.Initialize(player, wave, onDied);

            _wave       = wave;
            _tier       = Mathf.Max(1, wave / 5);
            _isApexBoss = _tier >= 2;
            _isEnraged  = false;

            _state             = BossActionState.Stalking;
            _actionTimer       = 0f;
            _dashesDoneInChain = 0;

            // Statistiche dinamiche progressive per Tier (Wave 5, 10, 15, 20, 25...)
            float bossHealth;
            if (_tier == 1)      bossHealth = 450f;
            else if (_tier == 2) bossHealth = 1400f;
            else if (_tier == 3) bossHealth = 2900f;
            else if (_tier == 4) bossHealth = 5000f;
            else                 bossHealth = 5000f + (_tier - 4) * 2500f;

            _health.SetMaxHealth(bossHealth);

            // Danno da contatto progressivo (37 alla Wave 5 fino a 76+ alla Wave 20+)
            _contactDamage = 24f + _tier * 13f;

            // Danno delle spore tossiche
            _sporeDamage = 10f + _tier * 3.5f;

            // Configurazione combo di scatto e dimensioni per Tier
            if (_tier >= 3)
            {
                // Wave 15+ (Abyssal / Omega): triplo scatto a catena e cooldown ridotto
                transform.localScale  = _initialLocalScale * (1.30f + Mathf.Min(0.30f, (_tier - 3) * 0.10f));
                _xpGemsDropCount      = 16 + (_tier - 2) * 6; // 22 a Wave 15, 28 a Wave 20
                _maxDashesInChain     = 3; // Triplo scatto a catena!
                _activeDashCooldown   = Mathf.Max(2.2f, 2.8f - (_tier - 3) * 0.3f);
            }
            else if (_tier == 2)
            {
                // Wave 10 (Apex): doppio scatto
                transform.localScale  = _initialLocalScale * 1.25f;
                _xpGemsDropCount      = 14;
                _maxDashesInChain     = 2; // Doppio scatto
                _activeDashCooldown   = _apexDashCooldown;
            }
            else
            {
                // Wave 5 (Mega Pathogen): scatto singolo
                transform.localScale  = _initialLocalScale;
                _xpGemsDropCount      = 6;
                _maxDashesInChain     = 1;
                _activeDashCooldown   = _baseDashCooldown;
            }

            ApplyThemeVisuals();
        }

        private void ApplyThemeVisuals()
        {
            if (_sr != null)
            {
                if (_tier >= 4)      _sr.color = COLOR_OMEGA_BLACK_CORE;
                else if (_tier == 3) _sr.color = COLOR_ABYSSAL_PURPLE;
                else if (_tier == 2) _sr.color = COLOR_APEX_MAGENTA;
                else                 _sr.color = COLOR_WAVE5_CRIMSON;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (_isDying) return;

            // Se in Enraged o Apex, pulsazione visiva per segnalare pericolo estremo
            if (_isEnraged && _sr != null && _flashTimer <= 0f)
            {
                float pulse = (Mathf.Sin(Time.time * 9.5f) + 1f) * 0.5f;
                Color baseCol = _tier >= 4 ? COLOR_OMEGA_BLACK_CORE : (_tier == 3 ? COLOR_ABYSSAL_PURPLE : COLOR_APEX_MAGENTA);
                _sr.color = Color.Lerp(COLOR_ENRAGED_ALERT, baseCol, pulse);
            }
        }

        protected override void FixedUpdate()
        {
            if (_playerTransform == null || _isDying) return;

            // Controllo soglia Berserk (< 35% HP per Apex Boss)
            if (_isApexBoss && !_isEnraged && _health.CurrentHealth / _health.MaxHealth <= 0.35f)
            {
                TriggerEnrage();
            }

            _actionTimer += Time.fixedDeltaTime;

            switch (_state)
            {
                case BossActionState.Stalking:
                    HandleStalking();
                    break;

                case BossActionState.Telegraphed:
                    HandleTelegraphed();
                    break;

                case BossActionState.Dashing:
                    HandleDashing();
                    break;

                case BossActionState.MultiDashPause:
                    HandleMultiDashPause();
                    break;
            }
        }

        private void HandleStalking()
        {
            // Inseguimento costante verso il player
            Vector2 dir = ((Vector2)_playerTransform.position - _rb.position).normalized;
            Vector2 nextStalk = _rb.position + dir * (_currentSpeed * Time.fixedDeltaTime);
            if (ArenaBoundary.Instance != null)
            {
                nextStalk = ArenaBoundary.Instance.ClampPosition(nextStalk, 0.85f);
            }
            _rb.MovePosition(nextStalk);

            if (_actionTimer >= _activeDashCooldown)
            {
                _state              = BossActionState.Telegraphed;
                _actionTimer        = 0f;
                _dashDirection      = dir; // Fissa la traiettoria di carica
                _dashesDoneInChain  = 0;
            }
        }

        private void HandleTelegraphed()
        {
            // Durata telegrafo: 0.38s standard, 0.24s per Apex/Enraged
            float telegraphDuration = _isEnraged ? 0.20f : (_isApexBoss ? 0.26f : 0.38f);

            // Piccolo arretramento elastico durante la carica
            Vector2 backoff = -_dashDirection * (0.8f * Time.fixedDeltaTime);
            _rb.MovePosition(_rb.position + backoff);

            if (_actionTimer >= telegraphDuration)
            {
                StartDash();
            }
        }

        private void StartDash()
        {
            _state       = BossActionState.Dashing;
            _actionTimer = 0f;
            _dashesDoneInChain++;

            // Shake leggero all'avvio della carica
            CameraFollow.Instance?.TriggerShake(0.18f, 0.22f);
            HapticManager.MediumImpact();

            // Alla Wave 10+: Rilascio istantaneo dell'anello di 8 spore tossiche a 360°!
            if (_isApexBoss)
            {
                EmitSporeRing();
            }
        }

        private void HandleDashing()
        {
            // Scatto ad alta velocità lungo la traiettoria
            float currentMultiplier = _isEnraged ? (_dashSpeedMultiplier * 1.20f) : _dashSpeedMultiplier;
            Vector2 nextDash = _rb.position + _dashDirection * (_currentSpeed * currentMultiplier * Time.fixedDeltaTime);
            if (ArenaBoundary.Instance != null)
            {
                nextDash = ArenaBoundary.Instance.ClampPosition(nextDash, 0.85f);
            }
            _rb.MovePosition(nextDash);

            float dashDuration = _isApexBoss ? 0.65f : 0.80f;
            if (_actionTimer >= dashDuration)
            {
                // Se fa parte di una combo Multi-Dash ed ha ancora cariche residue
                if (_isApexBoss && _dashesDoneInChain < _maxDashesInChain)
                {
                    _state       = BossActionState.MultiDashPause;
                    _actionTimer = 0f;
                }
                else
                {
                    _state       = BossActionState.Stalking;
                    _actionTimer = 0f;
                }
            }
        }

        private void HandleMultiDashPause()
        {
            // Brevissima pausa per riagganciare il player (0.22s) e lanciare il secondo scatto
            float pauseDuration = _isEnraged ? 0.16f : 0.22f;

            if (_actionTimer >= pauseDuration)
            {
                // Ricalcola la direzione verso la posizione attuale del player!
                _dashDirection = ((Vector2)_playerTransform.position - _rb.position).normalized;
                _state         = BossActionState.Telegraphed;
                _actionTimer   = 0f;
            }
        }

        private void TriggerEnrage()
        {
            _isEnraged          = true;
            _activeDashCooldown = _enragedDashCooldown;
            _currentSpeed      *= 1.30f;

            // Ruggito sismico / Overdrive
            CameraFollow.Instance?.TriggerShake(0.40f, 0.45f);
            HapticManager.HeavyImpact();
            AudioManager.Instance?.PlayPlayerHit();
        }

        /// <summary>
        /// Sprigiona 8 spore tossiche ad anello a 360° partendo dalla posizione del boss (zero GC).
        /// </summary>
        private void EmitSporeRing()
        {
            if (s_sporePool == null) return;

            Vector2 origin = _rb.position;
            for (int i = 0; i < SPORE_DIRECTIONS.Length; i++)
            {
                BossSporeProjectile spore = s_sporePool.Get();
                spore.Launch(origin, SPORE_DIRECTIONS[i], s_returnSporeCallback, _sporeDamage);
            }
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            other.GetComponent<HealthComponent>()?.TakeDamage(_contactDamage);
            AudioManager.Instance?.PlayPlayerHit();
            CameraFollow.Instance?.TriggerShake(0.30f, 0.40f);
            HapticManager.HeavyImpact();
        }

        protected override void HandleDeath()
        {
            base.HandleDeath();

            // Feedback aptico potente per la caduta del boss
            HapticManager.HeavyImpact();

            // Croce verde curativa garantita al centro del drop
            HealthPickupManager.Instance?.SpawnCross(_rb.position + Vector2.up * 0.35f);
        }

        protected override Color GetThemeColor()
        {
            if (_isEnraged) return COLOR_ENRAGED_ALERT;
            if (_tier >= 4) return COLOR_OMEGA_BLACK_CORE;
            if (_tier == 3) return COLOR_ABYSSAL_PURPLE;
            return _isApexBoss ? COLOR_APEX_MAGENTA : COLOR_WAVE5_CRIMSON;
        }
    }
}
