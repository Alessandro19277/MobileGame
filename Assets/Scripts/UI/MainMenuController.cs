using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MobileGame.Audio;
using MobileGame.Core;

namespace MobileGame.UI
{
    /// <summary>
    /// Gestisce il Menu Iniziale e la schermata della Classifica (Leaderboard).
    /// Si attiva all'avvio e permette di consultare i record prima di iniziare a giocare.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Pannelli")]
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _leaderboardPanel;

        [Header("Pulsanti")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _leaderboardButton;
        [SerializeField] private Button _closeLeaderboardButton;

        [Header("Profilo Pilota / Collegamento Email")]
        [SerializeField] private Button            _profileButton;
        [SerializeField] private TMP_Text          _profileButtonText;
        [SerializeField] private UserProfileDialog _profileDialog;

        [Header("Crediti & Info")]
        [SerializeField] private CreditsDialog     _creditsDialog;
        [SerializeField] private Button            _creditsButton;

        [Header("Testi")]
        [SerializeField] private TMP_Text _highScoreText;
        [SerializeField] private TMP_Text _leaderboardContentText;

        private readonly StringBuilder _sb = new StringBuilder(512);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_mainMenuPanel == null)
                _mainMenuPanel = gameObject;

            // Disattiva da codice il Raycast Target e il Word Wrapping per non andare mai a capo
            if (_leaderboardContentText != null)
            {
                _leaderboardContentText.raycastTarget = false;
                _leaderboardContentText.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        private void Start()
        {
            if (_playButton != null)
                _playButton.onClick.AddListener(OnPlayClicked);

            if (_leaderboardButton != null)
                _leaderboardButton.onClick.AddListener(OnOpenLeaderboardClicked);

            if (_closeLeaderboardButton != null)
                _closeLeaderboardButton.onClick.AddListener(OnCloseLeaderboardClicked);

            // Inizializza pulsante profilo / email se non agganciato da inspector
            InitProfileButtonAndDialog();

            // Inizializza pulsante circolare dei crediti e relativo popup
            InitCreditsButtonAndDialog();

            // Adatta layout e proporzioni per schermi smartphone allungati (20:9, 19.5:9, ecc.)
            AdaptLayoutForScreen();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += OnGameStarted;

                // Mostra il menu se siamo in MainMenu
                bool isMainMenu = GameManager.Instance.CurrentState == GameState.MainMenu;
                SetMainMenuVisible(isMainMenu);
            }
            else
            {
                SetMainMenuVisible(true);
            }

            if (_leaderboardPanel != null)
                _leaderboardPanel.SetActive(false);

            if (LeaderboardManager.Instance != null)
                LeaderboardManager.Instance.OnLeaderboardUpdated += OnLeaderboardUpdated;

            RefreshHighScore();
            RefreshProfileButtonText();
        }

        private void OnDestroy()
        {
            if (_playButton != null)
                _playButton.onClick.RemoveListener(OnPlayClicked);

            if (_leaderboardButton != null)
                _leaderboardButton.onClick.RemoveListener(OnOpenLeaderboardClicked);

            if (_closeLeaderboardButton != null)
                _closeLeaderboardButton.onClick.RemoveListener(OnCloseLeaderboardClicked);

            if (_profileButton != null)
                _profileButton.onClick.RemoveListener(OnOpenProfileClicked);

            if (_profileDialog != null)
                _profileDialog.OnDialogClosed -= OnCloseProfileClicked;

            if (_creditsButton != null)
                _creditsButton.onClick.RemoveListener(OnOpenCreditsClicked);

            if (_creditsDialog != null)
                _creditsDialog.OnDialogClosed -= OnCloseCreditsClicked;

            if (UserProfileManager.Instance != null)
                UserProfileManager.Instance.OnProfileChanged -= RefreshProfileButtonText;

            if (LeaderboardManager.Instance != null)
                LeaderboardManager.Instance.OnLeaderboardUpdated -= OnLeaderboardUpdated;

            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStarted -= OnGameStarted;
        }

        private void OnLeaderboardUpdated()
        {
            RefreshHighScore();
            if (_leaderboardPanel != null && _leaderboardPanel.activeSelf)
            {
                PopulateLeaderboard();
            }
        }

        private void Update()
        {
            // Se _playButton non è ancora stato assegnato in Inspector,
            // un tocco sullo schermo o click del mouse avvia comunque la partita!
            if (_playButton == null && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                {
                    OnPlayClicked();
                }
                else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    OnPlayClicked();
                }
            }
        }

        // ── UI Actions ─────────────────────────────────────────────────────────

        public void SetMainMenuVisible(bool visible)
        {
            if (_mainMenuPanel != null)
                _mainMenuPanel.SetActive(visible);

            SetMenuContentVisible(visible);

            if (visible)
            {
                RefreshHighScore();
                RefreshProfileButtonText();
                if (_leaderboardPanel != null)
                    _leaderboardPanel.SetActive(false);
            }
            else
            {
                if (_creditsDialog != null)
                    _creditsDialog.gameObject.SetActive(false);
            }
        }

        private void RefreshHighScore()
        {
            if (_highScoreText == null) return;

            int hi = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.GetHighScore() : 0;
            _sb.Clear();
            _sb.Append("RECORD: ").Append(hi);
            _highScoreText.SetText(_sb);
        }

        private void PopulateLeaderboard()
        {
            if (_leaderboardContentText == null) return;

            _sb.Clear();

            var entries = LeaderboardManager.Instance != null
                ? LeaderboardManager.Instance.GetTopEntries()
                : null;

            if (entries == null || entries.Count == 0)
            {
                _sb.AppendLine();
                _sb.AppendLine("<color=#66FCF1>NESSUN RECORD REGISTRATO</color>");
                _sb.AppendLine();
                _sb.AppendLine("GIOCA UNA PARTITA");
                _sb.AppendLine("PER ENTRARE IN CLASSIFICA!");
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    string medal = i switch
                    {
                        0 => "<color=#FFD700>#1</color>",
                        1 => "<color=#E0E0E0>#2</color>",
                        2 => "<color=#CD7F32>#3</color>",
                        _ => $"#{i + 1}"
                    };

                    string tag = string.IsNullOrEmpty(entry.initials) ? "AAA" : entry.initials;
                    var profile = UserProfileManager.Instance;
                    bool isCurrentPilot = profile != null && profile.IsLinked 
                        && string.Equals(tag, profile.Initials, System.StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrEmpty(entry.email)
                        && string.Equals(entry.email, profile.Email, System.StringComparison.OrdinalIgnoreCase);

                    string tagColor = isCurrentPilot ? "#00FF66" : "#00FFFF";
                    string pilotPrefix = isCurrentPilot ? "★ " : "";

                    _sb.Append(medal).Append(" <color=").Append(tagColor).Append(">")
                       .Append(pilotPrefix).Append(tag).Append("</color> ")
                       .Append(entry.score).Append(" | W").Append(entry.wave)
                       .Append(" | ").Append(entry.formattedTime)
                       .AppendLine();
                }
            }

            _leaderboardContentText.SetText(_sb);
        }

        // ── Profilo Pilota / Collegamento Email ─────────────────────────────────

        private void InitProfileButtonAndDialog()
        {
            if (_profileButton == null && _leaderboardButton != null)
            {
                var parent = _leaderboardButton.transform.parent;
                var cloneGo = Instantiate(_leaderboardButton.gameObject, parent);
                cloneGo.name = "ProfileButton";

                var cloneRt = cloneGo.GetComponent<RectTransform>();
                if (cloneRt != null)
                {
                    cloneRt.anchoredPosition = new Vector2(0f, -800f);
                }

                _profileButton = cloneGo.GetComponent<Button>();
                _profileButtonText = cloneGo.GetComponentInChildren<TMP_Text>();
                if (_profileButtonText != null)
                {
                    _profileButtonText.fontSize = 29f;
                    _profileButtonText.alignment = TextAlignmentOptions.Center;
                    _profileButtonText.textWrappingMode = TextWrappingModes.NoWrap;
                    _profileButtonText.enableAutoSizing = true;
                    _profileButtonText.fontSizeMin = 20f;
                    _profileButtonText.fontSizeMax = 30f;

                    // Margine orizzontale di sicurezza per non toccare i bordi del frame
                    var textRt = _profileButtonText.GetComponent<RectTransform>();
                    if (textRt != null)
                    {
                        textRt.offsetMin = new Vector2(35f, 0f);
                        textRt.offsetMax = new Vector2(-35f, 0f);
                    }
                }
            }

            if (_profileButton != null)
            {
                _profileButton.onClick.RemoveListener(OnOpenProfileClicked);
                _profileButton.onClick.AddListener(OnOpenProfileClicked);
            }

            if (UserProfileManager.Instance != null)
            {
                UserProfileManager.Instance.OnProfileChanged -= RefreshProfileButtonText;
                UserProfileManager.Instance.OnProfileChanged += RefreshProfileButtonText;
            }

            if (_profileDialog == null)
            {
                _profileDialog = FindAnyObjectByType<UserProfileDialog>(FindObjectsInactive.Include);
                if (_profileDialog == null)
                {
                    Transform canvasTransform = transform.parent != null ? transform.parent : transform;
                    TMP_FontAsset font = _leaderboardContentText != null ? _leaderboardContentText.font : null;
                    _profileDialog = UserProfileDialog.CreateDialog(canvasTransform, font);
                }
            }

            if (_profileDialog != null)
            {
                _profileDialog.OnDialogClosed -= OnCloseProfileClicked;
                _profileDialog.OnDialogClosed += OnCloseProfileClicked;
            }

            RefreshProfileButtonText();
        }

        public void RefreshProfileButtonText()
        {
            if (_profileButtonText == null) return;

            var profile = UserProfileManager.Instance;
            if (profile != null && profile.IsLinked)
            {
                _profileButtonText.SetText($"<color=#00E676>PILOTA: {profile.Initials}</color>");
            }
            else
            {
                _profileButtonText.SetText("<color=#00FFFF>COLLEGA EMAIL</color>");
            }

            _profileButtonText.alignment = TextAlignmentOptions.Center;
            _profileButtonText.enableAutoSizing = true;
            _profileButtonText.fontSizeMin = 20f;
            _profileButtonText.fontSizeMax = 30f;
        }

        public void OnOpenProfileClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            if (_profileDialog != null)
            {
                _profileDialog.Open();
            }
            SetMenuContentVisible(false);
        }

        public void OnCloseProfileClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            SetMenuContentVisible(true);
            RefreshProfileButtonText();
            RefreshHighScore();
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        public void OnPlayClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            SetMainMenuVisible(false);
            GameManager.Instance?.StartGame();
        }

        public void OnOpenLeaderboardClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            if (_leaderboardPanel != null)
            {
                _leaderboardPanel.SetActive(true);
                PopulateLeaderboard();
                LeaderboardManager.Instance?.RefreshCloudScores();
            }

            // Nasconde lo sfondo del menu e i tasti per non sovrapporli alla classifica
            SetMenuContentVisible(false);
        }

        public void OnCloseLeaderboardClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            if (_leaderboardPanel != null)
                _leaderboardPanel.SetActive(false);

            // Riattiva lo sfondo del menu e i tasti
            SetMenuContentVisible(true);
            RefreshHighScore();
        }

        // ── Crediti & Info Dialog ──────────────────────────────────────────────

        private void InitCreditsButtonAndDialog()
        {
            if (_creditsButton == null)
            {
                var existingBtn = transform.Find("CreditsIconButton");
                if (existingBtn != null)
                {
                    _creditsButton = existingBtn.GetComponent<Button>();
                }
                else
                {
                    // Icona tonda semitrasparente in basso a sinistra dello schermo
                    var iconGo = new GameObject("CreditsIconButton");
                    iconGo.transform.SetParent(transform, false);

                    var rt = iconGo.AddComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 0f);
                    rt.pivot     = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(90f, 90f);
                    rt.sizeDelta        = new Vector2(90f, 90f);

                    var img = iconGo.AddComponent<Image>();
                    img.sprite = CreditsDialog.CreateCircleIconSprite(128);
                    img.color  = Color.white;
                    img.raycastTarget = true;

                    _creditsButton = iconGo.AddComponent<Button>();
                    _creditsButton.targetGraphic = img;

                    var colors = _creditsButton.colors;
                    colors.normalColor      = Color.white;
                    colors.highlightedColor = new Color(0.8f, 1.0f, 1.0f, 1.0f);
                    colors.pressedColor     = new Color(0.4f, 0.8f, 1.0f, 0.8f);
                    colors.selectedColor    = Color.white;
                    _creditsButton.colors = colors;

                    // Lettera "i" all'interno
                    var textGo = new GameObject("Text");
                    textGo.transform.SetParent(iconGo.transform, false);

                    var textRt = textGo.AddComponent<RectTransform>();
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.sizeDelta = Vector2.zero;

                    var tmp = textGo.AddComponent<TextMeshProUGUI>();
                    tmp.text = "<i>i</i>";
                    tmp.fontSize = 44f;
                    tmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.color = new Color(0.00f, 0.95f, 1.00f, 0.95f);
                    TMP_FontAsset font = _leaderboardContentText != null ? _leaderboardContentText.font : null;
                    if (font != null) tmp.font = font;
                    tmp.raycastTarget = false;
                }
            }

            if (_creditsButton != null)
            {
                _creditsButton.onClick.RemoveListener(OnOpenCreditsClicked);
                _creditsButton.onClick.AddListener(OnOpenCreditsClicked);
            }

            if (_creditsDialog == null)
            {
                _creditsDialog = FindAnyObjectByType<CreditsDialog>(FindObjectsInactive.Include);
                if (_creditsDialog == null)
                {
                    Transform canvasTransform = transform.parent != null ? transform.parent : transform;
                    TMP_FontAsset font = _leaderboardContentText != null ? _leaderboardContentText.font : null;
                    _creditsDialog = CreditsDialog.CreateDialog(canvasTransform, font);
                }
            }

            if (_creditsDialog != null)
            {
                _creditsDialog.OnDialogClosed -= OnCloseCreditsClicked;
                _creditsDialog.OnDialogClosed += OnCloseCreditsClicked;
            }
        }

        public void OnOpenCreditsClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            if (_creditsDialog != null)
            {
                _creditsDialog.Open();
            }
            // Il menu principale rimane visibile sotto il popup modale semitrasparente
        }

        public void OnCloseCreditsClicked()
        {
            SetMenuContentVisible(true);
            RefreshProfileButtonText();
            RefreshHighScore();
        }

        private void SetMenuContentVisible(bool visible)
        {
            if (_mainMenuPanel != null && _mainMenuPanel != gameObject)
            {
                _mainMenuPanel.SetActive(visible);
                return;
            }

            // Se questo script è sul GameObject MainMenu, attiviamo/disattiviamo i figli
            // (sfondo, pulsanti, titoli) per non sovrapporli ma mantenere vivo lo script!
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var child = transform.GetChild(i);
                if (_leaderboardPanel != null && child.gameObject == _leaderboardPanel)
                    continue;
                if (_profileDialog != null && child.gameObject == _profileDialog.gameObject)
                    continue;
                if (_creditsDialog != null && child.gameObject == _creditsDialog.gameObject)
                    continue;

                child.gameObject.SetActive(visible);
            }
        }

        private void AdaptLayoutForScreen()
        {
            // 1. Preserva le proporzioni 1:1 originali della Cover (zero deformazioni / stretching)
            Transform bgTrans = transform.Find("BackgroundCover");
            if (bgTrans != null)
            {
                var fitter = bgTrans.GetComponent<AspectRatioFitter>();
                if (fitter == null) fitter = bgTrans.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectRatio = 768f / 1376f; // Ratio nativo di main_menu_cover.jpg (0.558)
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }

            // 2. Posizionamento garantito dei pulsanti con spazio di sicurezza dalle bande nere:
            // Play a -460, Classifica a -630, Profilo/Mail a -800.
            // Con altezza 1920 (bordo inferiore a -960), il tasto mail ha 85px interi di margine
            // sopra la banda nera e non viene MAI coperto!
            if (_playButton != null)
            {
                var rt = _playButton.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(0f, -460f);
            }

            if (_leaderboardButton != null)
            {
                var rt = _leaderboardButton.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(0f, -630f);
            }

            if (_profileButton != null)
            {
                var rt = _profileButton.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(0f, -800f);
            }

            // 3. Preserva le proporzioni anche per lo sfondo della Leaderboard
            if (_leaderboardPanel != null)
            {
                var lbFitter = _leaderboardPanel.GetComponent<AspectRatioFitter>();
                if (lbFitter == null) lbFitter = _leaderboardPanel.AddComponent<AspectRatioFitter>();
                lbFitter.aspectRatio = 768f / 1376f;
                lbFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            }
        }

        private void OnGameStarted()
        {
            SetMainMenuVisible(false);
        }
    }
}
