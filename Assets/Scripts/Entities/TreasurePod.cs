using UnityEngine;
using MobileGame.Audio;
using MobileGame.Core;
using MobileGame.Utilities;

namespace MobileGame.Entities
{
    /// <summary>
    /// Capsula del tesoro biologico ad evento improvviso.
    /// Non si muove (stazionaria), può essere colpita dalle armi del giocatore.
    /// Possiede animazione di pulsazione a riposo, hit-flash bianco quando subisce colpi,
    /// e animazione di sgretolamento/crumble in particelle dorate quando viene distrutta.
    /// Alla distruzione rilascia una fontana di gemme XP e una cura guaranteed.
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(HealthComponent))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class TreasurePod : MonoBehaviour
    {
        [Header("Statistiche")]
        [SerializeField] private float _maxHealth = 120f;
        [SerializeField] private int   _scoreValue = 150;
        [SerializeField] private int   _gemDropCount = 8;

        private Rigidbody2D      _rb;
        private HealthComponent  _health;
        private SpriteRenderer   _sr;
        private CircleCollider2D _collider;
        private Material         _defaultMaterial;
        private static Material  s_flashMaterial;

        private System.Action<TreasurePod> _onDied;
        private float   _flashTimer;
        private bool    _isDying;
        private float   _crumbleTimer;
        private float   _pulseTimer;
        private Vector3 _baseScale = new Vector3(0.50f, 0.50f, 1f);
        private bool    _hasDiedAndReleased;

        private const float FLASH_DURATION   = 0.085f;
        private const float CRUMBLE_DURATION = 0.30f;

        private static readonly Color GOLD_THEME = new Color(1.0f, 0.85f, 0.15f);

        private void Awake()
        {
            _rb       = GetComponent<Rigidbody2D>();
            _health   = GetComponent<HealthComponent>();
            _sr       = GetComponent<SpriteRenderer>();
            _collider = GetComponent<CircleCollider2D>();

            _rb.gravityScale = 0f;
            _rb.bodyType     = RigidbodyType2D.Kinematic;
            _rb.constraints  = RigidbodyConstraints2D.FreezeAll;

            gameObject.tag   = "Enemy"; // Permette a proiettili, sfere e shockwave di colpirla
            gameObject.layer = 3;       // Layer Enemy

            if (_collider != null)
            {
                _collider.isTrigger = true;
                _collider.radius    = 0.32f;
            }

            transform.localScale = _baseScale;

            if (_sr != null)
            {
                if (_sr.sprite == null)
                {
                    _sr.sprite = Resources.Load<Sprite>("Sprites/treasure_pod");
                }
                _sr.sortingOrder = 2;
                _defaultMaterial = _sr.sharedMaterial;
            }

            EnsureFlashMaterial();

            if (_health != null)
            {
                _health.OnDamaged += HandleDamaged;
                _health.OnDeath   += HandleDeath;
            }
        }

        private void OnDestroy()
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
            if (shader != null) s_flashMaterial = new Material(shader);
        }

        public void Initialize(Vector2 spawnPosition, System.Action<TreasurePod> onDied)
        {
            transform.position  = spawnPosition;
            _onDied              = onDied;
            _hasDiedAndReleased = false;

            _isDying      = false;
            _flashTimer   = 0f;
            _crumbleTimer = 0f;
            _pulseTimer   = 0f;

            if (_collider != null) _collider.enabled = true;
            if (_sr != null)
            {
                _sr.sharedMaterial          = _defaultMaterial;
                _sr.transform.localPosition = Vector3.zero;
            }
            transform.localScale = _baseScale;

            if (_health != null)
            {
                _health.SetMaxHealth(_maxHealth);
                _health.ResetHealth();
            }
        }

        private void Update()
        {
            // 1. Gestione Flash Bianco
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

            // 2. Animazione di morte sgretolamento / smolecolamento
            if (_isDying)
            {
                _crumbleTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(_crumbleTimer / CRUMBLE_DURATION);

                if (_sr != null)
                {
                    Vector2 jitter = Random.insideUnitCircle * (0.12f * (1f - t));
                    _sr.transform.localPosition = new Vector3(jitter.x, jitter.y, 0f);
                }

                transform.localScale = _baseScale * Mathf.Pow(t, 1.4f);

                if (_crumbleTimer <= 0f)
                {
                    FinishDeath();
                }
                return;
            }

            // 3. Animazione di pulsazione a riposo (respiro energetico nucleo dorato)
            _pulseTimer += Time.deltaTime * 3.5f;
            float pulseScale = 1.0f + Mathf.Sin(_pulseTimer) * 0.05f;
            transform.localScale = _baseScale * pulseScale;
        }

        private void HandleDamaged()
        {
            if (_isDying) return;

            _flashTimer = FLASH_DURATION;
            if (_sr != null && s_flashMaterial != null)
            {
                _sr.sharedMaterial = s_flashMaterial;
            }
        }

        private void HandleDeath()
        {
            if (_isDying) return;
            _isDying = true;
            _crumbleTimer = CRUMBLE_DURATION;

            if (_collider != null) _collider.enabled = false;

            GameManager.Instance?.AddScore(_scoreValue);
            AudioManager.Instance?.PlayEnemyDie();
            HapticManager.HeavyImpact();

            // Esplosione di sgretolamento in pixel d'oro brillante
            EnemyDeathCrumbler.SpawnCrumble(transform.position, GOLD_THEME, 1.4f);

            // Fontana di gemme XP dorate
            if (XPManager.Instance != null)
            {
                for (int i = 0; i < _gemDropCount; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * 0.75f;
                    XPManager.Instance.SpawnGem((Vector2)transform.position + offset);
                }
            }

            // Croce di cura bonus per premiare l'apertura del forziere
            HealthPickupManager.Instance?.SpawnCross((Vector2)transform.position + Vector2.up * 0.4f);

            // Breve flash iniziale prima dello sgretolamento
            _flashTimer = 0.06f;
            if (_sr != null && s_flashMaterial != null)
            {
                _sr.sharedMaterial = s_flashMaterial;
            }
        }

        private void FinishDeath()
        {
            if (_hasDiedAndReleased) return;
            _hasDiedAndReleased = true;

            _isDying = false;
            if (_collider != null) _collider.enabled = false;
            gameObject.SetActive(false);

            var cb = _onDied;
            _onDied = null;
            cb?.Invoke(this);
        }
    }
}
