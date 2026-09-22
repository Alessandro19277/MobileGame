using UnityEngine;

namespace MobileGame.Core
{
    /// <summary>
    /// Crea una barriera fisica circolare invalicabile (EdgeCollider2D a loop) attorno all'Arena,
    /// impedendo al Player e ai nemici di uscire dalla membrana/recinzione.
    /// Si calibra automaticamente sullo SpriteRenderer (o manualmente) ed espone ClampPosition per zero GC.
    /// </summary>
    [RequireComponent(typeof(EdgeCollider2D))]
    [ExecuteAlways]
    public class ArenaBoundary : MonoBehaviour
    {
        public static ArenaBoundary Instance { get; private set; }

        [Header("Calibrazione Raggio")]
        [Tooltip("Se abilitato, calcola automaticamente il raggio in base allo SpriteRenderer e al rapporto membrana")]
        [SerializeField] private bool _autoFitToSprite = true;

        [Tooltip("Rapporto tra la membrana interna e il bordo dello sprite (es. 0.90 = membrana al 90%)")]
        [Range(0.5f, 1.0f)]
        [SerializeField] private float _membraneRatio = 0.90f;

        [Tooltip("Raggio dell'arena invalicabile in unità world (usato direttamente se AutoFit è disattivato)")]
        [SerializeField] private float _radius = 18f;

        [Tooltip("Numero di punti per definire il cerchio fisico")]
        [SerializeField] private int _segments = 64;

        public float Radius => _radius;
        public Vector2 Center => (Vector2)transform.position;

        private EdgeCollider2D _edgeCollider;
        private SpriteRenderer _spriteRenderer;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
            }

            _edgeCollider = GetComponent<EdgeCollider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            UpdateBoundary();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && Instance == null)
            {
                Instance = this;
            }
        }

        private void OnValidate()
        {
            UpdateBoundary();
        }

        private void Update()
        {
            // In Edit Mode aggiorna in tempo reale se si scala o sposta l'oggetto nella Scene View
            if (!Application.isPlaying)
            {
                UpdateBoundary();
            }
        }

        public void UpdateBoundary()
        {
            if (_edgeCollider == null) _edgeCollider = GetComponent<EdgeCollider2D>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_autoFitToSprite && _spriteRenderer != null && _spriteRenderer.sprite != null)
            {
                float outerRadius = _spriteRenderer.bounds.extents.x;
                if (outerRadius > 0.1f)
                {
                    _radius = outerRadius * _membraneRatio;
                }
            }

            if (_segments >= 8 && _radius > 0f)
            {
                GenerateCircularBarrier();
            }
        }

        private void GenerateCircularBarrier()
        {
            if (_edgeCollider == null) return;

            Vector2[] points = new Vector2[_segments + 1];
            float angleStep = (2f * Mathf.PI) / _segments;

            // In Unity, EdgeCollider2D.points lavora in local space dell'oggetto.
            // Dividiamo per il lossyScale in modo che _radius rappresenti SEMPRE il raggio in WORLD UNITS!
            float scaleX = Mathf.Abs(transform.lossyScale.x);
            float scaleY = Mathf.Abs(transform.lossyScale.y);
            if (scaleX < 0.0001f) scaleX = 1f;
            if (scaleY < 0.0001f) scaleY = 1f;

            for (int i = 0; i < _segments; i++)
            {
                float a = i * angleStep;
                points[i] = new Vector2(
                    (Mathf.Cos(a) * _radius) / scaleX,
                    (Mathf.Sin(a) * _radius) / scaleY
                );
            }

            // Chiudi il cerchio ricongiungendo l'ultimo punto al primo
            points[_segments] = points[0];

            _edgeCollider.points = points;
        }

        /// <summary>
        /// Blocca rigorosamente una posizione all'interno della recinzione dell'arena.
        /// Zero allocazioni GC (AGENTS.md §4).
        /// </summary>
        public Vector2 ClampPosition(Vector2 position, float margin = 0.35f)
        {
            Vector2 center = (Vector2)transform.position;
            Vector2 offset = position - center;
            float maxR = Mathf.Max(0.1f, _radius - margin);
            if (offset.sqrMagnitude > maxR * maxR)
            {
                return center + offset.normalized * maxR;
            }
            return position;
        }

        /// <summary>
        /// Verifica se una data posizione è all'interno della recinzione.
        /// </summary>
        public bool IsInside(Vector2 position, float margin = 0f)
        {
            Vector2 center = (Vector2)transform.position;
            Vector2 offset = position - center;
            float maxR = Mathf.Max(0.1f, _radius - margin);
            return offset.sqrMagnitude <= maxR * maxR;
        }

        private void OnDrawGizmos()
        {
            // Disegna il confine dell'arena in ciano fluo nella vista Scene di Unity
            Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.85f);
            float step = (2f * Mathf.PI) / 64;
            Vector3 center = transform.position;
            Vector3 prev = center + new Vector3(Mathf.Cos(0) * _radius, Mathf.Sin(0) * _radius, 0f);

            for (int i = 1; i <= 64; i++)
            {
                float a = i * step;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * _radius, Mathf.Sin(a) * _radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
