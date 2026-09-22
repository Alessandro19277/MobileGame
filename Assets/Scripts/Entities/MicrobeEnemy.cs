using UnityEngine;
using MobileGame.Audio;
using MobileGame.Core;

namespace MobileGame.Entities
{
    /// <summary>
    /// Nemico base: insegue il player, lo danneggia per contatto, viene poolato da EnemySpawner.
    /// Include feedback visivo di colpo subito (Hit Flash bianco puro)
    /// e animazione di morte con sgretolamento/crumble in particelle e frammenti.
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class MicrobeEnemy : MonoBehaviour
    {
        [Header("Statistiche")]
        [SerializeField] protected float _baseSpeed        = 2f;
        [SerializeField] protected float _contactDamage    = 10f;
        [SerializeField] protected int   _scoreValue       = 10;
        [SerializeField] protected int   _xpGemsDropCount  = 1;

        // Pre-cachati in Awake — mai chiamati in Update (AGENTS.md §4)
        protected Rigidbody2D        _rb;
        protected HealthComponent    _health;
        protected SpriteRenderer     _sr;
        protected CircleCollider2D   _collider;
        protected Material           _defaultMaterial;
        protected static Material    s_flashMaterial;

        protected Transform                   _playerTransform;
        protected float                       _currentSpeed;
        protected System.Action<MicrobeEnemy> _onDied; // callback pool pre-allocato

        // Gestione Hit Flash & Sgretolamento (zero GC)
        protected float   _flashTimer;
        protected bool    _isDying;
        protected float   _crumbleTimer;
        protected Vector3 _initialLocalScale = Vector3.one;

        private const float FLASH_DURATION   = 0.085f;
        private const float CRUMBLE_DURATION = 0.22f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _rb       = GetComponent<Rigidbody2D>();
            _health   = GetComponent<HealthComponent>();
            _sr       = GetComponent<SpriteRenderer>();
            _collider = GetComponent<CircleCollider2D>();

            _rb.gravityScale = 0f;
            _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
            if (_collider != null) _collider.isTrigger = true;

            if (_sr != null)
            {
                _defaultMaterial = _sr.sharedMaterial;
            }

            EnsureFlashMaterial();

            // Scala proporzionata rispetto all'arena: 30% piu' piccoli
            transform.localScale *= 0.70f;
            _initialLocalScale = transform.localScale;

            if (_collider != null)
            {
                _collider.radius *= 0.85f;
            }

            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
                _health.OnDeath   += HandleDeath;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnDamaged -= HandleDamaged;
                _health.OnDeath   -= HandleDeath;
            }
        }

        private static void EnsureFlashMaterial()
        {
            if (s_flashMaterial != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/SpriteFlashWhite")
                         ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                         ?? Shader.Find("Sprites/Default");

            if (shader != null)
            {
                s_flashMaterial = new Material(shader);
            }
        }

        // ── Inizializzazione pool ──────────────────────────────────────────────

        /// <summary>Chiamato da EnemySpawner ogni volta che l'oggetto viene estratto dalla pool.</summary>
        public virtual void Initialize(Transform player, int wave, System.Action<MicrobeEnemy> onDied)
        {
            _playerTransform = player;
            _currentSpeed    = _baseSpeed + (wave - 1) * 0.3f;
            _onDied          = onDied;

            _isDying      = false;
            _flashTimer   = 0f;
            _crumbleTimer = 0f;

            if (_collider != null) _collider.enabled = true;
            if (_sr != null)
            {
                _sr.sharedMaterial           = _defaultMaterial;
                _sr.transform.localPosition = Vector3.zero;
            }
            transform.localScale = _initialLocalScale;

            _health.ResetHealth();
        }

        // ── Update per Hit Flash e Morte Crumble ────────────────────────────────

        protected virtual void Update()
        {
            // 1. Gestione Hit Flash bianco
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    _flashTimer = 0f;
                    if (_sr != null && !_isDying)
                    {
                        _sr.sharedMaterial = _defaultMaterial;
                    }
                }
            }

            // 2. Animazione di sgretolamento (Crumble)
            if (_isDying)
            {
                _crumbleTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_crumbleTimer / CRUMBLE_DURATION);

                // Vibrazione/jitter del corpo che si disintegra
                if (_sr != null)
                {
                    Vector2 jitter = Random.insideUnitCircle * (0.08f * (1f - t));
                    _sr.transform.localPosition = new Vector3(jitter.x, jitter.y, 0f);
                }

                // Collasso e riduzione in scala verso lo zero
                transform.localScale = _initialLocalScale * Mathf.Pow(t, 1.35f);

                if (_crumbleTimer <= 0f)
                {
                    FinishDeath();
                }
            }
        }

        // ── Loop fisica ────────────────────────────────────────────────────────

        protected virtual void FixedUpdate()
        {
            if (_playerTransform == null || _isDying) return;

            // Tutto su struct Vector2: zero alloc
            Vector2 dir  = ((Vector2)_playerTransform.position - _rb.position).normalized;
            Vector2 next = _rb.position + dir * (_currentSpeed * Time.fixedDeltaTime);

            if (ArenaBoundary.Instance != null)
            {
                next = ArenaBoundary.Instance.ClampPosition(next, 0.35f);
            }

            _rb.MovePosition(next);
        }

        // ── Collisioni ─────────────────────────────────────────────────────────

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDying || !other.CompareTag("Player")) return;

            other.GetComponent<HealthComponent>()?.TakeDamage(_contactDamage);
            AudioManager.Instance?.PlayPlayerHit();
            CameraFollow.Instance?.TriggerShake();
        }

        // ── Feedback Colpo (Hit Flash) ─────────────────────────────────────────

        protected virtual void HandleDamaged()
        {
            if (_isDying) return;

            _flashTimer = FLASH_DURATION;
            if (_sr != null && s_flashMaterial != null)
            {
                _sr.sharedMaterial = s_flashMaterial;
            }
        }

        // ── Morte & Sgretolamento ──────────────────────────────────────────────

        protected virtual void HandleDeath()
        {
            if (_isDying) return;
            _isDying = true;
            _crumbleTimer = CRUMBLE_DURATION;

            // Disattiva immediatamente il collider per non ostacolare proiettili o player
            if (_collider != null) _collider.enabled = false;

            // Assegna punteggio e rilascia drop
            GameManager.Instance?.AddScore(_scoreValue);

            // Rilascia sempre le gemme XP (la cura non toglie piu' esperienza al giocatore)
            for (int i = 0; i < _xpGemsDropCount; i++)
            {
                Vector2 offset = _xpGemsDropCount > 1 ? Random.insideUnitCircle * 0.35f : Vector2.zero;
                XPManager.Instance?.SpawnGem(_rb.position + offset);
            }

            // Drop dinamico della croce verde curativa (sistema salvavita / pity system):
            // Base 7% di probabilita'. Se il player e' ferito (< 60% HP), la probabilita' sale al 15%!
            float crossChance = 0.07f;
            var player = Player.PlayerController.Instance;
            if (player != null)
            {
                var hp = player.GetComponent<HealthComponent>();
                if (hp != null && hp.CurrentHealth < hp.MaxHealth * 0.6f)
                {
                    crossChance = 0.15f;
                }
            }

            if (Random.value < crossChance)
            {
                HealthPickupManager.Instance?.SpawnCross(_rb.position);
            }

            AudioManager.Instance?.PlayEnemyDie();
            MobileGame.Utilities.HapticManager.LightImpact();

            // Attiva l'effetto di sgretolamento in frammenti colorati
            EnemyDeathCrumbler.SpawnCrumble(_rb.position, GetThemeColor(), transform.localScale.x);

            // Breve flash bianco arcade all'avvio dello sgretolamento
            _flashTimer = 0.06f;
            if (_sr != null && s_flashMaterial != null)
            {
                _sr.sharedMaterial = s_flashMaterial;
            }
        }

        protected virtual void FinishDeath()
        {
            _isDying = false;
            if (_sr != null)
            {
                _sr.sharedMaterial           = _defaultMaterial;
                _sr.transform.localPosition = Vector3.zero;
            }
            transform.localScale = _initialLocalScale;
            if (_collider != null) _collider.enabled = true;

            _onDied?.Invoke(this);
        }

        /// <summary>
        /// Colore tematico del nemico usato per i frammenti di sgretolamento.
        /// </summary>
        protected virtual Color GetThemeColor() => new Color(0.15f, 0.95f, 0.2f); // Verde acido arcade
    }
}
