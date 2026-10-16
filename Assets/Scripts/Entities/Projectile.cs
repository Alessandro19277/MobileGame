using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Entities
{
    /// <summary>
    /// Proiettile poolato: si muove in linea retta, danneggia i nemici, scade per timer.
    /// FixedUpdate GC-free: solo struct Vector2 (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("Statistiche")]
        [SerializeField] private float _speed    = 10f;
        [SerializeField] private float _lifetime = 3f;
        // _damage viene impostato da PlayerShooter.TryShoot() via SetDamage() per applicare i bonus upgrade
        private float _damage = 25f;
        private int   _pierceCount = 1;

        private Collider2D                _lastHitCollider;
        private Rigidbody2D               _rb;
        private SpriteRenderer            _sr;

        private Vector2                   _direction;
        private float                     _timer;
        private bool                      _isCrit;
        private bool                      _isExpired;
        private System.Action<Projectile> _onExpired; // callback pool, pre-allocato in PlayerShooter.Awake

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();
            _rb.gravityScale = 0f;
            _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        // ── Inizializzazione pool ──────────────────────────────────────────────

        /// <summary>
        /// Inizializza e lancia il proiettile con sincronizzazione atomica di Transform e Rigidbody2D
        /// prima di attivare il GameObject (zero frame di desincronizzazione o morte istantanea).
        /// </summary>
        public void PrepareAndLaunch(
            Vector2 spawnPos,
            Vector2 direction,
            System.Action<Projectile> onExpired,
            float damage,
            int pierceCount,
            bool isCrit = false)
        {
            // 1. Sincronizza posizioni
            transform.position = spawnPos;
            if (_rb != null)
            {
                _rb.position = spawnPos;
                _rb.linearVelocity = Vector2.zero;
            }

            // 2. Direzione e rotazione (fallback se a contatto sovrapposto col nemico)
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // 3. Statistiche e timer
            _damage           = damage;
            _pierceCount      = Mathf.Max(1, pierceCount);
            _timer            = 0f;
            _isCrit           = isCrit;
            _isExpired        = false;
            _lastHitCollider  = null;
            _onExpired        = onExpired;

            if (_sr != null)
            {
                _sr.color = isCrit ? new Color(1f, 0.85f, 0.1f) : Color.white;
            }

            // 4. Attiva il GameObject solo a stato completato
            gameObject.SetActive(true);
        }

        /// <summary>Chiamato da PlayerShooter per applicare i bonus danno e penetrazione upgrade.</summary>
        public void SetDamage(float damage, int pierceCount = 1)
        {
            _damage      = damage;
            _pierceCount = Mathf.Max(1, pierceCount);
        }

        /// <summary>Chiamato da PlayerShooter ogni volta che il proiettile viene estratto dalla pool.</summary>
        public void Launch(Vector2 direction, System.Action<Projectile> onExpired, bool isCrit = false)
        {
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.up;
            _timer     = 0f;
            _isCrit    = isCrit;
            _isExpired = false;
            _lastHitCollider = null;
            _onExpired = onExpired;

            if (_sr != null)
            {
                _sr.color = isCrit ? new Color(1f, 0.85f, 0.1f) : Color.white;
            }

            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ── Loop ──────────────────────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (_isExpired) return;

            Vector2 next = _rb.position + _direction * (_speed * Time.fixedDeltaTime);
            _rb.MovePosition(next);

            // Se tocca o supera la recinzione dell'arena, dissolve il proiettile (zero GC)
            if (ArenaBoundary.Instance != null && !ArenaBoundary.Instance.IsInside(next))
            {
                Expire();
            }
        }

        private void Update()
        {
            if (_isExpired) return;

            _timer += Time.deltaTime;
            if (_timer >= _lifetime) Expire();
        }

        // ── Collisioni ─────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isExpired) return;
            if (other == _lastHitCollider) return; // Evita di consumare pierce multiplo sullo stesso nemico
            if (!other.CompareTag("Enemy")) return;

            HealthComponent hp = other.GetComponent<HealthComponent>();
            if (hp != null && hp.IsDead) return; // Non sprecare dardi su nemici già disintegrati

            _lastHitCollider = other;

            hp?.TakeDamage(_damage);
            if (other.TryGetComponent<MicrobeEnemy>(out var enemy))
            {
                enemy.ApplyKnockback(_direction, 0.50f);
            }
            MobileGame.UI.DamageNumberManager.Spawn(other.transform.position, Mathf.RoundToInt(_damage), _isCrit);

            _pierceCount--;
            if (_pierceCount <= 0)
            {
                Expire();
            }
        }

        private void Expire()
        {
            if (_isExpired) return;
            _isExpired = true;

            var cb = _onExpired;
            _onExpired = null;
            cb?.Invoke(this);
        }
    }
}
