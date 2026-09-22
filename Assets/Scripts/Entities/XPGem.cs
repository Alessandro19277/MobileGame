using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Entities
{
    /// <summary>
    /// Gemma XP poolata: rimane ferma finché il player non entra nel raggio magnetico,
    /// poi si muove verso di lui e lo raccoglie. Zero alloc in Update (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class XPGem : MonoBehaviour
    {
        [SerializeField] private float _baseMagnetRange = 3f;
        [SerializeField] private float _magnetSpeed     = 8f;

        private int                  _xpValue;
        private System.Action<XPGem> _onCollected;
        private Transform            _playerTransform;
        private bool                 _attracted;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            GetComponent<CircleCollider2D>().isTrigger = true;
        }

        private void Start()
        {
            // Cache del player: FindWithTag solo in Start, non in Update (AGENTS.md §4)
            var p = GameObject.FindWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }

        // ── Pool ──────────────────────────────────────────────────────────────

        /// <summary>Chiamato da XPManager ogni volta che la gem viene estratta dalla pool.</summary>
        public void Initialize(int xpValue, System.Action<XPGem> onCollected)
        {
            _xpValue     = xpValue;
            _onCollected = onCollected;
            _attracted   = false;
        }

        // ── Update ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_playerTransform == null) return;

            // Range magnetico modificato da upgrade (float mul): zero alloc
            float range    = _baseMagnetRange * (XPManager.Instance?.MagnetMultiplier ?? 1f);
            Vector2 toPlayer = (Vector2)_playerTransform.position - (Vector2)transform.position;

            if (!_attracted && toPlayer.sqrMagnitude <= range * range)
                _attracted = true;

            if (_attracted)
            {
                // MoveTowards su struct Vector2: zero alloc
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    _playerTransform.position,
                    _magnetSpeed * Time.deltaTime);
            }
        }

        // ── Raccolta ──────────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;
            XPManager.Instance?.AddXP(_xpValue);
            _onCollected?.Invoke(this);
        }
    }
}
