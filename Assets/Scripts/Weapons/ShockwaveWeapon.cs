using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Weapons
{
    /// <summary>
    /// Arma ad onda d'urto cellulare: ogni N secondi sprigiona una pulsazione a 360°
    /// che danneggia tutti i microbi nell'area ed espande un anello visivo al plasma.
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    public class ShockwaveWeapon : MonoBehaviour
    {
        public static ShockwaveWeapon Instance { get; private set; }

        [Header("Layer Nemici")]
        [SerializeField] private LayerMask _enemyLayerMask;

        [Header("Statistiche Base")]
        [SerializeField] private float _baseRadius   = 3.8f;
        [SerializeField] private float _baseCooldown = 3.0f;
        [SerializeField] private float _baseDamage   = 28f;

        public int  Level      { get; private set; } = 0; // 0 = non ancora sbloccata
        public int  MaxLevel   => 4;
        public bool IsUnlocked => Level > 0;

        private float _timer;
        private ContactFilter2D _enemyFilter;
        private readonly Collider2D[] _overlapBuffer = new Collider2D[32]; // pre-allocato, zero GC

        // Anello visivo generato automaticamente (zero asset esterni richiesti)
        private Transform    _visualRing;
        private LineRenderer _lineRenderer;
        private bool         _isVisualActive;
        private float        _visualTimer;
        private float        _visualMaxRadius;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _enemyFilter.SetLayerMask(_enemyLayerMask);
            _enemyFilter.useTriggers = true;

            CreateAutoVisualRing();
        }

        private void CreateAutoVisualRing()
        {
            var ringGO = new GameObject("ShockwavePulseEffect");
            ringGO.transform.SetParent(transform, false);

            _lineRenderer = ringGO.AddComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.loop = true;
            _lineRenderer.positionCount = 32;
            _lineRenderer.startWidth = 0.15f;
            _lineRenderer.endWidth = 0.15f;

            // Materiale Sprite di default (visibile senza asset custom)
            var mat = new Material(Shader.Find("Sprites/Default"));
            _lineRenderer.material = mat;

            float angleStep = (2f * Mathf.PI) / 32;
            for (int i = 0; i < 32; i++)
            {
                float a = i * angleStep;
                _lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
            }

            _visualRing = ringGO.transform;
            _visualRing.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!IsUnlocked) return;
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            // Timer ricarica onda
            _timer += Time.deltaTime;
            float currentCooldown = Mathf.Max(1.2f, _baseCooldown - (Level - 1) * 0.4f);

            if (_timer >= currentCooldown)
            {
                _timer = 0f;
                TriggerPulse();
            }

            // Animazione espansione ad anello luminoso (zero alloc)
            if (_isVisualActive && _visualRing != null)
            {
                _visualTimer += Time.deltaTime * 3.5f; // dura ~0.28s
                if (_visualTimer >= 1f)
                {
                    _isVisualActive = false;
                    _visualRing.gameObject.SetActive(false);
                }
                else
                {
                    float currentRadius = Mathf.Lerp(0.3f, _visualMaxRadius, _visualTimer);
                    _visualRing.localScale = new Vector3(currentRadius, currentRadius, 1f);

                    if (_lineRenderer != null)
                    {
                        float alpha = Mathf.Lerp(0.9f, 0f, _visualTimer);
                        Color c = new Color(0.2f, 0.9f, 1f, alpha);
                        _lineRenderer.startColor = c;
                        _lineRenderer.endColor = c;
                    }
                }
            }
        }

        // ── Impulso ────────────────────────────────────────────────────────────

        private void TriggerPulse()
        {
            float currentRadius = _baseRadius + (Level - 1) * 0.6f;
            float currentDamage = _baseDamage + (Level - 1) * 14f;

            // Physics2D.OverlapCircle con buffer pre-allocato: ZERO GC (AGENTS.md §4)
            int count = Physics2D.OverlapCircle(
                transform.position, currentRadius, _enemyFilter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i] == null) continue;
                _overlapBuffer[i].GetComponent<HealthComponent>()?.TakeDamage(currentDamage);
                MobileGame.UI.DamageNumberManager.Spawn(_overlapBuffer[i].transform.position, Mathf.RoundToInt(currentDamage), false);
            }

            // Attiva l'anello visivo ad onda espansiva
            if (_visualRing != null)
            {
                _visualMaxRadius = currentRadius;
                _visualTimer     = 0f;
                _isVisualActive  = true;
                _visualRing.localScale = Vector3.zero;
                _visualRing.gameObject.SetActive(true);
            }
        }

        // ── Gestione Livelli & Upgrade ─────────────────────────────────────────

        public void UnlockOrUpgrade()
        {
            if (Level >= MaxLevel) return;
            Level++;
        }
    }
}
