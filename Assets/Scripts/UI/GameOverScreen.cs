using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MobileGame.Audio;
using MobileGame.Core;

namespace MobileGame.UI
{
    /// <summary>
    /// Schermata di Game Over con cornice artistica Cyber-Panel (gemella del Leaderboard Panel):
    /// Bordo metallico con circuiti, tubi neon ciano brillanti, schermo a griglia digitale
    /// con telemetria missione in caratteri fosforo (zero emoji o caratteri speciali non supportati).
    /// Zero allocazioni heap a runtime (AGENTS.md §4).
    /// </summary>
    public class GameOverScreen : MonoBehaviour
    {
        [Header("Riferimenti UI")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text   _titleText;
        [SerializeField] private TMP_Text   _finalScoreText;
        [SerializeField] private TMP_Text   _survivalTimeText;
        [SerializeField] private TMP_Text   _highScoreBadgeText;
        [SerializeField] private Button     _retryButton;
        [SerializeField] private Button     _menuButton;
        [SerializeField] private Sprite     _panelBackgroundSprite;

        [Header("Inserimento Iniziali Record (Top 10)")]
        [SerializeField] private GameObject      _initialsContainer;
        [SerializeField] private TMP_InputField  _initialsInputField;
        [SerializeField] private Button          _submitInitialsButton;
        [SerializeField] private TMP_Text        _initialsPromptText;

        // Elementi Telemetria HUD
        private TMP_Text   _waveText;
        private TMP_Text   _pilotInfoText;
        private TMP_Text   _telemetryTagText;
        private GameObject _debriefCard;
        private bool       _isThemed;

        private readonly StringBuilder _sb = new StringBuilder(128);

        [SerializeField] private TMP_FontAsset _arcadeFont;

        // Palette Display Fosforo LED (zero allocazioni in loop)
        private static readonly Color LED_AMBER_SCORE    = new Color(1.00f, 0.82f, 0.12f, 1.00f);
        private static readonly Color LED_GREEN_PHOSPHOR = new Color(0.05f, 1.00f, 0.50f, 1.00f);
        private static readonly Color LED_CYAN_PHOSPHOR  = new Color(0.10f, 0.95f, 1.00f, 1.00f);
        private static readonly Color LED_VIOLET_WAVE    = new Color(0.80f, 0.58f, 1.00f, 1.00f);
        private static readonly Color LED_RED_ALERT      = new Color(1.00f, 0.25f, 0.32f, 1.00f);
        private static readonly Color LED_TEXT_MUTED     = new Color(0.55f, 0.72f, 0.84f, 1.00f);
        private static readonly Color LED_VIOLET_NEON    = new Color(0.92f, 0.28f, 1.00f, 1.00f);
        private static readonly Color LED_VIOLET_TEXT    = new Color(0.98f, 0.80f, 1.00f, 1.00f);
        private static readonly Color INPUT_TEXT_DARK_BLUE = new Color(0.06f, 0.16f, 0.38f, 1.00f); // Blu scuro profondo per le 3 lettere inserite

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Start()
        {
            ApplyCyberPanelTheme();

            if (_panel != null) _panel.SetActive(false);
            if (_initialsContainer != null) _initialsContainer.SetActive(false);

            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver += Show;

            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRetryClicked);

            if (_menuButton != null)
                _menuButton.onClick.AddListener(OnMenuClicked);

            if (_submitInitialsButton != null)
                _submitInitialsButton.onClick.AddListener(OnSubmitInitialsClicked);

            if (_initialsInputField != null)
            {
                _initialsInputField.characterLimit = 3;
                _initialsInputField.onValueChanged.AddListener(OnInitialsValueChanged);
                _initialsInputField.onSubmit.AddListener(_ => OnSubmitInitialsClicked());
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameOver -= Show;

            if (_retryButton != null)
                _retryButton.onClick.RemoveListener(OnRetryClicked);

            if (_menuButton != null)
                _menuButton.onClick.RemoveListener(OnMenuClicked);

            if (_submitInitialsButton != null)
                _submitInitialsButton.onClick.RemoveListener(OnSubmitInitialsClicked);
        }

        // ── Caricamento Immagine Pannello Cyber (gemella della Leaderboard) ───

        private Sprite GetPanelSprite()
        {
            if (_panelBackgroundSprite != null) return _panelBackgroundSprite;

            // Caricamento sicuro tramite Resources (gestito nativamente da Unity)
            var resourceSprite = Resources.Load<Sprite>("Sprites/gameover_panel_bg");
            if (resourceSprite != null) return resourceSprite;

            return null;
        }

        // ── Setup Grafico Scheda Cyber Recap ───────────────────────────────────

        private TMP_FontAsset GetArcadeFont()
        {
            if (_arcadeFont != null && _arcadeFont.name.IndexOf("PressStart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return _arcadeFont;

            // 1. Cerca in memoria tra tutti i FontAsset attualmente caricati nel runtime
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

            // 2. Cerca tra tutti i TMP_Text nella scena (es. pulsanti del menu o HUD)
            TMP_Text[] sceneTexts = FindObjectsByType<TMP_Text>();
            for (int i = 0; i < sceneTexts.Length; i++)
            {
                var t = sceneTexts[i];
                if (t != null && t.font != null && t.font.name.IndexOf("PressStart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _arcadeFont = t.font;
                    return _arcadeFont;
                }
            }

            // 3. Fallback tramite Resources
            _arcadeFont = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");
            if (_arcadeFont != null) return _arcadeFont;

            return _finalScoreText != null ? _finalScoreText.font : null;
        }

        // ── Setup Grafico Scheda Arcade Recap ──────────────────────────────────

        private void ApplyCyberPanelTheme()
        {
            if (_isThemed || _panel == null) return;
            _isThemed = true;

            TMP_FontAsset font = GetArcadeFont();
            Sprite panelSprite = GetPanelSprite();
            Sprite buttonFrameSprite = Resources.Load<Sprite>("Sprites/ui_button_frame");

            // 1. Backdrop Oscurante a tutto schermo
            var panelImg = _panel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = new Color(0.01f, 0.02f, 0.04f, 0.90f);
                panelImg.raycastTarget = true;
            }

            // 2. Card Centrale di Sfondo Arcade Pixel (posta come First Sibling)
            Transform existingCard = _panel.transform.Find("CyberRecapCard");
            if (existingCard != null)
            {
                _debriefCard = existingCard.gameObject;
            }
            else
            {
                _debriefCard = new GameObject("CyberRecapCard");
                _debriefCard.transform.SetParent(_panel.transform, false);
                _debriefCard.transform.SetAsFirstSibling();

                var cardRt = _debriefCard.AddComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot     = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.sizeDelta        = new Vector2(900, 1600); // 9:16 portrait arcade

                var cardImg = _debriefCard.AddComponent<Image>();
                if (panelSprite != null)
                {
                    cardImg.sprite = panelSprite;
                    cardImg.color  = Color.white;
                    cardImg.preserveAspect = false;
                }
                else
                {
                    cardImg.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);
                }
                cardImg.raycastTarget = false;
            }

            // 3. Header Superiore Arcade
            if (_titleText == null)
            {
                Transform existingTitle = _panel.transform.Find("RecapTitleText");
                if (existingTitle != null)
                {
                    _titleText = existingTitle.GetComponent<TMP_Text>();
                }
                else
                {
                    var titleGo = new GameObject("RecapTitleText");
                    titleGo.transform.SetParent(_panel.transform, false);
                    var titleRt = titleGo.AddComponent<RectTransform>();
                    titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                    titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                    titleRt.pivot     = new Vector2(0.5f, 0.5f);
                    titleRt.anchoredPosition = new Vector2(0, 540);
                    titleRt.sizeDelta = new Vector2(780, 60);
                    _titleText = titleGo.AddComponent<TextMeshProUGUI>();
                    _titleText.fontSize = 28;
                    _titleText.fontStyle = FontStyles.Bold;
                    _titleText.color = LED_RED_ALERT;
                    _titleText.alignment = TextAlignmentOptions.Center;
                    _titleText.raycastTarget = false;
                    if (font != null) _titleText.font = font;
                }
            }
            else
            {
                var rt = _titleText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 540);
                rt.sizeDelta = new Vector2(780, 60);
                _titleText.fontSize = 28;
                _titleText.fontStyle = FontStyles.Bold;
                _titleText.color = LED_RED_ALERT;
                _titleText.alignment = TextAlignmentOptions.Center;
                if (font != null) _titleText.font = font;
            }
            if (_titleText != null) _titleText.text = "MISSION FAILED";

            // Sotto-etichetta rapporto arcade
            Transform existingTag = _panel.transform.Find("RecapTelemetryTag");
            if (existingTag != null)
            {
                _telemetryTagText = existingTag.GetComponent<TMP_Text>();
            }
            else
            {
                var tagGo = new GameObject("RecapTelemetryTag");
                tagGo.transform.SetParent(_panel.transform, false);
                var tagRt = tagGo.AddComponent<RectTransform>();
                tagRt.anchorMin = new Vector2(0.5f, 0.5f);
                tagRt.anchorMax = new Vector2(0.5f, 0.5f);
                tagRt.pivot     = new Vector2(0.5f, 0.5f);
                tagRt.anchoredPosition = new Vector2(0, 485);
                tagRt.sizeDelta = new Vector2(720, 35);
                _telemetryTagText = tagGo.AddComponent<TextMeshProUGUI>();
                _telemetryTagText.fontSize = 14;
                _telemetryTagText.fontStyle = FontStyles.Bold;
                _telemetryTagText.color = new Color(1.0f, 0.70f, 0.20f);
                _telemetryTagText.alignment = TextAlignmentOptions.Center;
                _telemetryTagText.raycastTarget = false;
            }
            if (font != null) _telemetryTagText.font = font;
            _telemetryTagText.text = "DECONTAMINATION ABORTED";

            // 4. Badge Record Display
            if (_highScoreBadgeText == null)
            {
                Transform existingBadge = _panel.transform.Find("RecapHighScoreBadge");
                if (existingBadge != null)
                {
                    _highScoreBadgeText = existingBadge.GetComponent<TMP_Text>();
                }
                else
                {
                    var badgeGo = new GameObject("RecapHighScoreBadge");
                    badgeGo.transform.SetParent(_panel.transform, false);
                    var badgeRt = badgeGo.AddComponent<RectTransform>();
                    badgeRt.anchorMin = new Vector2(0.5f, 0.5f);
                    badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
                    badgeRt.pivot     = new Vector2(0.5f, 0.5f);
                    badgeRt.anchoredPosition = new Vector2(0, 435);
                    badgeRt.sizeDelta = new Vector2(720, 35);
                    _highScoreBadgeText = badgeGo.AddComponent<TextMeshProUGUI>();
                    _highScoreBadgeText.fontSize = 15;
                    _highScoreBadgeText.fontStyle = FontStyles.Bold;
                    _highScoreBadgeText.alignment = TextAlignmentOptions.Center;
                    _highScoreBadgeText.raycastTarget = false;
                }
            }
            else
            {
                var rt = _highScoreBadgeText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 435);
                rt.sizeDelta = new Vector2(720, 35);
                _highScoreBadgeText.fontSize = 15;
                _highScoreBadgeText.fontStyle = FontStyles.Bold;
                _highScoreBadgeText.alignment = TextAlignmentOptions.Center;
            }
            if (font != null) _highScoreBadgeText.font = font;

            // 5. Area Display Statistiche Arcade: rimossa la barra centrale scura/verde per lasciare il testo direttamente sopra il pannello
            Transform existingStatsBox = _panel.transform.Find("RecapStatsContainer");
            if (existingStatsBox != null)
            {
                existingStatsBox.gameObject.SetActive(false);
            }

            // Punteggio Finale LED (Oro Arcade)
            if (_finalScoreText != null)
            {
                var rt = _finalScoreText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 285);
                rt.sizeDelta = new Vector2(700, 130);
                _finalScoreText.fontSize = 34;
                _finalScoreText.lineSpacing = 28f; // Spazio extra tra titolo "FINAL SCORE" e punteggio
                _finalScoreText.fontStyle = FontStyles.Bold;
                _finalScoreText.color = LED_AMBER_SCORE;
                _finalScoreText.alignment = TextAlignmentOptions.Center;
                _finalScoreText.raycastTarget = false;
                if (font != null) _finalScoreText.font = font;
            }

            // Tempo di Sopravvivenza (Colonna Sinistra)
            if (_survivalTimeText == null)
            {
                Transform existingTime = _panel.transform.Find("RecapSurvivalTime");
                if (existingTime != null)
                {
                    _survivalTimeText = existingTime.GetComponent<TMP_Text>();
                }
                else
                {
                    var timeGo = new GameObject("RecapSurvivalTime");
                    timeGo.transform.SetParent(_panel.transform, false);
                    var timeRt = timeGo.AddComponent<RectTransform>();
                    timeRt.anchorMin = new Vector2(0.5f, 0.5f);
                    timeRt.anchorMax = new Vector2(0.5f, 0.5f);
                    timeRt.pivot     = new Vector2(0.5f, 0.5f);
                    timeRt.anchoredPosition = new Vector2(-180, 175);
                    timeRt.sizeDelta = new Vector2(360, 110);
                    _survivalTimeText = timeGo.AddComponent<TextMeshProUGUI>();
                    _survivalTimeText.fontSize = 22;
                    _survivalTimeText.lineSpacing = 24f; // Spazio extra tra titolo e tempo
                    _survivalTimeText.fontStyle = FontStyles.Bold;
                    _survivalTimeText.color = LED_CYAN_PHOSPHOR;
                    _survivalTimeText.alignment = TextAlignmentOptions.Center;
                    _survivalTimeText.raycastTarget = false;
                    if (font != null) _survivalTimeText.font = font;
                }
            }
            else
            {
                var rt = _survivalTimeText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-180, 175);
                rt.sizeDelta = new Vector2(360, 110);
                _survivalTimeText.fontSize = 22;
                _survivalTimeText.lineSpacing = 24f;
                _survivalTimeText.fontStyle = FontStyles.Bold;
                _survivalTimeText.color = LED_CYAN_PHOSPHOR;
                _survivalTimeText.alignment = TextAlignmentOptions.Center;
                _survivalTimeText.raycastTarget = false;
                if (font != null) _survivalTimeText.font = font;
            }

            // Ondata Raggiunta (Colonna Destra)
            Transform existingWave = _panel.transform.Find("RecapWaveText");
            if (existingWave != null)
            {
                _waveText = existingWave.GetComponent<TMP_Text>();
            }
            else
            {
                var waveGo = new GameObject("RecapWaveText");
                waveGo.transform.SetParent(_panel.transform, false);
                var waveRt = waveGo.AddComponent<RectTransform>();
                waveRt.anchorMin = new Vector2(0.5f, 0.5f);
                waveRt.anchorMax = new Vector2(0.5f, 0.5f);
                waveRt.pivot     = new Vector2(0.5f, 0.5f);
                waveRt.anchoredPosition = new Vector2(180, 175);
                waveRt.sizeDelta = new Vector2(360, 110);
                _waveText = waveGo.AddComponent<TextMeshProUGUI>();
                _waveText.fontSize = 22;
                _waveText.lineSpacing = 24f;
                _waveText.fontStyle = FontStyles.Bold;
                _waveText.color = LED_VIOLET_WAVE;
                _waveText.alignment = TextAlignmentOptions.Center;
                _waveText.raycastTarget = false;
                if (font != null) _waveText.font = font;
            }

            if (_waveText != null)
            {
                var rt = _waveText.rectTransform;
                rt.sizeDelta = new Vector2(360, 110);
                _waveText.lineSpacing = 24f;
            }

            // Pilota e Account Collegato (Centrato con email sotto)
            Transform existingPilot = _panel.transform.Find("RecapPilotText");
            if (existingPilot != null)
            {
                _pilotInfoText = existingPilot.GetComponent<TMP_Text>();
            }
            else
            {
                var pilotGo = new GameObject("RecapPilotText");
                pilotGo.transform.SetParent(_panel.transform, false);
                var pilotRt = pilotGo.AddComponent<RectTransform>();
                pilotRt.anchorMin = new Vector2(0.5f, 0.5f);
                pilotRt.anchorMax = new Vector2(0.5f, 0.5f);
                pilotRt.pivot     = new Vector2(0.5f, 0.5f);
                pilotRt.anchoredPosition = new Vector2(0, 60);
                pilotRt.sizeDelta = new Vector2(760, 95);
                _pilotInfoText = pilotGo.AddComponent<TextMeshProUGUI>();
                _pilotInfoText.fontSize = 15;
                _pilotInfoText.lineSpacing = 16f;
                _pilotInfoText.alignment = TextAlignmentOptions.Center;
                _pilotInfoText.raycastTarget = false;
                if (font != null) _pilotInfoText.font = font;
            }
            if (_pilotInfoText != null)
            {
                var rt = _pilotInfoText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0, 60);
                rt.sizeDelta = new Vector2(760, 95);
                _pilotInfoText.fontSize = 15;
                _pilotInfoText.lineSpacing = 16f;
                _pilotInfoText.alignment = TextAlignmentOptions.Center;
                if (font != null) _pilotInfoText.font = font;
            }

            // 6. Inserimento Iniziali Top 10
            if (_initialsContainer != null)
            {
                var initRt = _initialsContainer.GetComponent<RectTransform>();
                if (initRt != null)
                {
                    initRt.anchorMin = new Vector2(0.5f, 0.5f);
                    initRt.anchorMax = new Vector2(0.5f, 0.5f);
                    initRt.pivot     = new Vector2(0.5f, 0.5f);
                    initRt.anchoredPosition = new Vector2(0, -115);
                    initRt.sizeDelta = new Vector2(740, 205);
                }

                var initImg = _initialsContainer.GetComponent<Image>();
                if (initImg != null)
                {
                    initImg.enabled = false; // Rimosso sfondo: l'inserimento rimane pulito direttamente sopra il pannello arcade
                    initImg.color = Color.clear;
                    initImg.raycastTarget = false;
                }

                if (_initialsPromptText != null)
                {
                    var pRt = _initialsPromptText.rectTransform;
                    pRt.anchoredPosition = new Vector2(0, 48);
                    pRt.sizeDelta = new Vector2(700, 50);
                    _initialsPromptText.fontSize = 14;
                    _initialsPromptText.fontStyle = FontStyles.Bold;
                    _initialsPromptText.alignment = TextAlignmentOptions.Center;
                    if (font != null) _initialsPromptText.font = font;
                }

                if (_initialsInputField != null)
                {
                    var ifRt = _initialsInputField.GetComponent<RectTransform>();
                    if (ifRt != null)
                    {
                        ifRt.anchoredPosition = new Vector2(-155, -28);
                        ifRt.sizeDelta = new Vector2(230, 65);
                    }
                    if (_initialsInputField.textComponent != null)
                    {
                        _initialsInputField.textComponent.fontSize = 24;
                        _initialsInputField.textComponent.color = INPUT_TEXT_DARK_BLUE; // Colore blu scuro per le 3 lettere
                        if (font != null) _initialsInputField.textComponent.font = font;
                    }
                    if (_initialsInputField.placeholder is TMP_Text placeholderText)
                    {
                        placeholderText.color = new Color(0.15f, 0.25f, 0.45f, 0.55f);
                        if (font != null) placeholderText.font = font;
                    }
                }

                if (_submitInitialsButton != null)
                {
                    var btnRt = _submitInitialsButton.GetComponent<RectTransform>();
                    if (btnRt != null)
                    {
                        btnRt.anchoredPosition = new Vector2(145, -28);
                        btnRt.sizeDelta = new Vector2(310, 65);
                    }

                    var btnImg = _submitInitialsButton.GetComponent<Image>();
                    if (btnImg != null)
                    {
                        if (buttonFrameSprite != null)
                        {
                            btnImg.sprite = buttonFrameSprite;
                            btnImg.color  = LED_VIOLET_NEON;
                        }
                        else
                        {
                            btnImg.color = LED_VIOLET_NEON;
                        }
                    }
                    var btnTxt = _submitInitialsButton.GetComponentInChildren<TMP_Text>();
                    if (btnTxt != null)
                    {
                        if (font != null) btnTxt.font = font;
                        btnTxt.text = "SAVE";
                        btnTxt.color = LED_VIOLET_TEXT;
                        btnTxt.fontSize = 18;
                        btnTxt.fontStyle = FontStyles.Bold;
                        btnTxt.raycastTarget = false;
                    }
                }
            }

            // 7. Pulsanti Neon Arcade Inferiori (Stile Viola Neon rialzati)
            // A. TRY AGAIN
            if (_retryButton != null)
            {
                var rt = _retryButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot     = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, -370); // Posizione rialzata
                    rt.sizeDelta = new Vector2(560, 130);
                }

                var img = _retryButton.GetComponent<Image>();
                if (img != null)
                {
                    if (buttonFrameSprite != null)
                    {
                        img.sprite = buttonFrameSprite;
                        img.color  = LED_VIOLET_NEON; // Tinta viola neon identica ai LED del pannello
                    }
                    else
                    {
                        img.color = LED_VIOLET_NEON;
                    }
                    img.raycastTarget = true;
                }

                var colors = _retryButton.colors;
                colors.normalColor      = LED_VIOLET_NEON;
                colors.highlightedColor = new Color(1.0f, 0.65f, 1.0f, 1.0f);
                colors.pressedColor     = new Color(0.60f, 0.15f, 0.80f, 1.0f);
                colors.selectedColor    = LED_VIOLET_NEON;
                _retryButton.colors     = colors;

                var txt = _retryButton.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    if (font != null) txt.font = font;
                    txt.text = "TRY AGAIN";
                    txt.fontSize = 22;
                    txt.fontStyle = FontStyles.Bold;
                    txt.color = LED_VIOLET_TEXT; // Viola neon luminoso e leggibile
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.raycastTarget = false;
                }
            }

            // B. MAIN MENU
            if (_menuButton != null)
            {
                var rt = _menuButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot     = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, -515); // Posizione rialzata
                    rt.sizeDelta = new Vector2(560, 130);
                }

                var img = _menuButton.GetComponent<Image>();
                if (img != null)
                {
                    if (buttonFrameSprite != null)
                    {
                        img.sprite = buttonFrameSprite;
                        img.color  = LED_VIOLET_NEON; // Tinta viola neon identica ai LED del pannello
                    }
                    else
                    {
                        img.color = LED_VIOLET_NEON;
                    }
                    img.raycastTarget = true;
                }

                var colors = _menuButton.colors;
                colors.normalColor      = LED_VIOLET_NEON;
                colors.highlightedColor = new Color(1.0f, 0.65f, 1.0f, 1.0f);
                colors.pressedColor     = new Color(0.60f, 0.15f, 0.80f, 1.0f);
                colors.selectedColor    = LED_VIOLET_NEON;
                _menuButton.colors      = colors;

                var txt = _menuButton.GetComponentInChildren<TMP_Text>();
                if (txt != null)
                {
                    if (font != null) txt.font = font;
                    txt.text = "MAIN MENU";
                    txt.fontSize = 22;
                    txt.fontStyle = FontStyles.Bold;
                    txt.color = LED_VIOLET_TEXT; // Viola neon luminoso e leggibile
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.raycastTarget = false;
                }
            }
        }

        // ── Visualizzazione e Popolamento Dati Recap ───────────────────────────

        private void Show()
        {
            if (_arcadeFont == null || !_arcadeFont.name.Contains("PressStart"))
            {
                _isThemed = false;
            }
            ApplyCyberPanelTheme();

            if (_panel != null) _panel.SetActive(true);

            if (_titleText != null)
            {
                _titleText.SetText("MISSION FAILED");
                _titleText.color = LED_RED_ALERT;
            }

            if (GameManager.Instance != null)
            {
                // 1. Punteggio Finale Elegante (Oro & Bianco, distanziato dal titolo)
                if (_finalScoreText != null)
                {
                    _sb.Clear();
                    _sb.Append("<size=45%><color=#94A3B8>FINAL SCORE</color></size>\n<line-height=135%>");
                    _sb.Append("<color=#FFD234>").Append(GameManager.Instance.Score.ToString("N0")).Append("</color> <size=50%><color=#FBBF24>PTS</color></size></line-height>");
                    _finalScoreText.SetText(_sb);
                }

                // 2. Tempo di Sopravvivenza (Colonna Sinistra, distanziato dal titolo)
                if (_survivalTimeText != null)
                {
                    float time = GameManager.Instance.LastSurvivalTime;
                    int mins = (int)(time / 60f);
                    int secs = (int)(time % 60f);

                    _sb.Clear();
                    _sb.Append("<size=50%><color=#67E8F9>SURVIVAL TIME</color></size>\n<line-height=135%><color=#FFFFFF>");
                    if (mins < 10) _sb.Append('0');
                    _sb.Append(mins).Append(':');
                    if (secs < 10) _sb.Append('0');
                    _sb.Append(secs).Append("</color></line-height>");
                    _survivalTimeText.SetText(_sb);
                }

                // 3. Ondata Raggiunta (Colonna Destra, distanziata dal titolo)
                if (_waveText != null)
                {
                    _sb.Clear();
                    _sb.Append("<size=50%><color=#C084FC>WAVE REACHED</color></size>\n<line-height=135%><color=#FFFFFF>WAVE ").Append(GameManager.Instance.Wave).Append("</color></line-height>");
                    _waveText.SetText(_sb);
                }

                // 4. Info Pilota & Account (Centrato con email subito sotto e più grande)
                var profile = UserProfileManager.Instance;
                bool isLinked = profile != null && profile.IsLinked;
                if (_pilotInfoText != null)
                {
                    _sb.Clear();
                    if (isLinked)
                    {
                        _sb.Append("<size=115%><color=#94A3B8>PILOT:</color> <color=#00FF9D><b>").Append(profile.Initials).Append("</b></color></size>");
                        if (!string.IsNullOrEmpty(profile.Email))
                        {
                            _sb.Append("\n<size=95%><color=#CBD5E1>(").Append(profile.Email).Append(")</color></size>");
                        }
                    }
                    else
                    {
                        _sb.Append("<size=115%><color=#94A3B8>PILOT:</color> <color=#00FF9D><b>GUEST</b></color></size>\n<size=90%><color=#94A3B8>(NOT LINKED)</color></size>");
                    }
                    _pilotInfoText.SetText(_sb);
                }

                // 5. Badge Record Display
                if (_highScoreBadgeText != null)
                {
                    if (GameManager.Instance.IsNewHighScore)
                    {
                        _highScoreBadgeText.gameObject.SetActive(true);
                        _highScoreBadgeText.SetText("NEW ALL-TIME RECORD!");
                        _highScoreBadgeText.color = LED_AMBER_SCORE;
                    }
                    else if (GameManager.Instance.QualifiesForLeaderboard)
                    {
                        _highScoreBadgeText.gameObject.SetActive(true);
                        _highScoreBadgeText.SetText("TOP 10 QUALIFIED!");
                        _highScoreBadgeText.color = LED_CYAN_PHOSPHOR;
                    }
                    else
                    {
                        int hi = LeaderboardManager.Instance != null ? LeaderboardManager.Instance.GetHighScore() : 0;
                        _highScoreBadgeText.gameObject.SetActive(true);
                        _sb.Clear();
                        _sb.Append("TARGET TO BEAT: ").Append(hi.ToString("N0")).Append(" PTS");
                        _highScoreBadgeText.SetText(_sb);
                        _highScoreBadgeText.color = LED_TEXT_MUTED;
                    }
                }

                // 6. Flusso Classifica & Inserimento Iniziali
                bool qualifies = GameManager.Instance.QualifiesForLeaderboard;

                if (qualifies && isLinked)
                {
                    string pilotInitials = profile.Initials;
                    string pilotEmail = profile.Email;
                    GameManager.Instance.RecordFinalScore(pilotInitials, pilotEmail);

                    if (_retryButton != null) _retryButton.gameObject.SetActive(true);
                    if (_menuButton != null) _menuButton.gameObject.SetActive(true);

                    if (_initialsContainer != null)
                    {
                        _initialsContainer.SetActive(true);
                        if (_initialsInputField != null) _initialsInputField.gameObject.SetActive(false);
                        if (_submitInitialsButton != null) _submitInitialsButton.gameObject.SetActive(false);

                        if (_initialsPromptText != null)
                        {
                            _sb.Clear();
                            _sb.Append("SCORE SAVED TO LEADERBOARD!\nPILOT: <color=#00FF9D>").Append(pilotInitials).Append("</color>");
                            _initialsPromptText.SetText(_sb);
                            _initialsPromptText.color = LED_GREEN_PHOSPHOR;
                        }
                    }
                }
                else
                {
                    if (_retryButton != null)
                        _retryButton.gameObject.SetActive(!qualifies);

                    if (_menuButton != null)
                        _menuButton.gameObject.SetActive(!qualifies);

                    if (_initialsContainer != null)
                    {
                        _initialsContainer.SetActive(qualifies);
                        if (qualifies)
                        {
                            if (_initialsInputField != null) _initialsInputField.gameObject.SetActive(true);
                            if (_submitInitialsButton != null) _submitInitialsButton.gameObject.SetActive(true);

                            if (_initialsPromptText != null)
                            {
                                _initialsPromptText.SetText("TOP 10 ACHIEVED!\nENTER YOUR 3 INITIALS:");
                                _initialsPromptText.color = LED_AMBER_SCORE;
                            }

                            if (_submitInitialsButton != null)
                                _submitInitialsButton.interactable = false;

                            if (_initialsInputField != null)
                            {
                                _initialsInputField.text = string.Empty;
                                if (_initialsInputField.textComponent != null)
                                    _initialsInputField.textComponent.color = INPUT_TEXT_DARK_BLUE;
                                _initialsInputField.interactable = true;
                                _initialsInputField.Select();
                                _initialsInputField.ActivateInputField();
                            }
                        }
                    }
                    else if (qualifies)
                    {
                        GameManager.Instance.RecordFinalScore(isLinked ? profile.Initials : "AAA");
                        GameManager.Instance.ReturnToMainMenu();
                    }
                }
            }
        }

        private void OnInitialsValueChanged(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                if (_submitInitialsButton != null) _submitInitialsButton.interactable = false;
                return;
            }

            string upper = val.ToUpperInvariant();
            if (upper != val && _initialsInputField != null)
            {
                _initialsInputField.text = upper;
                _initialsInputField.caretPosition = upper.Length;
            }

            if (_initialsInputField != null && _initialsInputField.textComponent != null)
            {
                _initialsInputField.textComponent.color = INPUT_TEXT_DARK_BLUE;
            }

            if (_submitInitialsButton != null)
            {
                _submitInitialsButton.interactable = (upper.Trim().Length == 3);
            }
        }

        private void OnSubmitInitialsClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            if (GameManager.Instance == null) return;

            string initials = _initialsInputField != null ? _initialsInputField.text.Trim() : string.Empty;
            if (initials.Length < 3)
            {
                if (_initialsPromptText != null)
                {
                    _initialsPromptText.SetText("ENTER 3 LETTERS!");
                    _initialsPromptText.color = LED_RED_ALERT;
                }
                return;
            }

            string email = (UserProfileManager.Instance != null && UserProfileManager.Instance.IsLinked) ? UserProfileManager.Instance.Email : string.Empty;
            GameManager.Instance.RecordFinalScore(initials, email);

            if (_initialsContainer != null)
                _initialsContainer.SetActive(false);

            GameManager.Instance.ReturnToMainMenu();
        }

        private void OnRetryClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            EnsureScoreSaved();
            GameManager.Instance?.RestartGame();
        }

        private void OnMenuClicked()
        {
            AudioManager.Instance?.PlayButtonClick();
            EnsureScoreSaved();
            GameManager.Instance?.ReturnToMainMenu();
        }

        private void EnsureScoreSaved()
        {
            if (GameManager.Instance != null && !GameManager.Instance.ScoreRecorded)
            {
                var profile = UserProfileManager.Instance;
                bool isLinked = profile != null && profile.IsLinked;
                string initials;
                if (isLinked)
                {
                    initials = profile.Initials;
                }
                else if (_initialsInputField != null && !string.IsNullOrWhiteSpace(_initialsInputField.text) && _initialsInputField.text.Trim().Length == 3)
                {
                    initials = _initialsInputField.text.Trim();
                }
                else
                {
                    initials = "AAA";
                }
                string email = isLinked ? profile.Email : string.Empty;
                GameManager.Instance.RecordFinalScore(initials, email);
            }
        }
    }
}
