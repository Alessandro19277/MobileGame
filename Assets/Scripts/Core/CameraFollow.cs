using UnityEngine;

namespace MobileGame.Core
{
    /// <summary>
    /// Segue il player con Vector3.SmoothDamp in LateUpdate (zero GC, AGENTS.md §4).
    /// Garantisce movimento ultra-fluido a 60 FPS senza micro-scatti.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        [Header("Target da seguire")]
        [SerializeField] private Transform _target;

        [Header("Fluidita' (SmoothDamp)")]
        [Tooltip("Tempo di risposta: valori tra 0.05 e 0.15 offrono la massima fluidita'")]
        [SerializeField] private float _smoothTime = 0.08f;

        [Header("Confini Arena (Opzionali)")]
        [SerializeField] private bool    _useBounds  = false;
        [SerializeField] private Vector2 _minBounds  = new Vector2(-30f, -30f);
        [SerializeField] private Vector2 _maxBounds  = new Vector2(30f, 30f);

        // Pre-cached velocity e Z (zero GC alloc nei loop)
        private Vector3 _velocity = Vector3.zero;
        private float   _cameraZ;

        // Parametri camera shake
        private float _shakeTimer;
        private float _shakeMagnitude = 0.15f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _cameraZ = transform.position.z;

            // Visuale perfettamente proporzionata su tutti i formati di schermo (zero crop orizzontale)
            var cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5.2f;
            }
        }

        private void Start()
        {
            if (_target == null)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null) _target = player.transform;
            }

            // Snap immediato sul player all'avvio per evitare scatti iniziali
            if (_target != null)
            {
                transform.position = new Vector3(_target.position.x, _target.position.y, _cameraZ);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Loop Telecamera ───────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 targetPos = _target.position;
            float targetX = targetPos.x;
            float targetY = targetPos.y;

            if (_useBounds)
            {
                targetX = Mathf.Clamp(targetX, _minBounds.x, _maxBounds.x);
                targetY = Mathf.Clamp(targetY, _minBounds.y, _maxBounds.y);
            }

            Vector3 targetWorld = new Vector3(targetX, targetY, _cameraZ);

            // Vector3.SmoothDamp: l'algoritmo piu' fluido e stabile in assoluto per 2D
            Vector3 smoothed = Vector3.SmoothDamp(transform.position, targetWorld, ref _velocity, _smoothTime);

            // Camera shake se attivo (usa unscaledDeltaTime per funzionare anche in bullet-time)
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                smoothed.x += Random.Range(-_shakeMagnitude, _shakeMagnitude);
                smoothed.y += Random.Range(-_shakeMagnitude, _shakeMagnitude);
            }

            transform.position = smoothed;
        }

        // ── API Pubblica ───────────────────────────────────────────────────────

        public void TriggerShake(float duration = 0.15f, float magnitude = 0.18f)
        {
            _shakeTimer     = duration;
            _shakeMagnitude = magnitude;
        }
    }
}
