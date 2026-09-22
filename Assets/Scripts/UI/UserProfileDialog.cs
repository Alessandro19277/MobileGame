using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MobileGame.Core;

namespace MobileGame.UI
{
    /// <summary>
    /// Finestra di dialogo / Modale per il collegamento dell'account pilota via Email e 3 iniziali arcade.
    /// Si apre dal Menu Principale. Quando collegato, i record a Game Over vengono salvati automaticamente.
    /// Include mascheramento orizzontale a scorrimento (RectMask2D) e gestione click reattiva.
    /// </summary>
    public class UserProfileDialog : MonoBehaviour
    {
        [Header("Contenitore")]
        [SerializeField] private GameObject _dialogPanel;

        [Header("Testi")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _statusInfoText;
        [SerializeField] private TMP_Text _feedbackText;

        [Header("Input Fields")]
        [SerializeField] private TMP_InputField _emailInputField;
        [SerializeField] private TMP_InputField _initialsInputField;

        [Header("Pulsanti")]
        [SerializeField] private Button _saveButton;
        [SerializeField] private TMP_Text _saveButtonText;
        [SerializeField] private Button _unlinkButton;
        [SerializeField] private Button _closeButton;

        public event Action OnDialogClosed;

        private readonly StringBuilder _sb = new StringBuilder(256);

        private void Awake()
        {
            if (_dialogPanel == null)
                _dialogPanel = gameObject;

            BindControls();
        }

        private void OnDestroy()
        {
            UnbindControls();
        }

        public void BindControls()
        {
            if (_saveButton != null)
            {
                _saveButton.onClick.RemoveListener(OnSaveClicked);
                _saveButton.onClick.AddListener(OnSaveClicked);
            }

            if (_unlinkButton != null)
            {
                _unlinkButton.onClick.RemoveListener(OnUnlinkClicked);
                _unlinkButton.onClick.AddListener(OnUnlinkClicked);
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
                _closeButton.onClick.AddListener(Close);
            }

            if (_initialsInputField != null)
            {
                _initialsInputField.characterLimit = 3;
                _initialsInputField.onValueChanged.RemoveListener(OnInitialsChanged);
                _initialsInputField.onValueChanged.AddListener(OnInitialsChanged);
                _initialsInputField.onSubmit.RemoveListener(OnInputSubmit);
                _initialsInputField.onSubmit.AddListener(OnInputSubmit);
            }

            if (_emailInputField != null)
            {
                _emailInputField.onSubmit.RemoveListener(OnInputSubmit);
                _emailInputField.onSubmit.AddListener(OnInputSubmit);
            }
        }

        private void UnbindControls()
        {
            if (_saveButton != null)
                _saveButton.onClick.RemoveListener(OnSaveClicked);

            if (_unlinkButton != null)
                _unlinkButton.onClick.RemoveListener(OnUnlinkClicked);

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);

            if (_initialsInputField != null)
            {
                _initialsInputField.onValueChanged.RemoveListener(OnInitialsChanged);
                _initialsInputField.onSubmit.RemoveListener(OnInputSubmit);
            }

            if (_emailInputField != null)
            {
                _emailInputField.onSubmit.RemoveListener(OnInputSubmit);
            }
        }

        private void OnInputSubmit(string _)
        {
            OnSaveClicked();
        }

        // ── Apertura e Chiusura ────────────────────────────────────────────────

        public void Open()
        {
            if (_dialogPanel != null)
                _dialogPanel.SetActive(true);

            BindControls();
            RefreshUI();
        }

        public void Close()
        {
            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);

            OnDialogClosed?.Invoke();
        }

        // ── Aggiornamento Stato UI ─────────────────────────────────────────────

        public void RefreshUI()
        {
            var profile = UserProfileManager.Instance;
            bool isLinked = profile != null && profile.IsLinked;

            if (_titleText != null)
            {
                _titleText.SetText("<color=#00FFFF>PROFILO PILOTA / ACCOUNT</color>");
            }

            if (_statusInfoText != null)
            {
                _sb.Clear();
                if (isLinked)
                {
                    _sb.AppendLine("<color=#00E676>[ACCOUNT COLLEGATO]</color>");
                    _sb.Append("Email: <color=#66FCF1>").Append(profile.Email).AppendLine("</color>");
                    _sb.Append("Iniziali record: <color=#FFD700>").Append(profile.Initials).AppendLine("</color>");
                    _sb.Append("<size=80%><color=#A0AEC0>I record verranno inseriti automaticamente in classifica.</color></size>");
                }
                else
                {
                    _sb.AppendLine("<color=#FFA500>[NON COLLEGATO]</color>");
                    _sb.AppendLine("Collega la tua email e imposta le tue 3 iniziali.");
                    _sb.Append("<size=80%><color=#A0AEC0>In caso di record il gioco saprà già chi sei!</color></size>");
                }
                _statusInfoText.SetText(_sb);
            }

            if (_emailInputField != null)
            {
                _emailInputField.text = isLinked ? profile.Email : string.Empty;
            }

            if (_initialsInputField != null)
            {
                _initialsInputField.text = isLinked ? profile.Initials : string.Empty;
            }

            if (_saveButtonText != null)
            {
                _saveButtonText.SetText(isLinked ? "AGGIORNA PROFILO" : "CONFERMA E COLLEGA");
            }

            if (_unlinkButton != null)
            {
                _unlinkButton.gameObject.SetActive(isLinked);
            }

            if (_feedbackText != null)
            {
                _feedbackText.SetText(string.Empty);
            }
        }

        private void OnInitialsChanged(string val)
        {
            if (string.IsNullOrEmpty(val)) return;

            string upper = val.ToUpperInvariant();
            if (upper != val && _initialsInputField != null)
            {
                _initialsInputField.text = upper;
                _initialsInputField.caretPosition = upper.Length;
            }
        }

        // ── Handler Pulsanti ───────────────────────────────────────────────────

        private void OnSaveClicked()
        {
            string email = _emailInputField != null ? _emailInputField.text.Trim() : string.Empty;
            string initials = _initialsInputField != null ? _initialsInputField.text.Trim() : string.Empty;

            if (UserProfileManager.Instance == null) return;

            bool success = UserProfileManager.Instance.LinkProfile(email, initials, out string error);

            if (_feedbackText != null)
            {
                if (success)
                {
                    _feedbackText.color = new Color(0.00f, 0.95f, 0.46f); // Neon green
                    _feedbackText.SetText($"PROFILO COLLEGATO COME [{UserProfileManager.Instance.Initials}]!");
                }
                else
                {
                    _feedbackText.color = new Color(1.0f, 0.35f, 0.35f); // Red
                    _feedbackText.SetText(error);
                }
            }

            if (success)
            {
                RefreshUI();
            }
        }

        private void OnUnlinkClicked()
        {
            if (UserProfileManager.Instance == null) return;

            UserProfileManager.Instance.UnlinkProfile();

            if (_feedbackText != null)
            {
                _feedbackText.color = new Color(1f, 0.85f, 0.1f);
                _feedbackText.SetText("Account scollegato.");
            }

            RefreshUI();
        }

        // ── Factory Procedurale per Setup Rapido se non presente in Scena ─────

        public static UserProfileDialog CreateDialog(Transform parent, TMP_FontAsset font)
        {
            var dialogGo = new GameObject("UserProfileDialog");
            dialogGo.transform.SetParent(parent, false);

            var rt = dialogGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = dialogGo.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.05f, 0.09f, 0.96f);
            bg.raycastTarget = true;

            var dialog = dialogGo.AddComponent<UserProfileDialog>();

            // Pannello centrale (Card)
            var cardGo = new GameObject("Card");
            cardGo.transform.SetParent(dialogGo.transform, false);
            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(920, 1150);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = new Color(0.05f, 0.11f, 0.18f, 0.98f);
            cardImg.raycastTarget = true;

            // Titolo
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0, 470);
            titleRt.sizeDelta = new Vector2(820, 80);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "<color=#00FFFF>PROFILO PILOTA / ACCOUNT</color>";
            titleTmp.fontSize = 44;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;
            if (font != null) titleTmp.font = font;
            dialog._titleText = titleTmp;

            // Info Stato
            var statusGo = new GameObject("StatusInfoText");
            statusGo.transform.SetParent(cardGo.transform, false);
            var statusRt = statusGo.AddComponent<RectTransform>();
            statusRt.anchoredPosition = new Vector2(0, 320);
            statusRt.sizeDelta = new Vector2(820, 180);
            var statusTmp = statusGo.AddComponent<TextMeshProUGUI>();
            statusTmp.fontSize = 28;
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.raycastTarget = false;
            if (font != null) statusTmp.font = font;
            dialog._statusInfoText = statusTmp;

            // Input Email (con RectMask2D per clipping e scorrimento fluido del testo)
            var emailField = CreateSimpleInputField(cardGo.transform, "EmailField", new Vector2(0, 150), new Vector2(780, 95), "INSERISCI EMAIL (es. pilota@mail.com)", 64, font);
            dialog._emailInputField = emailField;

            // Input Iniziali (3 caratteri maiuscoli)
            var initialsField = CreateSimpleInputField(cardGo.transform, "InitialsField", new Vector2(0, 25), new Vector2(780, 95), "3 INIZIALI RECORD (es. ALE)", 3, font);
            dialog._initialsInputField = initialsField;

            // Feedback Text
            var feedGo = new GameObject("FeedbackText");
            feedGo.transform.SetParent(cardGo.transform, false);
            var feedRt = feedGo.AddComponent<RectTransform>();
            feedRt.anchoredPosition = new Vector2(0, -85);
            feedRt.sizeDelta = new Vector2(820, 75);
            var feedTmp = feedGo.AddComponent<TextMeshProUGUI>();
            feedTmp.fontSize = 26;
            feedTmp.alignment = TextAlignmentOptions.Center;
            feedTmp.raycastTarget = false;
            if (font != null) feedTmp.font = font;
            dialog._feedbackText = feedTmp;

            // Bottone Salva / Conferma
            var saveBtn = CreateSimpleButton(cardGo.transform, "SaveButton", new Vector2(0, -195), new Vector2(780, 110), new Color(0f, 0.72f, 0.58f), "CONFERMA E COLLEGA", out TMP_Text saveBtnText, font);
            dialog._saveButton = saveBtn;
            dialog._saveButtonText = saveBtnText;

            // Bottone Scollega
            var unlinkBtn = CreateSimpleButton(cardGo.transform, "UnlinkButton", new Vector2(0, -325), new Vector2(780, 95), new Color(0.65f, 0.18f, 0.22f), "SCOLLEGA ACCOUNT", out _, font);
            dialog._unlinkButton = unlinkBtn;

            // Bottone Chiudi
            var closeBtn = CreateSimpleButton(cardGo.transform, "CloseButton", new Vector2(0, -445), new Vector2(780, 95), new Color(0.22f, 0.28f, 0.38f), "INDIETRO / CHIUDI", out _, font);
            dialog._closeButton = closeBtn;

            dialog._dialogPanel = dialogGo;

            // FONDAMENTALE: Collega subito i listener di tutti i pulsanti e campi creati!
            dialog.BindControls();

            dialogGo.SetActive(false);

            return dialog;
        }

        private static TMP_InputField CreateSimpleInputField(Transform parent, string name, Vector2 pos, Vector2 size, string placeholderText, int charLimit, TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.16f, 0.26f, 1f);
            bgImg.raycastTarget = true;

            // Text Area con RectMask2D: maschera e fa scorrere i caratteri oltre il limite orizzontale
            var textAreaGo = new GameObject("Text Area");
            textAreaGo.transform.SetParent(go.transform, false);
            var textAreaRt = textAreaGo.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(25, 6);
            textAreaRt.offsetMax = new Vector2(-25, -6);
            textAreaGo.AddComponent<RectMask2D>();

            // Testo reale
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textAreaGo.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var textTmp = textGo.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 32;
            textTmp.alignment = TextAlignmentOptions.MidlineLeft;
            textTmp.color = Color.white;
            textTmp.raycastTarget = false;
            textTmp.overflowMode = TextOverflowModes.Overflow;
            textTmp.textWrappingMode = TextWrappingModes.NoWrap;
            if (font != null) textTmp.font = font;

            // Testo segnaposto (Placeholder)
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textAreaGo.transform, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var phTmp = phGo.AddComponent<TextMeshProUGUI>();
            phTmp.fontSize = 28;
            phTmp.alignment = TextAlignmentOptions.MidlineLeft;
            phTmp.color = new Color(0.5f, 0.6f, 0.7f, 0.7f);
            phTmp.text = placeholderText;
            phTmp.raycastTarget = false;
            phTmp.textWrappingMode = TextWrappingModes.NoWrap;
            phTmp.overflowMode = TextOverflowModes.Ellipsis;
            if (font != null) phTmp.font = font;

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = bgImg;
            input.textViewport = textAreaRt;
            input.textComponent = textTmp;
            input.placeholder = phTmp;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = charLimit;

            return input;
        }

        private static Button CreateSimpleButton(Transform parent, string name, Vector2 pos, Vector2 size, Color btnColor, string label, out TMP_Text labelText, TMP_FontAsset font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = btnColor;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = true;

            var colors = btn.colors;
            colors.normalColor = btnColor;
            colors.highlightedColor = btnColor * 1.2f;
            colors.pressedColor = btnColor * 0.75f;
            colors.selectedColor = btnColor;
            btn.colors = colors;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 32;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false; // NON intercetta il click, lasciandolo direttamente al Button!
            if (font != null) tmp.font = font;

            labelText = tmp;
            return btn;
        }
    }
}
