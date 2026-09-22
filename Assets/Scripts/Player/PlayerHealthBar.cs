using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Player
{
    /// <summary>
    /// Barra della vita fluttuante sopra la testa del Player.
    /// Verde sopra il 30%, diventa rossa sotto il 30%.
    /// Zero allocazioni heap in loop (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(HealthComponent))]
    public class PlayerHealthBar : MonoBehaviour
    {
        [Header("Posizionamento")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0.70f, 0f);
        [SerializeField] private float   _barWidth  = 0.65f;
        [SerializeField] private float   _barHeight = 0.10f;
        [SerializeField] private float   _borderThickness = 0.025f;

        [Header("Colori")]
        [SerializeField] private Color _healthyColor   = new Color(0.0f, 0.90f, 0.46f); // Verde neon
        [SerializeField] private Color _criticalColor  = new Color(1.0f, 0.20f, 0.20f); // Rosso neon
        [SerializeField] private Color _bgColor        = new Color(0.05f, 0.08f, 0.12f, 0.85f); // Sfondo scuro

        private HealthComponent _health;
        private GameObject      _container;
        private SpriteRenderer  _bgRenderer;
        private SpriteRenderer  _fillRenderer;
        private Transform       _fillTransform;

        private float _maxFillWidth;
        private float _baseBarWidth;
        private float _baseMaxHealth = 100f;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _baseBarWidth = _barWidth;
            _health = GetComponent<HealthComponent>();
            if (_health != null)
                _baseMaxHealth = _health.MaxHealth;

            CreateHealthBarVisuals();
        }

        private void Start()
        {
            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += OnGameStarted;
                GameManager.Instance.OnGameOver    += OnGameOver;

                bool isPlaying = GameManager.Instance.CurrentState == GameState.Playing;
                SetVisible(isPlaying);
            }
            else
            {
                SetVisible(true);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.OnHealthChanged -= HandleHealthChanged;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= OnGameStarted;
                GameManager.Instance.OnGameOver    -= OnGameOver;
            }
        }

        // ── Creazione Grafica Procedurale (Zero dipendenze esterne) ────────────

        private void CreateHealthBarVisuals()
        {
            _container = new GameObject("OverheadHealthBar");
            _container.transform.SetParent(transform);
            _container.transform.localPosition = _offset;
            _container.transform.localRotation = Quaternion.identity;

            // Sprite 1x1 bianco per rendering pixel-perfect
            Sprite centerSprite = CreateSolidSprite(new Vector2(0.5f, 0.5f));
            Sprite leftPivotSprite = CreateSolidSprite(new Vector2(0f, 0.5f));

            // Sfondo della barra (Dark Background)
            var bgObj = new GameObject("HealthBar_BG");
            bgObj.transform.SetParent(_container.transform);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(_barWidth, _barHeight, 1f);

            _bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            _bgRenderer.sprite = centerSprite;
            _bgRenderer.color = _bgColor;
            _bgRenderer.sortingOrder = 95;

            // Barra di riempimento (Fill)
            _maxFillWidth = _barWidth - (_borderThickness * 2f);
            float fillHeight = _barHeight - (_borderThickness * 2f);

            var fillObj = new GameObject("HealthBar_Fill");
            fillObj.transform.SetParent(_container.transform);
            // Pivot a sinistra: parte dal bordo sinistro interno
            fillObj.transform.localPosition = new Vector3(-_maxFillWidth / 2f, 0f, 0f);
            fillObj.transform.localScale = new Vector3(_maxFillWidth, fillHeight, 1f);

            _fillTransform = fillObj.transform;
            _fillRenderer  = fillObj.AddComponent<SpriteRenderer>();
            _fillRenderer.sprite = leftPivotSprite;
            _fillRenderer.color = _healthyColor;
            _fillRenderer.sortingOrder = 96;
        }

        private static Sprite CreateSolidSprite(Vector2 pivot)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            Color[] colors = new Color[] { Color.white, Color.white, Color.white, Color.white };
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), pivot, 2f);
        }

        // ── Aggiornamento Salute (Event-driven, Zero GC) ───────────────────────

        private void HandleHealthChanged(float current, float max)
        {
            if (_fillTransform == null || _fillRenderer == null || _bgRenderer == null) return;

            // Se la salute massima aumenta (potenziamenti), ingrandisce la larghezza della barra
            float widthRatio = (_baseMaxHealth > 0f && max > 0f) ? (max / _baseMaxHealth) : 1f;
            widthRatio = Mathf.Clamp(widthRatio, 1.0f, 2.5f); // Limita l'allargamento per estetica
            float currentBarWidth = _baseBarWidth * widthRatio;

            // Aggiorna dimensioni dello sfondo
            _bgRenderer.transform.localScale = new Vector3(currentBarWidth, _barHeight, 1f);

            // Ricalcola la larghezza del riempimento e riposiziona il pivot a sinistra
            _maxFillWidth = currentBarWidth - (_borderThickness * 2f);
            float fillHeight = _barHeight - (_borderThickness * 2f);
            _fillTransform.localPosition = new Vector3(-_maxFillWidth / 2f, 0f, 0f);

            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            // Scala orizzontale in base al rapporto di vita
            Vector3 scale = _fillTransform.localScale;
            scale.x = _maxFillWidth * ratio;
            scale.y = fillHeight;
            _fillTransform.localScale = scale;

            // Verde sopra il 30%, rosso sotto il 30%
            _fillRenderer.color = ratio > 0.30f ? _healthyColor : _criticalColor;
        }

        private void OnGameStarted()
        {
            SetVisible(true);
            if (_health != null)
                HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        }
        private void OnGameOver()    => SetVisible(false);

        public void SetVisible(bool visible)
        {
            if (_container != null)
                _container.SetActive(visible);
        }
    }
}
