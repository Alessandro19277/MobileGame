using UnityEngine;

namespace MobileGame.Player
{
    /// <summary>
    /// Genera e controlla una scia al plasma/motore ionico (TrailRenderer) dietro al nanobot.
    /// Si assottiglia e sfuma naturalmente quando il giocatore si muove.
    /// Zero allocazioni heap nel loop di aggiornamento (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerTrail : MonoBehaviour
    {
        private TrailRenderer _trail;
        private Rigidbody2D   _rb;
        private Vector2       _lastPosition;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _lastPosition = _rb != null ? _rb.position : (Vector2)transform.position;

            // Inizializzazione o recupero del TrailRenderer
            _trail = GetComponent<TrailRenderer>();
            if (_trail == null)
            {
                _trail = gameObject.AddComponent<TrailRenderer>();
            }

            ConfigureTrail();
        }

        private void ConfigureTrail()
        {
            _trail.time = 0.28f;
            _trail.minVertexDistance = 0.08f;
            _trail.sortingOrder = -1; // Sotto lo sprite del nano-bot

            // Curva di larghezza: da 0.40 (motore nano-bot) a 0.0 (estremità scia)
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 0.40f);
            widthCurve.AddKey(1f, 0.0f);
            _trail.widthCurve = widthCurve;

            // Gradiente ciano neon brillante -> blu profondo trasparente
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.0f, 0.95f, 1.0f), 0.0f),   // Ciano elettrico
                    new GradientColorKey(new Color(0.0f, 0.35f, 1.0f), 0.65f),  // Blu ionico
                    new GradientColorKey(new Color(0.1f, 0.05f, 0.4f), 1.0f)   // Scuro
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.75f, 0.0f),
                    new GradientAlphaKey(0.40f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            _trail.colorGradient = gradient;

            // Shader compatibile URP/Builtin
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null)
            {
                _trail.material = new Material(shader);
            }
        }

        private void Update()
        {
            if (_trail == null || _rb == null) return;

            // Rileva se il giocatore è fermo o in movimento per modulare l'emissione
            Vector2 currentPos = _rb.position;
            float sqrDist = (currentPos - _lastPosition).sqrMagnitude;
            _lastPosition = currentPos;

            // Se fermo, non emette nuova scia
            _trail.emitting = sqrDist > 0.0001f;
        }

        public void ClearTrail()
        {
            if (_trail != null)
            {
                _trail.Clear();
            }
        }
    }
}
