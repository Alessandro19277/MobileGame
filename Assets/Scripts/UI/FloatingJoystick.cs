using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using MobileGame.Core;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace MobileGame.UI
{
    /// <summary>
    /// Joystick virtuale dinamico "a scomparsa" (Floating Joystick):
    /// - Compare esattamente nel punto in cui il giocatore tocca lo schermo con il dito.
    /// - Segue il trascinamento fornendo un vettore direzionale normalizzato (InputVector).
    /// - Sparisce all'istante non appena il dito viene sollevato.
    /// - Grafica neon cyber generata proceduralmente a runtime (Zero file .meta, AGENTS.md §1).
    /// - Zero allocazioni heap nel loop di aggiornamento (AGENTS.md §4).
    /// </summary>
    public class FloatingJoystick : MonoBehaviour
    {
        public static FloatingJoystick Instance { get; private set; }

        [Header("Parametri Joystick")]
        [SerializeField] private float _maxRadius = 100f;
        [SerializeField] private float _deadZone  = 8f;

        // Riferimenti gerarchia UI
        private RectTransform _canvasRect;
        private RectTransform _baseRect;
        private RectTransform _knobRect;
        private CanvasGroup   _canvasGroup;
        private Image         _baseImage;
        private Image         _knobImage;

        // Stato input (zero GC)
        private Vector2 _inputVector;
        private Vector2 _startBaseCanvasPos;
        private int     _activeFingerId = -1;
        private bool    _isMouseActive  = false;
        private bool    _isActive       = false;

        public Vector2 InputVector => _inputVector;
        public bool    IsActive    => _isActive;

        // Auto-inizializzazione runtime se il componente non è già presente nella scena
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var joystickObj = new GameObject("FloatingJoystick");
                joystickObj.transform.SetParent(canvas.transform, false);
                joystickObj.transform.SetSiblingIndex(1); // Dietro alla HUD superiore

                var rt = joystickObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                joystickObj.AddComponent<FloatingJoystick>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnhancedTouchSupport.Enable();

            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _canvasRect = canvas.GetComponent<RectTransform>();
            }

            BuildVisualHierarchy();
            Hide();
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += Hide;
                GameManager.Instance.OnGameOver    += Hide;
            }
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= Hide;
                GameManager.Instance.OnGameOver    -= Hide;
            }
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildVisualHierarchy()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable   = false;
            _canvasGroup.alpha          = 0f;

            // 1. Base Ring (cerchio esterno con bordo luminoso neon ciano)
            var baseObj = new GameObject("JoystickBase");
            baseObj.transform.SetParent(transform, false);

            _baseRect = baseObj.AddComponent<RectTransform>();
            _baseRect.sizeDelta = new Vector2(220f, 220f);
            _baseRect.anchorMin = new Vector2(0.5f, 0.5f);
            _baseRect.anchorMax = new Vector2(0.5f, 0.5f);
            _baseRect.pivot     = new Vector2(0.5f, 0.5f);

            _baseImage = baseObj.AddComponent<Image>();
            _baseImage.sprite        = CreateBaseSprite(128);
            _baseImage.raycastTarget = false;

            // 2. Knob Stick (pomello interno luminoso)
            var knobObj = new GameObject("JoystickKnob");
            knobObj.transform.SetParent(baseObj.transform, false);

            _knobRect = knobObj.AddComponent<RectTransform>();
            _knobRect.sizeDelta = new Vector2(90f, 90f);
            _knobRect.anchorMin = new Vector2(0.5f, 0.5f);
            _knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            _knobRect.pivot     = new Vector2(0.5f, 0.5f);
            _knobRect.anchoredPosition = Vector2.zero;

            _knobImage = knobObj.AddComponent<Image>();
            _knobImage.sprite        = CreateKnobSprite(128);
            _knobImage.raycastTarget = false;
        }

        private void Update()
        {
            // Se la partita non è in corso (menu, game over, pause menu, level up pause), joystick inattivo
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                if (_isActive) Hide();
                return;
            }

            if (Time.timeScale <= 0.0001f)
            {
                if (_isActive) Hide();
                return;
            }

            if (!_isActive)
            {
                PollForActivation();
            }
            else
            {
                TrackActiveInput();
            }
        }

        private void PollForActivation()
        {
            // 1. Check touch input
            var touches = Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (t.phase == TouchPhase.Began)
                {
                    if (IsPointerOverBlockingUI(t.touchId))
                        continue;

                    StartTrackingTouch(t.touchId, t.screenPosition);
                    return;
                }
            }

            // 2. Check mouse input (Editor / PC fallback)
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                if (IsPointerOverBlockingUI(-1))
                    return;

                StartTrackingMouse(mouse.position.ReadValue());
            }
        }

        private void TrackActiveInput()
        {
            if (_activeFingerId != -1)
            {
                var touches = Touch.activeTouches;
                bool fingerFound = false;

                for (int i = 0; i < touches.Count; i++)
                {
                    var t = touches[i];
                    if (t.touchId == _activeFingerId)
                    {
                        fingerFound = true;
                        if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        {
                            Hide();
                        }
                        else
                        {
                            UpdateTracking(t.screenPosition);
                        }
                        break;
                    }
                }

                if (!fingerFound)
                {
                    Hide();
                }
            }
            else if (_isMouseActive)
            {
                var mouse = Mouse.current;
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    Hide();
                }
                else
                {
                    UpdateTracking(mouse.position.ReadValue());
                }
            }
        }

        private void StartTrackingTouch(int fingerId, Vector2 screenPos)
        {
            _activeFingerId = fingerId;
            _isMouseActive  = false;
            ActivateAt(screenPos);
        }

        private void StartTrackingMouse(Vector2 screenPos)
        {
            _activeFingerId = -1;
            _isMouseActive  = true;
            ActivateAt(screenPos);
        }

        private Canvas _parentCanvas;

        private Camera GetEventCamera()
        {
            if (_parentCanvas == null) _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return _parentCanvas.worldCamera;
            }
            return null;
        }

        private void ActivateAt(Vector2 screenPos)
        {
            if (_canvasRect == null)
            {
                if (_parentCanvas == null) _parentCanvas = GetComponentInParent<Canvas>();
                if (_parentCanvas != null) _canvasRect = _parentCanvas.GetComponent<RectTransform>();
                if (_canvasRect == null) return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, GetEventCamera(), out Vector2 localPoint))
            {
                // Clamp della base nei limiti dello schermo per non tagliare il joystick ai bordi
                float halfW = _canvasRect.rect.width * 0.5f;
                float halfH = _canvasRect.rect.height * 0.5f;
                float pad   = 110f;
                localPoint.x = Mathf.Clamp(localPoint.x, -halfW + pad, halfW - pad);
                localPoint.y = Mathf.Clamp(localPoint.y, -halfH + pad, halfH - pad);

                _baseRect.anchoredPosition = localPoint;
                _knobRect.anchoredPosition = Vector2.zero;
                _startBaseCanvasPos        = localPoint;

                _canvasGroup.alpha = 1f;
                _isActive          = true;
                _inputVector       = Vector2.zero;
            }
        }

        private void UpdateTracking(Vector2 screenPos)
        {
            if (_canvasRect == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, GetEventCamera(), out Vector2 currentLocalPos))
            {
                Vector2 delta = currentLocalPos - _startBaseCanvasPos;
                float distance = delta.magnitude;

                if (distance > _maxRadius)
                {
                    _knobRect.anchoredPosition = delta.normalized * _maxRadius;
                }
                else
                {
                    _knobRect.anchoredPosition = delta;
                }

                if (distance > _deadZone)
                {
                    float norm = Mathf.Clamp01((distance - _deadZone) / (_maxRadius - _deadZone));
                    _inputVector = delta.normalized * norm;
                }
                else
                {
                    _inputVector = Vector2.zero;
                }
            }
        }

        public void Hide()
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;

            if (_knobRect != null)
                _knobRect.anchoredPosition = Vector2.zero;

            _inputVector    = Vector2.zero;
            _isActive       = false;
            _activeFingerId = -1;
            _isMouseActive  = false;
        }

        private bool IsPointerOverBlockingUI(int fingerId)
        {
            if (EventSystem.current == null) return false;
            if (fingerId >= 0)
                return EventSystem.current.IsPointerOverGameObject(fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }

        // ── Generazione Grafica Procedurale Neon (Zero .meta) ─────────────────

        private static Sprite CreateBaseSprite(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            Color[] pixels  = new Color[resolution * resolution];
            float center    = resolution * 0.5f;
            float invRadius = 1f / center;

            Color ringCyan  = new Color(0.0f, 0.95f, 1.0f, 0.85f);
            Color ringGlow  = new Color(0.0f, 0.50f, 1.0f, 0.40f);
            Color innerDark = new Color(0.02f, 0.06f, 0.14f, 0.45f);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy) * invRadius;

                    Color col = Color.clear;
                    if (r <= 1f)
                    {
                        float edgeFalloff = Mathf.Clamp01((1f - r) / 0.06f);

                        if (r >= 0.70f)
                        {
                            // Anello neon con glow interno ed esterno
                            float ringT = Mathf.Sin((r - 0.70f) / 0.30f * Mathf.PI);
                            Color ring = Color.Lerp(ringGlow, ringCyan, ringT);
                            col = Color.Lerp(innerDark, ring, ringT);
                            col.a *= edgeFalloff;
                        }
                        else
                        {
                            // Sfondo interno semitrasparente
                            col = innerDark;
                        }
                    }
                    pixels[y * resolution + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateKnobSprite(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            Color[] pixels  = new Color[resolution * resolution];
            float center    = resolution * 0.5f;
            float invRadius = 1f / center;

            Color coreWhite = new Color(0.90f, 0.98f, 1.00f, 1.0f);
            Color glowCyan  = new Color(0.00f, 0.85f, 1.00f, 0.95f);
            Color rimDeep   = new Color(0.00f, 0.40f, 0.90f, 0.80f);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy) * invRadius;

                    Color col = Color.clear;
                    if (r <= 1f)
                    {
                        float edgeFalloff = Mathf.Clamp01((1f - r) / 0.08f);
                        Color knobColor;
                        if (r < 0.45f)
                        {
                            knobColor = Color.Lerp(coreWhite, glowCyan, r / 0.45f);
                        }
                        else
                        {
                            knobColor = Color.Lerp(glowCyan, rimDeep, (r - 0.45f) / 0.55f);
                        }
                        knobColor.a *= edgeFalloff;
                        col = knobColor;
                    }
                    pixels[y * resolution + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }
    }
}
