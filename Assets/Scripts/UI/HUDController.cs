using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MobileGame.Core;

namespace MobileGame.UI
{
    /// <summary>
    /// Gestisce l'interfaccia HUD durante la partita:
    /// - Barra XP elegante con stile Cyber-Neon, indicatore di livello e animazione fluida (Lerp).
    /// - Score e Wave ingranditi (42pt) con colori arcade brillanti (Oro e Ciano).
    /// - Banner animato ad ogni cambio Wave con effetto punch-in e dissolvenza automatica.
    /// Conforme alle regole AGENTS.md (zero GC in loop).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Riferimenti UI")]
        [SerializeField] private Slider   _healthSlider;
        [SerializeField] private Slider   _xpSlider;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _timerText;

        [Header("Elementi XP & Wave Migliorati (Auto-configurati se non assegnati)")]
        [SerializeField] private TMP_Text    _xpLevelText;
        [SerializeField] private TMP_Text    _waveBannerText;
        [SerializeField] private CanvasGroup _waveBannerGroup;

        [Header("Pannello Opzionale")]
        [SerializeField] private GameObject _hudPanel;

        public static HUDController Instance { get; private set; }

        // Overlay lampeggio allarme rosso a tutto schermo (zero GC)
        private Image     _screenFlashOverlay;
        private Coroutine _screenFlashCoroutine;

        // Pre-allocati in Awake: zero alloc negli aggiornamenti testo (AGENTS.md §4)
        private readonly StringBuilder _sb       = new StringBuilder(64);
        private readonly StringBuilder _bannerSb = new StringBuilder(128);

        private HealthComponent      _playerHealth;
        private SurvivalTimer        _timer;
        private System.Action<float> _updateTimerCallback;

        private float     _targetXP = 0f;
        private Coroutine _waveBannerCoroutine;

        // Palette Colori Arcade Neon
        private readonly Color _colorScore = new Color(1.0f, 0.88f, 0.35f, 1.0f); // Oro arcade
        private readonly Color _colorWave  = new Color(0.0f, 0.90f, 1.0f, 1.0f);  // Ciano neon elettrico
        private readonly Color _colorBg    = new Color(0.04f, 0.08f, 0.14f, 0.90f); // Sfondo scuro cianotico
        private readonly Color _colorXp    = new Color(0.0f, 0.88f, 1.0f, 1.0f);   // Barra riempimento ciano

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            SetupXPBarVisuals();
            SetupScoreAndWaveVisuals();
            EnsureWaveBannerExists();
            EnsureScreenFlashOverlayExists();
            EnsureFloatingJoystickExists();
            EnsurePauseMenuExists();
        }

        private void Start()
        {
            _updateTimerCallback = UpdateTimer;

            if (_healthSlider != null) _healthSlider.gameObject.SetActive(false);

            // Sottoscrivi timer di sopravvivenza
            _timer = SurvivalTimer.Instance;
            if (_timer != null)
            {
                _timer.OnTimeChanged += _updateTimerCallback;
                UpdateTimer(_timer.ElapsedTime);
            }

            // Sottoscrivi gli eventi del GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted  += HandleGameStarted;
                GameManager.Instance.OnScoreChanged += UpdateScore;
                GameManager.Instance.OnWaveChanged  += UpdateWave;
                GameManager.Instance.OnGameOver     += HandleGameOver;

                SetHUDActive(GameManager.Instance.CurrentState == GameState.Playing);

                UpdateScore(GameManager.Instance.Score);
                UpdateWave(GameManager.Instance.Wave);
            }
            else
            {
                SetHUDActive(false);
            }

            if (XPManager.Instance != null)
            {
                XPManager.Instance.OnXPChanged += UpdateXP;
                XPManager.Instance.OnLevelUp   += HandleLevelUp;
                UpdateXP(XPManager.Instance.CurrentXP, XPManager.Instance.XPForNextLevel);
            }
        }

        private void Update()
        {
            // Lerp fluido della barra XP: zero allocazioni heap in loop (AGENTS.md §4)
            if (_xpSlider != null && Mathf.Abs(_xpSlider.value - _targetXP) > 0.0005f)
            {
                _xpSlider.value = Mathf.MoveTowards(_xpSlider.value, _targetXP, 2.8f * Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_timer != null && _updateTimerCallback != null)
                _timer.OnTimeChanged -= _updateTimerCallback;

            if (_playerHealth != null)
                _playerHealth.OnHealthChanged -= UpdateHealth;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted  -= HandleGameStarted;
                GameManager.Instance.OnScoreChanged -= UpdateScore;
                GameManager.Instance.OnWaveChanged  -= UpdateWave;
                GameManager.Instance.OnGameOver     -= HandleGameOver;
            }

            if (XPManager.Instance != null)
            {
                XPManager.Instance.OnXPChanged -= UpdateXP;
                XPManager.Instance.OnLevelUp   -= HandleLevelUp;
            }
        }

        // ── Game Flow Handlers ─────────────────────────────────────────────────

        private void HandleGameStarted()
        {
            SetHUDActive(true);
            UpdateTimer(0f);
            if (_xpSlider != null) _xpSlider.value = 0f;
            _targetXP = 0f;
            UpdateLevelDisplay();
        }

        private void HandleGameOver()
        {
            SetHUDActive(false);
            if (_waveBannerCoroutine != null)
            {
                StopCoroutine(_waveBannerCoroutine);
                _waveBannerCoroutine = null;
            }
            if (_waveBannerGroup != null)
            {
                _waveBannerGroup.alpha = 0f;
                _waveBannerGroup.gameObject.SetActive(false);
            }
            if (_screenFlashCoroutine != null)
            {
                StopCoroutine(_screenFlashCoroutine);
                _screenFlashCoroutine = null;
            }
            if (_screenFlashOverlay != null)
            {
                _screenFlashOverlay.gameObject.SetActive(false);
            }
        }

        public void SetHUDActive(bool active)
        {
            if (_hudPanel != null)
            {
                _hudPanel.SetActive(active);
            }
            else
            {
                if (_xpSlider != null)     _xpSlider.gameObject.SetActive(active);
                if (_scoreText != null)    _scoreText.gameObject.SetActive(active);
                if (_waveText != null)     _waveText.gameObject.SetActive(active);
                if (_timerText != null)    _timerText.gameObject.SetActive(active);
            }

            // La vita è ora sopra la testa del player, nascondi lo slider in alto
            if (_healthSlider != null)
                _healthSlider.gameObject.SetActive(false);

            if (!active && _waveBannerGroup != null)
                _waveBannerGroup.gameObject.SetActive(false);

            if (!active && FloatingJoystick.Instance != null)
                FloatingJoystick.Instance.Hide();

            if (PauseMenu.Instance != null)
            {
                PauseMenu.Instance.SetPauseButtonVisible(active && !PauseMenu.Instance.IsPaused);
                if (!active && PauseMenu.Instance.IsPaused)
                {
                    PauseMenu.Instance.ClosePauseMenuInstant();
                }
            }
        }

        // ── Visual Setup & Styling ─────────────────────────────────────────────

        private void SetupXPBarVisuals()
        {
            if (_xpSlider == null) return;

            // 1. Rimuovi la manopola (handle) per trasformarla in una vera barra di progresso
            if (_xpSlider.handleRect != null)
                _xpSlider.handleRect.gameObject.SetActive(false);

            // 2. Posizionamento a tutta larghezza in cima allo schermo
            RectTransform sliderRt = _xpSlider.GetComponent<RectTransform>();
            if (sliderRt != null)
            {
                sliderRt.anchorMin = new Vector2(0f, 1f);
                sliderRt.anchorMax = new Vector2(1f, 1f);
                sliderRt.pivot     = new Vector2(0.5f, 1f);
                sliderRt.anchoredPosition = new Vector2(0f, -16f);
                sliderRt.sizeDelta = new Vector2(-80f, 22f);
            }

            // 3. Sfondo scuro cyberpunk
            Transform bgTransform = _xpSlider.transform.Find("Background");
            if (bgTransform != null)
            {
                Image bgImg = bgTransform.GetComponent<Image>();
                if (bgImg != null) bgImg.color = _colorBg;
            }

            // 4. Riempimento ciano neon con padding interno
            Transform fillArea = _xpSlider.transform.Find("Fill Area");
            if (fillArea != null)
            {
                RectTransform faRt = fillArea.GetComponent<RectTransform>();
                if (faRt != null)
                {
                    faRt.anchorMin = Vector2.zero;
                    faRt.anchorMax = Vector2.one;
                    faRt.offsetMin = new Vector2(3f, 3f);
                    faRt.offsetMax = new Vector2(-3f, -3f);
                }

                Transform fillTransform = fillArea.Find("Fill");
                if (fillTransform != null)
                {
                    RectTransform fillRt = fillTransform.GetComponent<RectTransform>();
                    if (fillRt != null) fillRt.sizeDelta = Vector2.zero;

                    Image fillImg = fillTransform.GetComponent<Image>();
                    if (fillImg != null) fillImg.color = _colorXp;
                }
            }

            // 5. Testo livello compatto (LV. 1) all'interno della barra
            if (_xpLevelText == null)
            {
                CreateXPLevelText();
            }
        }

        private void CreateXPLevelText()
        {
            if (_xpSlider == null) return;

            var lvlObj = new GameObject("XPLevelBadge");
            lvlObj.transform.SetParent(_xpSlider.transform, false);

            var rt = lvlObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(12f, 0f);
            rt.sizeDelta = new Vector2(160f, 22f);

            _xpLevelText = lvlObj.AddComponent<TextMeshProUGUI>();
            _xpLevelText.fontSize = 18f;
            _xpLevelText.fontStyle = FontStyles.Bold;
            _xpLevelText.color = Color.white;
            _xpLevelText.alignment = TextAlignmentOptions.MidlineLeft;
            _xpLevelText.textWrappingMode = TextWrappingModes.NoWrap;
            _xpLevelText.raycastTarget = false;

            if (_scoreText != null && _scoreText.font != null)
                _xpLevelText.font = _scoreText.font;

            UpdateLevelDisplay();
        }

        private void SetupScoreAndWaveVisuals()
        {
            if (_scoreText != null)
            {
                _scoreText.fontSize = 42f;
                _scoreText.fontStyle = FontStyles.Bold;
                _scoreText.color = _colorScore;
                _scoreText.textWrappingMode = TextWrappingModes.NoWrap;

                RectTransform rt = _scoreText.rectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot     = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(40f, -48f);
                    rt.sizeDelta = new Vector2(420f, 65f);
                    _scoreText.alignment = TextAlignmentOptions.MidlineLeft;
                }
            }

            if (_waveText != null)
            {
                _waveText.fontSize = 42f;
                _waveText.fontStyle = FontStyles.Bold;
                _waveText.color = _colorWave;
                _waveText.textWrappingMode = TextWrappingModes.NoWrap;

                RectTransform rt = _waveText.rectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot     = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-40f, -48f);
                    rt.sizeDelta = new Vector2(320f, 65f);
                    _waveText.alignment = TextAlignmentOptions.MidlineRight;
                }
            }
        }

        private void EnsureWaveBannerExists()
        {
            if (_waveBannerText != null && _waveBannerGroup != null) return;

            Transform canvasTransform = _hudPanel != null ? _hudPanel.transform : transform.parent;
            if (canvasTransform == null) canvasTransform = transform;

            var bannerObj = new GameObject("WaveAnnouncementBanner");
            bannerObj.transform.SetParent(canvasTransform, false);

            var rt = bannerObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 250f);
            rt.sizeDelta = new Vector2(960f, 200f);

            _waveBannerGroup = bannerObj.AddComponent<CanvasGroup>();
            _waveBannerGroup.alpha = 0f;
            _waveBannerGroup.blocksRaycasts = false;
            _waveBannerGroup.interactable = false;

            _waveBannerText = bannerObj.AddComponent<TextMeshProUGUI>();
            _waveBannerText.textWrappingMode = TextWrappingModes.Normal;
            _waveBannerText.alignment = TextAlignmentOptions.Center;
            _waveBannerText.raycastTarget = false;
            _waveBannerText.fontSize = 88f;

            if (_scoreText != null && _scoreText.font != null)
                _waveBannerText.font = _scoreText.font;

            bannerObj.SetActive(false);
        }

        private void EnsureScreenFlashOverlayExists()
        {
            if (_screenFlashOverlay != null) return;

            Transform canvasTransform = _hudPanel != null ? _hudPanel.transform : transform.parent;
            if (canvasTransform == null) canvasTransform = transform;

            var flashObj = new GameObject("ScreenAlarmFlashOverlay");
            flashObj.transform.SetParent(canvasTransform, false);
            flashObj.transform.SetAsFirstSibling(); // Dietro a testi e HUD ma davanti al mondo di gioco

            var rt = flashObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _screenFlashOverlay = flashObj.AddComponent<Image>();
            _screenFlashOverlay.color = new Color(1f, 0f, 0f, 0f);
            _screenFlashOverlay.raycastTarget = false;
            flashObj.SetActive(false);
        }

        private void EnsureFloatingJoystickExists()
        {
            if (FloatingJoystick.Instance != null) return;

            Transform canvasTransform = _hudPanel != null ? _hudPanel.transform : transform.parent;
            if (canvasTransform == null) canvasTransform = transform;

            var joystickObj = new GameObject("FloatingJoystick");
            joystickObj.transform.SetParent(canvasTransform, false);
            joystickObj.transform.SetSiblingIndex(1); // Dietro ai testi della HUD ma sopra il flash

            var rt = joystickObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            joystickObj.AddComponent<FloatingJoystick>();
        }

        private void EnsurePauseMenuExists()
        {
            if (PauseMenu.Instance != null) return;

            Transform canvasTransform = _hudPanel != null ? _hudPanel.transform : transform.parent;
            if (canvasTransform == null) canvasTransform = transform;

            var pauseObj = new GameObject("PauseMenu");
            pauseObj.transform.SetParent(canvasTransform, false);

            var rt = pauseObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            pauseObj.AddComponent<PauseMenu>();
        }

        // ── Aggiornamenti Event-Driven ─────────────────────────────────────────

        private void UpdateHealth(float current, float max)
        {
            if (_healthSlider != null)
                _healthSlider.value = max > 0f ? current / max : 0f;
        }

        private void UpdateXP(int current, int max)
        {
            float newTarget = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (newTarget < _targetXP && _xpSlider != null)
            {
                // Reset istantaneo a 0 al level-up per non scendere all'indietro
                _xpSlider.value = 0f;
            }
            _targetXP = newTarget;
            UpdateLevelDisplay();
        }

        private void HandleLevelUp(int level)
        {
            UpdateLevelDisplay();
        }

        private void UpdateLevelDisplay()
        {
            if (_xpLevelText == null) return;
            int level = XPManager.Instance != null ? XPManager.Instance.CurrentLevel : 1;
            _sb.Clear();
            _sb.Append("LV. ").Append(level);
            _xpLevelText.SetText(_sb);
        }

        private void UpdateScore(int score)
        {
            if (_scoreText == null) return;
            _sb.Clear();
            _sb.Append("SCORE: ").Append(score);
            _scoreText.SetText(_sb);
        }

        private void UpdateWave(int wave)
        {
            if (_waveText == null) return;
            _sb.Clear();
            _sb.Append("WAVE ").Append(wave);
            _waveText.SetText(_sb);

            // Annuncio a tutto schermo al cambio wave
            ShowWaveAnnouncement(wave);
        }

        private void UpdateTimer(float elapsedTime)
        {
            if (_timerText == null) return;

            if (elapsedTime < 0f) elapsedTime = 0f;
            int mins = (int)(elapsedTime / 60f);
            int secs = (int)(elapsedTime % 60f);

            _sb.Clear();
            if (mins < 10) _sb.Append('0');
            _sb.Append(mins).Append(':');
            if (secs < 10) _sb.Append('0');
            _sb.Append(secs);

            _timerText.SetText(_sb);
        }

        // ── Animazione Banner Cambio Wave ──────────────────────────────────────

        public void ShowWaveAnnouncement(int wave)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
                return;

            EnsureWaveBannerExists();

            if (_waveBannerCoroutine != null)
                StopCoroutine(_waveBannerCoroutine);

            _waveBannerCoroutine = StartCoroutine(AnimateWaveBannerRoutine(wave));
        }

        private IEnumerator AnimateWaveBannerRoutine(int wave)
        {
            if (_waveBannerText == null || _waveBannerGroup == null) yield break;

            _bannerSb.Clear();
            _bannerSb.Append("<size=88><b><color=#FFFFFF>WAVE </color><color=#FFD700>").Append(wave).Append("</color></b></size>");
            _waveBannerText.SetText(_bannerSb);

            Transform bannerT = _waveBannerGroup.transform;
            _waveBannerGroup.gameObject.SetActive(true);

            // Fase 1: Pop-in dinamico con scale punch (1.35x -> 1.0x) e dissolvenza (0 -> 1)
            float elapsed = 0f;
            const float popDuration = 0.22f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float s = Mathf.Lerp(1.35f, 1.0f, t);
                bannerT.localScale = new Vector3(s, s, 1f);
                _waveBannerGroup.alpha = t;
                yield return null;
            }

            bannerT.localScale = Vector3.one;
            _waveBannerGroup.alpha = 1f;

            // Fase 2: Pausa ad alta leggibilità (~1.4 secondi)
            yield return new WaitForSecondsRealtime(1.4f);

            // Fase 3: Dissolvenza in uscita fluida (1 -> 0)
            elapsed = 0f;
            const float fadeDuration = 0.45f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                _waveBannerGroup.alpha = 1f - t;
                yield return null;
            }

            _waveBannerGroup.alpha = 0f;
            _waveBannerGroup.gameObject.SetActive(false);
            _waveBannerCoroutine = null;
        }

        // ── Animazione Banner Avvisi Eventi Speciali (Swarm / Treasure) ─────────

        public void ShowEventAnnouncement(string title, string subtitle, Color color, float displayDuration = 2.0f, bool flashScreen = false)
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Playing)
                return;

            EnsureWaveBannerExists();

            if (_waveBannerCoroutine != null)
                StopCoroutine(_waveBannerCoroutine);

            _waveBannerCoroutine = StartCoroutine(AnimateEventBannerRoutine(title, subtitle, color, displayDuration));

            if (flashScreen)
            {
                TriggerScreenFlash(color, displayDuration + 0.6f);
            }
        }

        private IEnumerator AnimateEventBannerRoutine(string title, string subtitle, Color color, float displayDuration)
        {
            if (_waveBannerText == null || _waveBannerGroup == null) yield break;

            string hexColor = ColorUtility.ToHtmlStringRGB(color);
            _bannerSb.Clear();
            _bannerSb.Append("<size=52><b><color=#").Append(hexColor).Append(">").Append(title).Append("</color></b></size>");
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                _bannerSb.Append("\n<size=28><color=#E0E0E0>").Append(subtitle).Append("</color></size>");
            }
            _waveBannerText.SetText(_bannerSb);

            Transform bannerT = _waveBannerGroup.transform;
            _waveBannerGroup.gameObject.SetActive(true);

            // Pop-in rapido
            float elapsed = 0f;
            const float popDuration = 0.20f;
            while (elapsed < popDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / popDuration);
                float s = Mathf.Lerp(1.4f, 1.0f, t);
                bannerT.localScale = new Vector3(s, s, 1f);
                _waveBannerGroup.alpha = t;
                yield return null;
            }

            bannerT.localScale = Vector3.one;
            _waveBannerGroup.alpha = 1f;

            // Pausa visibile (3-4 secondi come richiesto)
            yield return new WaitForSecondsRealtime(displayDuration);

            // Dissolvenza in uscita
            elapsed = 0f;
            const float fadeDuration = 0.50f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                _waveBannerGroup.alpha = 1f - t;
                yield return null;
            }

            _waveBannerGroup.alpha = 0f;
            _waveBannerGroup.gameObject.SetActive(false);
            _waveBannerCoroutine = null;
        }

        public void TriggerScreenFlash(Color color, float totalDuration)
        {
            EnsureScreenFlashOverlayExists();
            if (_screenFlashOverlay == null) return;

            if (_screenFlashCoroutine != null)
                StopCoroutine(_screenFlashCoroutine);

            _screenFlashCoroutine = StartCoroutine(AnimateScreenFlashRoutine(color, totalDuration));
        }

        private IEnumerator AnimateScreenFlashRoutine(Color color, float totalDuration)
        {
            if (_screenFlashOverlay == null) yield break;

            _screenFlashOverlay.gameObject.SetActive(true);
            float elapsed = 0f;

            // Lampeggio ritmico d'allarme rosso a tutto schermo
            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                // Onda sinusoidale da 0 a 0.35 alpha ad alta frequenza (~4Hz)
                float pulse = (Mathf.Sin(elapsed * 12f) + 1f) * 0.5f;
                float alpha = pulse * 0.38f;
                _screenFlashOverlay.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            // Dissolvenza finale a zero
            float fadeElapsed = 0f;
            float startAlpha = _screenFlashOverlay.color.a;
            while (fadeElapsed < 0.3f)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(startAlpha, 0f, fadeElapsed / 0.3f);
                _screenFlashOverlay.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }

            _screenFlashOverlay.color = new Color(color.r, color.g, color.b, 0f);
            _screenFlashOverlay.gameObject.SetActive(false);
            _screenFlashCoroutine = null;
        }
    }
}
