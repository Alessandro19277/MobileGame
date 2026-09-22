using UnityEngine;
using TMPro;

namespace MobileGame.UI
{
    /// <summary>
    /// Numero di danno fluttuante con ascesa, scale punch e dissolvenza.
    /// Componente poolato in World Space a zero allocazioni GC nei frame di gioco (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageNumber : MonoBehaviour
    {
        private TextMeshPro _tmp;
        private float       _timer;
        private float       _duration = 0.42f;
        private Vector2     _velocity;
        private Vector3     _baseScale;
        private bool        _isCrit;
        private System.Action<DamageNumber> _onComplete;

        private static readonly Color NORMAL_COLOR = new Color(1f, 1f, 1f, 1f);
        private static readonly Color CRIT_COLOR   = new Color(1f, 0.88f, 0.1f, 1f); // Oro brillante
        private static readonly Color HEAL_COLOR   = new Color(0.00f, 0.95f, 0.46f, 1f); // Verde neon cura (#00E676)
        private static readonly string HEAL_FORMAT = "+{0} HP";

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _tmp.alignment   = TextAlignmentOptions.Center;
            _tmp.sortingOrder = 10; // Sopra a sprite nemici ed arena
            _tmp.outlineWidth = 0.28f;
            _tmp.outlineColor = Color.black;
            _baseScale       = transform.localScale;
        }

        /// <summary>
        /// Inizializza il popup numerico con la quantita di danno inflitto e stato critico.
        /// Zero GC: usa SetText("{0}", damage) senza allocare stringhe (AGENTS.md §4).
        /// </summary>
        public void Initialize(Vector2 worldPos, int damage, bool isCrit, System.Action<DamageNumber> onComplete)
        {
            transform.position = worldPos + (Vector2)(Random.insideUnitCircle * 0.15f);
            _isCrit     = isCrit;
            _onComplete = onComplete;
            _timer      = 0f;

            // Velocita ascesa e leggera dispersione orizzontale
            _velocity = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(1.8f, 2.5f));

            if (_tmp != null)
            {
                _tmp.color     = isCrit ? CRIT_COLOR : NORMAL_COLOR;
                _tmp.fontSize  = isCrit ? 4.8f : 3.6f;
                _tmp.fontStyle = isCrit ? FontStyles.Bold : FontStyles.Normal;
                _tmp.alpha     = 1f;
                _tmp.SetText("{0}", damage); // Zero alloc string formatting in TMP
            }

            transform.localScale = _baseScale * (isCrit ? 1.35f : 1.0f);
        }

        /// <summary>
        /// Inizializza il popup numerico con la quantita di salute ripristinata in verde neon brillante.
        /// Zero GC: usa SetText(HEAL_FORMAT, healAmount) senza allocare stringhe (AGENTS.md §4).
        /// </summary>
        public void InitializeHeal(Vector2 worldPos, int healAmount, System.Action<DamageNumber> onComplete)
        {
            transform.position = worldPos + (Vector2)(Random.insideUnitCircle * 0.12f);
            _isCrit     = false;
            _onComplete = onComplete;
            _timer      = 0f;

            _velocity = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(2.0f, 2.6f));

            if (_tmp != null)
            {
                _tmp.color     = HEAL_COLOR;
                _tmp.fontSize  = 4.2f;
                _tmp.fontStyle = FontStyles.Bold;
                _tmp.alpha     = 1f;
                _tmp.SetText(HEAL_FORMAT, healAmount);
            }

            transform.localScale = _baseScale * 1.25f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            float t = Mathf.Clamp01(_timer / _duration);

            // Movimento verso l alto con attrito decrescente
            transform.position += (Vector3)(_velocity * Time.deltaTime);
            _velocity = Vector2.Lerp(_velocity, Vector2.zero, Time.deltaTime * 3.5f);

            // Scale punch iniziale e contrazione
            float scaleMod;
            if (t < 0.25f)
            {
                scaleMod = Mathf.Lerp(0.6f, 1.25f, t / 0.25f);
            }
            else
            {
                scaleMod = Mathf.Lerp(1.25f, 0.9f, (t - 0.25f) / 0.75f);
            }

            transform.localScale = _baseScale * ((_isCrit ? 1.35f : 1.0f) * scaleMod);

            // Dissolvenza alpha nella seconda meta del tempo
            if (t > 0.45f && _tmp != null)
            {
                float alphaT = (t - 0.45f) / 0.55f;
                _tmp.alpha = Mathf.Lerp(1f, 0f, alphaT);
            }

            if (_timer >= _duration)
            {
                var cb = _onComplete;
                _onComplete = null;
                cb?.Invoke(this);
            }
        }
    }
}
