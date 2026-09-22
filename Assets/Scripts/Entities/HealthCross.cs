using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Entities
{
    /// <summary>
    /// Croce verde curativa stile arcade: compare raramente alla morte dei nemici.
    /// Ripristina una piccola quantità di salute (es. 15 HP) quando raccolta.
    /// Ha attrazione magnetica e animazione ad impulso pulsante.
    /// Zero allocazioni heap in loop (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class HealthCross : MonoBehaviour
    {
        [SerializeField] private float _baseMagnetRange  = 3.5f;
        [SerializeField] private float _magnetSpeed      = 9.0f;
        [SerializeField] private float _targetWorldSize  = 0.45f; // Dimensione arcade ben visibile a schermo

        private float                      _healAmount = 15f;
        private System.Action<HealthCross> _onCollected;
        private Transform                  _playerTransform;
        private bool                       _attracted;
        private CircleCollider2D           _collider;
        private SpriteRenderer             _renderer;

        private float _baseScale = 0.14f;
        private static Sprite _cachedCrossSprite;
        private static Material s_unlitMaterial;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _collider = GetComponent<CircleCollider2D>();
            if (_collider == null) _collider = gameObject.AddComponent<CircleCollider2D>();
            _collider.isTrigger = true;

            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            if (s_unlitMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    s_unlitMaterial = new Material(shader);
                }
            }
            if (s_unlitMaterial != null)
            {
                _renderer.sharedMaterial = s_unlitMaterial;
            }

            if (_cachedCrossSprite == null)
            {
                var multiSprites = Resources.LoadAll<Sprite>("Sprites/health_cross");
                if (multiSprites != null && multiSprites.Length > 0)
                {
                    _cachedCrossSprite = multiSprites[0];
                }

                if (_cachedCrossSprite == null)
                    _cachedCrossSprite = Resources.Load<Sprite>("Sprites/health_cross");

                if (_cachedCrossSprite == null)
                    _cachedCrossSprite = CreateArcadeCrossSprite();
            }

            _renderer.sprite = _cachedCrossSprite;
            _renderer.sortingOrder = 22; // Sopra ai nemici e gemme XP

            RecalculateScale();
        }

        private void Start()
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) _playerTransform = p.transform;
            RecalculateScale();
        }

        private void OnEnable()
        {
            if (_playerTransform == null)
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) _playerTransform = p.transform;
            }
            RecalculateScale();
        }

        private void RecalculateScale()
        {
            if (_renderer != null && _renderer.sprite != null && _renderer.sprite.bounds.size.x > 0.001f)
            {
                _baseScale = _targetWorldSize / _renderer.sprite.bounds.size.x;
            }
            else
            {
                _baseScale = 0.07f; // Fallback per texture 512x512
            }

            if (_collider != null)
            {
                _collider.radius = (_targetWorldSize * 0.55f) / Mathf.Max(0.001f, _baseScale);
            }

            transform.localScale = new Vector3(_baseScale, _baseScale, 1f);
        }

        // ── Inizializzazione Pool ──────────────────────────────────────────────

        public void Initialize(float healAmount, System.Action<HealthCross> onCollected)
        {
            _healAmount  = healAmount;
            _onCollected = onCollected;
            _attracted   = false;
            RecalculateScale();
        }

        // ── Loop di Gioco ──────────────────────────────────────────────────────

        private void Update()
        {
            // Effetto pulsazione vivida stile arcade (±14%): zero allocazioni (AGENTS.md §4)
            float pulse = _baseScale * (1.0f + Mathf.Sin(Time.time * 7f) * 0.14f);
            transform.localScale = new Vector3(pulse, pulse, 1f);

            if (_playerTransform == null) return;

            float range = _baseMagnetRange * (XPManager.Instance != null ? XPManager.Instance.MagnetMultiplier : 1f);
            Vector2 toPlayer = (Vector2)_playerTransform.position - (Vector2)transform.position;

            if (!_attracted && toPlayer.sqrMagnitude <= range * range)
                _attracted = true;

            if (_attracted)
            {
                transform.position = Vector2.MoveTowards(
                    transform.position,
                    _playerTransform.position,
                    _magnetSpeed * Time.deltaTime);
            }
        }

        // ── Raccolta & Cura ────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            var health = other.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.Heal(_healAmount);
                UI.DamageNumberManager.SpawnHeal(transform.position, (int)_healAmount);
                Utilities.HapticManager.LightImpact();
                Audio.AudioManager.Instance?.PlayHeal();
            }

            _onCollected?.Invoke(this);
        }

        // ── Creazione Sprite Croce Arcade Procedurale ─────────────────────────

        private static Sprite CreateArcadeCrossSprite()
        {
            const int size = 12;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            Color outline = new Color(0.01f, 0.18f, 0.08f, 0.95f); // Bordo scuro smeraldo
            Color neon    = new Color(0.00f, 0.95f, 0.46f, 1.00f); // Verde neon brillante (#00E676)
            Color core    = new Color(0.75f, 1.00f, 0.88f, 1.00f); // Nucleo menta luminoso

            Color[] pixels = new Color[size * size];

            // Matrice 12x12: forma della croce (4-7 centrale)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isCrossArm = (x >= 4 && x <= 7 && y >= 1 && y <= 10) ||
                                      (y >= 4 && y <= 7 && x >= 1 && x <= 10);

                    bool isBorder = isCrossArm && (
                        x == 1 || x == 10 || y == 1 || y == 10 ||
                        (x == 4 && (y < 4 || y > 7)) ||
                        (x == 7 && (y < 4 || y > 7)) ||
                        (y == 4 && (x < 4 || x > 7)) ||
                        (y == 7 && (x < 4 || x > 7))
                    );

                    bool isCore = (x == 5 || x == 6) && (y == 5 || y == 6);

                    if (isBorder)
                        pixels[y * size + x] = outline;
                    else if (isCore)
                        pixels[y * size + x] = core;
                    else if (isCrossArm)
                        pixels[y * size + x] = neon;
                    else
                        pixels[y * size + x] = Color.clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 18f);
        }
    }
}
