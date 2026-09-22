using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using MobileGame.Core;
using MobileGame.Audio;

namespace MobileGame.UI
{
    /// <summary>
    /// Gestisce il menu di pausa e il pulsante di pausa in-game:
    /// - Pulsante di pausa in alto a destra, posizionato sotto la scritta WAVE.
    /// - Schermata modale di pausa con stile Cyber-Arcade a 60 FPS (zero GC alloc).
    /// - Pulsanti "RESUME" e "MAIN MENU".
    /// - Supporto New Input System (Escape da tastiera per test rapido).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }

        public bool IsPaused => _isPaused;

        private bool _isPaused = false;

        // UI References
        private GameObject _pausePanel;
        private Button     _pauseButton;
        private Button     _resumeButton;
        private Button     _mainMenuButton;
        private TMP_Text   _statsSnippetText;
        private TMP_FontAsset _arcadeFont;

        private readonly StringBuilder _sb = new StringBuilder(64);

        // Palette Colori Cyber Neon
        private static readonly Color LED_VIOLET_NEON = new Color(0.92f, 0.28f, 1.00f, 1.00f);
        private static readonly Color LED_VIOLET_TEXT = new Color(0.98f, 0.80f, 1.00f, 1.00f);
        private static readonly Color LED_CYAN_NEON   = new Color(0.00f, 0.92f, 1.00f, 1.00f);
        private static readonly Color LED_AMBER_SCORE = new Color(1.00f, 0.82f, 0.12f, 1.00f);

        // Auto-inizializzazione runtime se il componente non è già presente nella scena
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var pauseObj = new GameObject("PauseMenu");
                pauseObj.transform.SetParent(canvas.transform, false);

                var rt = pauseObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                pauseObj.AddComponent<PauseMenu>();
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

            _arcadeFont = GetArcadeFont();
            BuildUI();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += HandleGameStarted;
                GameManager.Instance.OnGameOver    += HandleGameOver;

                bool isPlaying = GameManager.Instance.CurrentState == GameState.Playing;
                SetPauseButtonVisible(isPlaying);
            }
            else
            {
                SetPauseButtonVisible(false);
            }

            if (_pausePanel != null) _pausePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= HandleGameStarted;
                GameManager.Instance.OnGameOver    -= HandleGameOver;
            }
        }

        private void Update()
        {
            // Supporto New Input System tasto Escape (zero GC)
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_isPaused)
                {
                    ResumeGame();
                }
                else if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Playing)
                {
                    PauseGame();
                }
            }
        }

        private void HandleGameStarted()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            if (_pausePanel != null) _pausePanel.SetActive(false);
            SetPauseButtonVisible(true);
        }

        private void HandleGameOver()
        {
            _isPaused = false;
            if (_pausePanel != null) _pausePanel.SetActive(false);
            SetPauseButtonVisible(false);
        }

        public void PauseGame()
        {
            if (_isPaused) return;
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing) return;
            if (Time.timeScale <= 0.0001f) return; // Non sovrapporre se in pausa Level Up

            _isPaused = true;
            AudioManager.Instance?.PlayButtonClick();
            FloatingJoystick.Instance?.Hide();
            Time.timeScale = 0f;

            UpdateStatsSnippet();
            if (_pausePanel != null) _pausePanel.SetActive(true);
            SetPauseButtonVisible(false);
        }

        public void ResumeGame()
        {
            if (!_isPaused) return;
            _isPaused = false;
            AudioManager.Instance?.PlayButtonClick();
            Time.timeScale = 1f;

            if (_pausePanel != null) _pausePanel.SetActive(false);
            SetPauseButtonVisible(true);
        }

        public void ReturnToMainMenu()
        {
            _isPaused = false;
            Time.timeScale = 1f;
            AudioManager.Instance?.PlayButtonClick();

            if (_pausePanel != null) _pausePanel.SetActive(false);
            SetPauseButtonVisible(false);

            GameManager.Instance?.ReturnToMainMenu();
        }

        public void SetPauseButtonVisible(bool visible)
        {
            if (_pauseButton != null)
            {
                _pauseButton.gameObject.SetActive(visible);
            }
        }

        public void ClosePauseMenuInstant()
        {
            if (_isPaused)
            {
                _isPaused = false;
                Time.timeScale = 1f;
            }
            if (_pausePanel != null) _pausePanel.SetActive(false);
            SetPauseButtonVisible(false);
        }

        private void UpdateStatsSnippet()
        {
            if (_statsSnippetText == null) return;
            _sb.Clear();
            int wave = GameManager.Instance != null ? GameManager.Instance.Wave : 1;
            int score = GameManager.Instance != null ? GameManager.Instance.Score : 0;
            float time = SurvivalTimer.Instance != null ? SurvivalTimer.Instance.ElapsedTime : 0f;
            int mins = (int)(time / 60f);
            int secs = (int)(time % 60f);

            _sb.Append("WAVE ").Append(wave)
               .Append("   SCORE: ").Append(score)
               .Append("   TIME: ");
            if (mins < 10) _sb.Append('0');
            _sb.Append(mins).Append(':');
            if (secs < 10) _sb.Append('0');
            _sb.Append(secs);

            _statsSnippetText.SetText(_sb);
        }

        // ── Costruzione Grafica UI ─────────────────────────────────────────────

        private void BuildUI()
        {
            // 1. Pulsante di Pausa in alto a destra, posizionato sotto la scritta WAVE
            var btnGo = new GameObject("PauseHUDButton");
            btnGo.transform.SetParent(transform, false);

            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(1f, 1f);
            btnRt.anchorMax = new Vector2(1f, 1f);
            btnRt.pivot     = new Vector2(1f, 1f);
            // La wave in HUDController ha anchoredPosition = (-40, -48) e sizeDelta = (320, 65) -> bottom a -113f.
            // Posizioniamo il tasto pausa subito sotto a -125f:
            btnRt.anchoredPosition = new Vector2(-40f, -125f);
            btnRt.sizeDelta        = new Vector2(170f, 62f);

            var btnImg = btnGo.AddComponent<Image>();
            Sprite buttonFrame = Resources.Load<Sprite>("Sprites/ui_button_frame");
            if (buttonFrame != null)
            {
                btnImg.sprite = buttonFrame;
                btnImg.color  = LED_CYAN_NEON;
            }
            else
            {
                btnImg.color = new Color(0.04f, 0.10f, 0.20f, 0.85f);
            }
            btnImg.raycastTarget = true;

            _pauseButton = btnGo.AddComponent<Button>();
            _pauseButton.targetGraphic = btnImg;
            var colors = _pauseButton.colors;
            colors.normalColor      = LED_CYAN_NEON;
            colors.highlightedColor = new Color(0.40f, 1.00f, 1.00f, 1.00f);
            colors.pressedColor     = new Color(0.00f, 0.60f, 0.80f, 1.00f);
            colors.selectedColor    = LED_CYAN_NEON;
            _pauseButton.colors     = colors;
            _pauseButton.onClick.AddListener(PauseGame);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "|| PAUSE";
            tmp.fontSize = 14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (_arcadeFont != null) tmp.font = _arcadeFont;
            tmp.raycastTarget = false;

            // 2. Schermata di Pausa (Overlay modale)
            _pausePanel = new GameObject("PauseScreenPanel");
            _pausePanel.transform.SetParent(transform, false);
            _pausePanel.transform.SetAsLastSibling();

            var panelRt = _pausePanel.AddComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.sizeDelta = Vector2.zero;

            var bgImg = _pausePanel.AddComponent<Image>();
            bgImg.color = new Color(0.01f, 0.02f, 0.04f, 0.88f);
            bgImg.raycastTarget = true; // Blocca i tocchi al gioco sottostante

            // Card Centrale Arcade
            var cardGo = new GameObject("PauseCard");
            cardGo.transform.SetParent(_pausePanel.transform, false);

            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot     = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(860f, 960f);

            var cardImg = cardGo.AddComponent<Image>();
            Sprite panelBg = Resources.Load<Sprite>("Sprites/gameover_panel_bg");
            if (panelBg != null)
            {
                cardImg.sprite = panelBg;
                cardImg.color  = Color.white;
                cardImg.preserveAspect = false;
            }
            else
            {
                cardImg.color = new Color(0.04f, 0.07f, 0.14f, 0.98f);
            }
            cardImg.raycastTarget = false;

            // Titolo PAUSED
            var titleGo = new GameObject("PauseTitleText");
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.5f);
            titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.pivot     = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 260f);
            titleRt.sizeDelta        = new Vector2(760f, 70f);

            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "GAME PAUSED";
            titleTmp.fontSize = 38f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = LED_AMBER_SCORE;
            if (_arcadeFont != null) titleTmp.font = _arcadeFont;
            titleTmp.raycastTarget = false;

            // Sottotitolo
            var subGo = new GameObject("PauseSubtitleText");
            subGo.transform.SetParent(cardGo.transform, false);
            var subRt = subGo.AddComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0.5f);
            subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.pivot     = new Vector2(0.5f, 0.5f);
            subRt.anchoredPosition = new Vector2(0f, 195f);
            subRt.sizeDelta        = new Vector2(760f, 40f);

            var subTmp = subGo.AddComponent<TextMeshProUGUI>();
            subTmp.text = "-- SYSTEM SUSPENDED --";
            subTmp.fontSize = 13f;
            subTmp.alignment = TextAlignmentOptions.Center;
            subTmp.color = new Color(0.55f, 0.72f, 0.84f, 1.0f);
            if (_arcadeFont != null) subTmp.font = _arcadeFont;
            subTmp.raycastTarget = false;

            // Statistiche correnti durante la pausa
            var statsGo = new GameObject("PauseStatsSnippet");
            statsGo.transform.SetParent(cardGo.transform, false);
            var statsRt = statsGo.AddComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(0.5f, 0.5f);
            statsRt.anchorMax = new Vector2(0.5f, 0.5f);
            statsRt.pivot     = new Vector2(0.5f, 0.5f);
            statsRt.anchoredPosition = new Vector2(0f, 115f);
            statsRt.sizeDelta        = new Vector2(760f, 50f);

            _statsSnippetText = statsGo.AddComponent<TextMeshProUGUI>();
            _statsSnippetText.fontSize = 15f;
            _statsSnippetText.fontStyle = FontStyles.Bold;
            _statsSnippetText.alignment = TextAlignmentOptions.Center;
            _statsSnippetText.color = LED_CYAN_NEON;
            if (_arcadeFont != null) _statsSnippetText.font = _arcadeFont;
            _statsSnippetText.raycastTarget = false;

            // Pulsante 1: RESUME
            _resumeButton = CreateNeonButton(cardGo.transform, "ResumeButton", new Vector2(0f, -50f), "RESUME", ResumeGame);

            // Pulsante 2: MAIN MENU
            _mainMenuButton = CreateNeonButton(cardGo.transform, "MainMenuButton", new Vector2(0f, -200f), "MAIN MENU", ReturnToMainMenu);

            _pausePanel.SetActive(false);
        }

        private Button CreateNeonButton(Transform parent, string name, Vector2 pos, string label, UnityEngine.Events.UnityAction onClick)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);

            var rt = btnGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta        = new Vector2(560f, 130f);

            var img = btnGo.AddComponent<Image>();
            Sprite buttonFrame = Resources.Load<Sprite>("Sprites/ui_button_frame");
            if (buttonFrame != null)
            {
                img.sprite = buttonFrame;
                img.color  = LED_VIOLET_NEON;
            }
            else
            {
                img.color = LED_VIOLET_NEON;
            }
            img.raycastTarget = true;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor      = LED_VIOLET_NEON;
            colors.highlightedColor = new Color(1.0f, 0.65f, 1.0f, 1.0f);
            colors.pressedColor     = new Color(0.60f, 0.15f, 0.80f, 1.0f);
            colors.selectedColor    = LED_VIOLET_NEON;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = LED_VIOLET_TEXT;
            tmp.alignment = TextAlignmentOptions.Center;
            if (_arcadeFont != null) tmp.font = _arcadeFont;
            tmp.raycastTarget = false;

            return btn;
        }

        private TMP_FontAsset GetArcadeFont()
        {
            if (_arcadeFont != null) return _arcadeFont;

            TMP_FontAsset[] allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < allFonts.Length; i++)
            {
                var f = allFonts[i];
                if (f != null && f.name.IndexOf("PressStart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _arcadeFont = f;
                    return _arcadeFont;
                }
            }

            _arcadeFont = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");
            return _arcadeFont;
        }
    }
}
