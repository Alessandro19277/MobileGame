using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MobileGame.Audio;

namespace MobileGame.UI
{
    /// <summary>
    /// Schermata modale con la lore e i crediti del gioco:
    /// - Breve descrizione della missione ("You are a nanopilot...").
    /// - Creatori del gioco (Mauro Pigna & Alessandro Scavuzzo).
    /// - Stile Cyber-Arcade coerente con la Leaderboard e il GameOverScreen.
    /// - Zero allocazioni nei loop (AGENTS.md §4).
    /// </summary>
    public class CreditsDialog : MonoBehaviour
    {
        [Header("Pannello & Componenti")]
        [SerializeField] private GameObject _dialogPanel;
        [SerializeField] private Button     _closeButton;
        [SerializeField] private TMP_Text   _titleText;
        [SerializeField] private TMP_Text   _descriptionText;
        [SerializeField] private TMP_Text   _creatorsText;

        public event Action OnDialogClosed;

        private static readonly Color LED_AMBER_SCORE  = new Color(1.00f, 0.82f, 0.12f, 1.00f);
        private static readonly Color LED_CYAN_NEON    = new Color(0.00f, 0.95f, 1.00f, 1.00f);
        private static readonly Color LED_VIOLET_NEON  = new Color(0.92f, 0.28f, 1.00f, 1.00f);
        private static readonly Color LED_VIOLET_TEXT  = new Color(0.98f, 0.80f, 1.00f, 1.00f);

        private void Awake()
        {
            if (_dialogPanel == null)
                _dialogPanel = gameObject;

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Close);
                _closeButton.onClick.AddListener(Close);
            }
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
        }

        public void Open()
        {
            if (_dialogPanel != null)
            {
                _dialogPanel.transform.SetAsLastSibling();
                _dialogPanel.SetActive(true);
            }
        }

        public void Close()
        {
            AudioManager.Instance?.PlayButtonClick();

            if (_dialogPanel != null)
                _dialogPanel.SetActive(false);

            OnDialogClosed?.Invoke();
        }

        // ── Factory Procedurale per Setup Dinamico ─────────────────────────────

        public static CreditsDialog CreateDialog(Transform parent, TMP_FontAsset font)
        {
            var dialogGo = new GameObject("CreditsDialog");
            dialogGo.transform.SetParent(parent, false);

            var rt = dialogGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Sfondo oscurante semitrasparente che fa intravedere il Main Menu sottostante
            var bg = dialogGo.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.02f, 0.06f, 0.60f);
            bg.raycastTarget = true; // Blocca i click verso il menu sottostante

            var dialog = dialogGo.AddComponent<CreditsDialog>();
            dialog._dialogPanel = dialogGo;

            // Tocco fuori dalla card per chiudere il popup
            var bgBtn = dialogGo.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(dialog.Close);

            // Card centrale Popup: più larga (980) e più bassa (740) rispetto a prima
            var cardGo = new GameObject("Card");
            cardGo.transform.SetParent(dialogGo.transform, false);

            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot     = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta        = new Vector2(980f, 740f);

            var cardImg = cardGo.AddComponent<Image>();
            cardImg.sprite = CreatePopupPanelSprite(128, 20, 3);
            cardImg.type   = Image.Type.Sliced;
            cardImg.color  = Color.white;
            cardImg.raycastTarget = true; // Assorbe i click sulla card

            // 1. Titolo
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.5f);
            titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.pivot     = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 290f);
            titleRt.sizeDelta        = new Vector2(860f, 55f);

            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "ABOUT & CREDITS";
            titleTmp.fontSize = 32f;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = LED_AMBER_SCORE;
            if (font != null) titleTmp.font = font;
            titleTmp.raycastTarget = false;
            dialog._titleText = titleTmp;

            // 2. Linea d'accento orizzontale
            var accentGo = new GameObject("AccentLine");
            accentGo.transform.SetParent(cardGo.transform, false);
            var accentRt = accentGo.AddComponent<RectTransform>();
            accentRt.anchorMin = new Vector2(0.5f, 0.5f);
            accentRt.anchorMax = new Vector2(0.5f, 0.5f);
            accentRt.pivot     = new Vector2(0.5f, 0.5f);
            accentRt.anchoredPosition = new Vector2(0f, 252f);
            accentRt.sizeDelta        = new Vector2(900f, 2f);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = new Color(0.00f, 0.90f, 1.00f, 0.40f);
            accentImg.raycastTarget = false;

            // 3. Descrizione del gioco (Mission Brief)
            var descGo = new GameObject("DescriptionText");
            descGo.transform.SetParent(cardGo.transform, false);
            var descRt = descGo.AddComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.5f, 0.5f);
            descRt.anchorMax = new Vector2(0.5f, 0.5f);
            descRt.pivot     = new Vector2(0.5f, 0.5f);
            descRt.anchoredPosition = new Vector2(0f, 150f);
            descRt.sizeDelta        = new Vector2(880f, 140f);

            var descTmp = descGo.AddComponent<TextMeshProUGUI>();
            descTmp.text = "<color=#00FFFF>[ MISSION BRIEFING ]</color>\n\n<line-height=130%><color=#E2E8F0>\"You are a nanopilot trying to defend an organism from a virus apocalypse. Don't get hit pilot and survive as long as possible!\"</color></line-height>";
            descTmp.fontSize = 19f;
            descTmp.alignment = TextAlignmentOptions.Center;
            descTmp.textWrappingMode = TextWrappingModes.Normal;
            if (font != null) descTmp.font = font;
            descTmp.raycastTarget = false;
            dialog._descriptionText = descTmp;

            // 5. Separatore Cyber
            var divGo = new GameObject("DividerText");
            divGo.transform.SetParent(cardGo.transform, false);
            var divRt = divGo.AddComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.5f, 0.5f);
            divRt.anchorMax = new Vector2(0.5f, 0.5f);
            divRt.pivot     = new Vector2(0.5f, 0.5f);
            divRt.anchoredPosition = new Vector2(0f, 55f);
            divRt.sizeDelta        = new Vector2(820f, 20f);

            var divTmp = divGo.AddComponent<TextMeshProUGUI>();
            divTmp.text = "<color=#334155>--------------------------------</color>";
            divTmp.fontSize = 14f;
            divTmp.alignment = TextAlignmentOptions.Center;
            if (font != null) divTmp.font = font;
            divTmp.raycastTarget = false;

            // 6. Creatori del gioco
            var creatorsGo = new GameObject("CreatorsText");
            creatorsGo.transform.SetParent(cardGo.transform, false);
            var creatorsRt = creatorsGo.AddComponent<RectTransform>();
            creatorsRt.anchorMin = new Vector2(0.5f, 0.5f);
            creatorsRt.anchorMax = new Vector2(0.5f, 0.5f);
            creatorsRt.pivot     = new Vector2(0.5f, 0.5f);
            creatorsRt.anchoredPosition = new Vector2(0f, -55f);
            creatorsRt.sizeDelta        = new Vector2(880f, 150f);

            var creatorsTmp = creatorsGo.AddComponent<TextMeshProUGUI>();
            creatorsTmp.text = "<color=#EA47FF>[ GAME CREATORS ]</color>\n\n<line-height=140%><color=#FFFFFF><b>Mauro Pigna</b></color> <color=#94A3B8>&</color> <color=#FFFFFF><b>Alessandro Scavuzzo</b></color></line-height>";
            creatorsTmp.fontSize = 24f;
            creatorsTmp.alignment = TextAlignmentOptions.Center;
            if (font != null) creatorsTmp.font = font;
            creatorsTmp.raycastTarget = false;
            dialog._creatorsText = creatorsTmp;

            // 7. Footer info
            var footerGo = new GameObject("FooterText");
            footerGo.transform.SetParent(cardGo.transform, false);
            var footerRt = footerGo.AddComponent<RectTransform>();
            footerRt.anchorMin = new Vector2(0.5f, 0.5f);
            footerRt.anchorMax = new Vector2(0.5f, 0.5f);
            footerRt.pivot     = new Vector2(0.5f, 0.5f);
            footerRt.anchoredPosition = new Vector2(0f, -165f);
            footerRt.sizeDelta        = new Vector2(880f, 30f);

            var footerTmp = footerGo.AddComponent<TextMeshProUGUI>();
            footerTmp.text = "<size=75%><color=#64748B>NANOBOT DEFENSE SYSTEM - MOBILE COMPUTING</color></size>";
            footerTmp.fontSize = 13f;
            footerTmp.alignment = TextAlignmentOptions.Center;
            if (font != null) footerTmp.font = font;
            footerTmp.raycastTarget = false;

            // 8. Pulsante "CLOSE"
            var btnGo = new GameObject("CloseButton");
            btnGo.transform.SetParent(cardGo.transform, false);

            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot     = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(0f, -270f);
            btnRt.sizeDelta        = new Vector2(400f, 85f);

            var btnImg = btnGo.AddComponent<Image>();
            Sprite buttonFrame = Resources.Load<Sprite>("Sprites/ui_button_frame");
            if (buttonFrame != null)
            {
                btnImg.sprite = buttonFrame;
                btnImg.color  = LED_VIOLET_NEON;
            }
            else
            {
                btnImg.color = LED_VIOLET_NEON;
            }
            btnImg.raycastTarget = true;

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.normalColor      = LED_VIOLET_NEON;
            colors.highlightedColor = new Color(1.0f, 0.65f, 1.0f, 1.0f);
            colors.pressedColor     = new Color(0.60f, 0.15f, 0.80f, 1.0f);
            colors.selectedColor    = LED_VIOLET_NEON;
            btn.colors = colors;
            btn.onClick.AddListener(dialog.Close);
            dialog._closeButton = btn;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "CLOSE";
            tmp.fontSize = 22f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = LED_VIOLET_TEXT;
            tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;
            tmp.raycastTarget = false;

            dialogGo.SetActive(false);
            return dialog;
        }

        // ── Generazione Sprite Tondo Semitrasparente (Zero .meta) ─────────────

        public static Sprite CreateCircleIconSprite(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            Color[] pixels = new Color[resolution * resolution];
            float center   = resolution * 0.5f;
            float invR     = 1f / center;

            Color borderCyan = new Color(0.0f, 0.95f, 1.0f, 0.90f);
            Color bgDark     = new Color(0.02f, 0.08f, 0.18f, 0.55f); // Semitrasparente

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = (x + 0.5f) - center;
                    float dy = (y + 0.5f) - center;
                    float r  = Mathf.Sqrt(dx * dx + dy * dy) * invR;

                    Color col = Color.clear;
                    if (r <= 1f)
                    {
                        float edgeFalloff = Mathf.Clamp01((1f - r) / 0.08f);
                        if (r >= 0.76f)
                        {
                            col = borderCyan;
                        }
                        else
                        {
                            col = bgDark;
                        }
                        col.a *= edgeFalloff;
                    }
                    pixels[y * resolution + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        // ── Generazione Sprite Pannello Popup 9-Sliced Cyber (Zero .meta) ───────

        public static Sprite CreatePopupPanelSprite(int res, int radius, int borderThickness)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            Color[] pixels = new Color[res * res];
            float c = (res - 1) * 0.5f;
            float innerHalf = c - radius;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x - c) - innerHalf);
                    float dy = Mathf.Max(0f, Mathf.Abs(y - c) - innerHalf);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float edgeDist = radius - dist;

                    if (edgeDist < -0.5f)
                    {
                        pixels[y * res + x] = Color.clear;
                    }
                    else
                    {
                        float alphaAA = Mathf.Clamp01(edgeDist + 0.5f);
                        Color col;
                        if (edgeDist < borderThickness)
                        {
                            // Bordo esterno neon ciano cyber
                            col = new Color(0.00f, 0.92f, 1.00f, 0.95f);
                        }
                        else if (edgeDist < borderThickness + 2.5f)
                        {
                            // Luce soffusa interna
                            col = new Color(0.05f, 0.40f, 0.85f, 0.45f);
                        }
                        else
                        {
                            // Sfondo cyber glass scuro con leggero gradiente verticale
                            float tY = (float)y / (res - 1);
                            col = Color.Lerp(
                                new Color(0.03f, 0.06f, 0.12f, 0.96f),
                                new Color(0.07f, 0.12f, 0.22f, 0.96f),
                                tY
                            );
                        }
                        col.a *= alphaAA;
                        pixels[y * res + x] = col;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            // 9-slice borders: i bordi di 32px non vengono scalati, il centro sì
            Vector4 sliceBorder = new Vector4(32, 32, 32, 32);
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, sliceBorder);
        }
    }
}
