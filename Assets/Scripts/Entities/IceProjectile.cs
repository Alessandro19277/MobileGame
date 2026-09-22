using UnityEngine;
using MobileGame.Core;
using MobileGame.Audio;
using MobileGame.Player;

namespace MobileGame.Entities
{
    /// <summary>
    /// Proiettile criogenico sparato dal Batterio di Ghiaccio:
    /// viaggia in linea retta, danneggia il Player e gli applica un debuff di rallentamento.
    /// Poolato e zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class IceProjectile : MonoBehaviour
    {
        [Header("Statistiche")]
        [SerializeField] private float _speed        = 6.5f;
        [SerializeField] private float _damage       = 8f;
        [SerializeField] private float _slowPercent  = 0.45f;
        [SerializeField] private float _slowDuration = 2.5f;
        [SerializeField] private float _lifetime     = 3.5f;

        private Rigidbody2D    _rb;
        private SpriteRenderer _sr;

        private Vector2                       _direction;
        private float                         _timer;
        private System.Action<IceProjectile>  _onExpired;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();

            _rb.gravityScale = 0f;
            _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.22f;

            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        }

        /// <summary>
        /// Inizializza e lancia il proiettile di ghiaccio nella direzione bersaglio.
        /// </summary>
        public void Launch(Vector2 startPos, Vector2 direction, System.Action<IceProjectile> onExpired)
        {
            transform.position = startPos;
            _direction         = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
            _timer             = 0f;
            _onExpired         = onExpired;

            // Orientamento visuale del cristallo verso la traiettoria
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void FixedUpdate()
        {
            Vector2 next = _rb.position + _direction * (_speed * Time.fixedDeltaTime);
            _rb.MovePosition(next);

            if (ArenaBoundary.Instance != null && !ArenaBoundary.Instance.IsInside(next))
            {
                Expire();
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _lifetime)
            {
                Expire();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            // Danneggia il player
            other.GetComponent<HealthComponent>()?.TakeDamage(_damage);

            // Applica il rallentamento da congelamento
            PlayerController.Instance?.ApplySlow(_slowPercent, _slowDuration);

            AudioManager.Instance?.PlayPlayerHit();
            CameraFollow.Instance?.TriggerShake(0.12f, 0.15f);

            Expire();
        }

        private void Expire()
        {
            var cb = _onExpired;
            _onExpired = null;
            cb?.Invoke(this);
        }
    }
}
