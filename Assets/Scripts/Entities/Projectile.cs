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

        private Rigidbody2D    _rb;
        private SpriteRenderer _sr;

        private Vector2                   _direction;
        private float                     _timer;
        private bool                      _isCrit;
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

        /// <summary>Chiamato da PlayerShooter per applicare i bonus danno e penetrazione upgrade.</summary>
        public void SetDamage(float damage, int pierceCount = 1)
        {
            _damage      = damage;
            _pierceCount = Mathf.Max(1, pierceCount);
        }

        /// <summary>Chiamato da PlayerShooter ogni volta che il proiettile viene estratto dalla pool.</summary>
        public void Launch(Vector2 direction, System.Action<Projectile> onExpired, bool isCrit = false)
        {
            _direction = direction.normalized;
            _timer     = 0f;
            _isCrit    = isCrit;
            _onExpired = onExpired;

            if (_sr != null)
            {
                _sr.color = isCrit ? new Color(1f, 0.85f, 0.1f) : Color.white; // Oro per i colpi critici
            }

            // Ruota il proiettile nella direzione di volo (punta a destra = 0 gradi)
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ── Loop ──────────────────────────────────────────────────────────────

        private void FixedUpdate()
        {
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
            _timer += Time.deltaTime;
            if (_timer >= _lifetime) Expire();
        }

        // ── Collisioni ─────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;
            other.GetComponent<HealthComponent>()?.TakeDamage(_damage);
            MobileGame.UI.DamageNumberManager.Spawn(other.transform.position, Mathf.RoundToInt(_damage), _isCrit);

            _pierceCount--;
            if (_pierceCount <= 0)
            {
                Expire();
            }
        }

        private void Expire() => _onExpired?.Invoke(this);
    }
}
