using UnityEngine;
using MobileGame.Core;
using MobileGame.Audio;

namespace MobileGame.Entities
{
    /// <summary>
    /// Proiettile bio-spora tossica sparato ad anello dal Boss Apex (Wave 10+):
    /// viaggia in linea retta a velocità costante, danneggia il Player al contatto e viene riciclato nel pool.
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BossSporeProjectile : MonoBehaviour
    {
        [Header("Statistiche Spora")]
        [SerializeField] private float _speed    = 5.5f;
        [SerializeField] private float _damage   = 12f;
        [SerializeField] private float _lifetime = 4.0f;

        private Rigidbody2D    _rb;
        private SpriteRenderer _sr;

        private Vector2                             _direction;
        private float                               _timer;
        private System.Action<BossSporeProjectile>  _onExpired;

        private static Sprite s_sporeSprite;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _sr = GetComponent<SpriteRenderer>();

            _rb.gravityScale = 0f;
            _rb.constraints  = RigidbodyConstraints2D.FreezeRotation;

            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius    = 0.22f;

            transform.localScale = new Vector3(0.55f, 0.55f, 1f);

            EnsureSprite();
            if (_sr != null && s_sporeSprite != null)
            {
                _sr.sprite       = s_sporeSprite;
                _sr.color        = new Color(1.0f, 0.18f, 0.85f, 1.0f); // Magenta tossico brillante
                _sr.sortingOrder = 90;
            }
        }

        private static void EnsureSprite()
        {
            if (s_sporeSprite != null) return;

            // Spora radiale circolare pixel-perfect con alone bioluminescente
            int size = 24;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    if (dist <= 1f)
                    {
                        float alpha = Mathf.Clamp01(1f - dist);
                        alpha = Mathf.Pow(alpha, 1.4f);
                        float core = dist < 0.35f ? 1f : 0.8f;
                        tex.SetPixel(x, y, new Color(core, core, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            s_sporeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        }

        /// <summary>
        /// Inizializza e lancia la spora nella direzione bersaglio.
        /// </summary>
        public void Launch(Vector2 startPos, Vector2 direction, System.Action<BossSporeProjectile> onExpired)
        {
            transform.position = startPos;
            _direction         = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.down;
            _timer             = 0f;
            _onExpired         = onExpired;

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

            AudioManager.Instance?.PlayPlayerHit();
            CameraFollow.Instance?.TriggerShake(0.12f, 0.16f);

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
