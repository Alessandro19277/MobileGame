using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using MobileGame.Core;
using MobileGame.UI;

namespace MobileGame.Player
{
    /// <summary>
    /// Muove il player (Rigidbody2D) tramite joystick virtuale fluttuante (o fallback tastiera).
    /// Singleton per permettere agli upgrade di modificare la velocità.
    /// Zero allocazioni heap in FixedUpdate (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movimento")]
        [SerializeField] private float _speed = 4.2f;

        // Pre-cachati in Awake (AGENTS.md §4)
        private Rigidbody2D     _rb;
        private HealthComponent _health;
        private SpriteRenderer  _spriteRenderer;
        private PlayerDeathFX   _deathFX;

        private Vector2 _moveInput;
        private bool    _isDying;

        // Debuff rallentamento (zero GC)
        private float _slowTimer;
        private float _slowMultiplier = 1f;
        private static readonly Color SLOW_FROST_TINT = new Color(0.45f, 0.85f, 1f);
        private static readonly Color NORMAL_TINT      = Color.white;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _rb             = GetComponent<Rigidbody2D>();
            _health         = GetComponent<HealthComponent>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            if (GetComponent<PlayerHealthBar>() == null)
                gameObject.AddComponent<PlayerHealthBar>();

            if (GetComponent<PlayerTrail>() == null)
                gameObject.AddComponent<PlayerTrail>();

            if (GetComponent<PlayerDeathFX>() == null)
                gameObject.AddComponent<PlayerDeathFX>();

            _deathFX = GetComponent<PlayerDeathFX>();

            if (_health != null)
                _health.OnDeath += OnPlayerDeath;

            EnhancedTouchSupport.Enable();
            _moveInput = Vector2.zero;

            // Scala proporzionata rispetto all'arena (piu' compatto per visuale piu' ampia)
            transform.localScale = new Vector3(0.46f, 0.46f, 1f);
            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.radius = 0.42f;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += HandleGameStarted;
                GameManager.Instance.OnGameOver    += HandleGameOver;

                // Se siamo nel menu iniziale, nasconde lo sprite del player dal mondo di gioco
                bool isPlaying = GameManager.Instance.CurrentState == GameState.Playing;
                if (_spriteRenderer != null) _spriteRenderer.enabled = isPlaying;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_health != null)
                _health.OnDeath -= OnPlayerDeath;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= HandleGameStarted;
                GameManager.Instance.OnGameOver    -= HandleGameOver;
            }
        }

        private void HandleGameStarted()
        {
            _isDying = false;

            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.enabled = true;

            var shooter = GetComponent<PlayerShooter>();
            if (shooter != null) shooter.enabled = true;

            var hpBar = GetComponent<PlayerHealthBar>();
            if (hpBar != null) hpBar.SetVisible(true);

            _deathFX?.ResetDeathFX();

            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = true;
                _spriteRenderer.color   = NORMAL_TINT;
            }
            _slowTimer      = 0f;
            _slowMultiplier = 1f;
            _rb.position    = Vector2.zero;
            _moveInput      = Vector2.zero;
            GetComponent<PlayerTrail>()?.ClearTrail();
        }

        private void HandleGameOver()
        {
            if (_spriteRenderer != null) _spriteRenderer.enabled = false;
            GetComponent<PlayerTrail>()?.ClearTrail();
        }

        private void OnPlayerDeath()
        {
            if (_isDying) return;
            _isDying   = true;
            _moveInput = Vector2.zero;

            // Ferma fisica e disabilita collider per prevenire ulteriori collisioni
            _rb.linearVelocity = Vector2.zero;
            var col = GetComponent<CircleCollider2D>();
            if (col != null) col.enabled = false;

            // Disattiva shooter e nasconde barra vita
            var shooter = GetComponent<PlayerShooter>();
            if (shooter != null) shooter.enabled = false;

            var hpBar = GetComponent<PlayerHealthBar>();
            if (hpBar != null) hpBar.SetVisible(false);

            // Avvia la sequenza cinematografica di morte (Proposta 1: Cyber-Detonation)
            if (_deathFX != null)
            {
                _deathFX.PlayDeathSequence(() =>
                {
                    _isDying = false;
                    GameManager.Instance?.TriggerGameOver();
                });
            }
            else
            {
                _isDying = false;
                GameManager.Instance?.TriggerGameOver();
            }
        }

        private void OnEnable()  => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        // ── Input sampling & Status effects ────────────────────────────────────

        private void Update()
        {
            if (_isDying || GameManager.Instance?.CurrentState != GameState.Playing || Time.timeScale <= 0.0001f)
            {
                _moveInput = Vector2.zero;
                return;
            }

            // Gestione timer rallentamento criogenico (zero GC)
            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.deltaTime;
                if (_slowTimer <= 0f)
                {
                    _slowTimer      = 0f;
                    _slowMultiplier = 1f;
                    if (_spriteRenderer != null) _spriteRenderer.color = NORMAL_TINT;
                }
            }

            // Lettura joystick virtuale a scomparsa (touch su mobile o mouse in Editor)
            Vector2 joyInput = Vector2.zero;
            if (FloatingJoystick.Instance != null && FloatingJoystick.Instance.IsActive)
            {
                joyInput = FloatingJoystick.Instance.InputVector;
            }

            if (joyInput.sqrMagnitude > 0.0001f)
            {
                _moveInput = joyInput;
            }
            else
            {
                // Fallback da tastiera per test rapidi in Editor (WASD / Frecce con New Input System)
                Vector2 kbInput = Vector2.zero;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    kbInput.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  kbInput.y -= 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  kbInput.x -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) kbInput.x += 1f;

                    if (kbInput.sqrMagnitude > 1f)
                        kbInput.Normalize();
                }
                _moveInput = kbInput;
            }
        }

        // ── Fisica ─────────────────────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (_isDying || _moveInput.sqrMagnitude < 0.0001f)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            float currentSpeed = _speed * _slowMultiplier;
            Vector2 next = _rb.position + _moveInput * (currentSpeed * Time.fixedDeltaTime);

            // Clamp rigoroso all'interno della recinzione dell'arena (zero GC)
            if (ArenaBoundary.Instance != null)
            {
                next = ArenaBoundary.Instance.ClampPosition(next, 0.35f);
            }

            _rb.MovePosition(next);
        }

        // ── API status effects ─────────────────────────────────────────────────

        /// <summary>
        /// Applica un debuff di congelamento / rallentamento temporaneo con tinta ciano (zero GC).
        /// </summary>
        public void ApplySlow(float slowPercent, float duration)
        {
            _slowTimer      = Mathf.Max(_slowTimer, duration);
            _slowMultiplier = Mathf.Min(_slowMultiplier, Mathf.Clamp01(1f - slowPercent));
            if (_spriteRenderer != null) _spriteRenderer.color = SLOW_FROST_TINT;
            MobileGame.Utilities.HapticManager.MediumImpact();
        }

        // ── API upgrade ────────────────────────────────────────────────────────

        /// <summary>Aumenta la velocità del player di una percentuale (es. 0.25 = +25%).</summary>
        public void ApplySpeedBonus(float multiplier)
            => _speed *= (1f + multiplier);

        /// <summary>Aumenta il raggio magnetico delle gemme XP.</summary>
        public void ApplyMagnetBonus(float multiplier)
        {
            if (XPManager.Instance != null)
                XPManager.Instance.MagnetMultiplier *= (1f + multiplier);
        }
    }
}
