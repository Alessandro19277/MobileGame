using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MobileGame.Audio;
using MobileGame.Core;
using MobileGame.Player;
using MobileGame.Weapons;

namespace MobileGame.UI
{
    public enum UpgradeRarity
    {
        Common,     // Verde: #00E676
        Rare,       // Blu elettrico: #00B0FF
        Epic,       // Viola neon: #D500F9
        Legendary   // Oro: #FFD700
    }

    /// <summary>
    /// Gestisce la schermata di potenziamento al Level Up.
    /// Sistema a 4 Rarità (Comune, Raro, Epico, Leggendario) scalate in base alla Wave.
    /// Zero allocazioni heap in loop (AGENTS.md §4).
    /// </summary>
    public class LevelUpScreen : MonoBehaviour
    {
        public static LevelUpScreen Instance { get; private set; }

        [Header("Pannello principale")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text   _levelText;

        [Header("Carta 1")]
        [SerializeField] private Button   _card1Button;
        [SerializeField] private TMP_Text _card1Title;
        [SerializeField] private TMP_Text _card1Desc;
        [SerializeField] private TMP_Text _card1RarityText;
        [SerializeField] private Image    _card1Border;

        [Header("Carta 2")]
        [SerializeField] private Button   _card2Button;
        [SerializeField] private TMP_Text _card2Title;
        [SerializeField] private TMP_Text _card2Desc;
        [SerializeField] private TMP_Text _card2RarityText;
        [SerializeField] private Image    _card2Border;

        [Header("Carta 3")]
        [SerializeField] private Button   _card3Button;
        [SerializeField] private TMP_Text _card3Title;
        [SerializeField] private TMP_Text _card3Desc;
        [SerializeField] private TMP_Text _card3RarityText;
        [SerializeField] private Image    _card3Border;

        private readonly StringBuilder _sb = new StringBuilder(64);

        public struct UpgradeOption
        {
            public string         Title;
            public string         Description;
            public UpgradeRarity  Rarity;
            public System.Action  Apply;
        }

        private UpgradeOption[]          _allUpgrades;
        private readonly UpgradeOption[] _currentOptions = new UpgradeOption[3];
        private readonly List<int>       _candidateIndices = new List<int>(32);
        private readonly int[]           _selectedIndices = new int[3];

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_panel != null) _panel.SetActive(false);

            ConfigureCardLayouts();
            InitializeUpgrades();

            if (_card1Button != null) _card1Button.onClick.AddListener(() => SelectUpgrade(0));
            if (_card2Button != null) _card2Button.onClick.AddListener(() => SelectUpgrade(1));
            if (_card3Button != null) _card3Button.onClick.AddListener(() => SelectUpgrade(2));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            _card1Button?.onClick.RemoveAllListeners();
            _card2Button?.onClick.RemoveAllListeners();
            _card3Button?.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// Ingrandisce e posiziona le tre card al centro dello schermo (Canvas 1080x1920 portrait)
        /// e imposta i campi testo per massima visibilità.
        /// </summary>
        private void ConfigureCardLayouts()
        {
            // Posizioni Y per le 3 carte centrate: +300, 0, -300 (altezza 275 ciascuna, larghezza 950)
            SetupSingleCardLayout(_card1Button, _card1Title, _card1Desc, _card1Border, 300f);
            SetupSingleCardLayout(_card2Button, _card2Title, _card2Desc, _card2Border, 0f);
            SetupSingleCardLayout(_card3Button, _card3Title, _card3Desc, _card3Border, -300f);

            // Titolo superiore ("LEVEL UP!") posizionato sopra le card
            if (_levelText != null)
            {
                RectTransform levelRt = _levelText.rectTransform;
                levelRt.anchorMin = new Vector2(0.5f, 0.5f);
                levelRt.anchorMax = new Vector2(0.5f, 0.5f);
                levelRt.pivot = new Vector2(0.5f, 0.5f);
                levelRt.anchoredPosition = new Vector2(0f, 520f);
                levelRt.sizeDelta = new Vector2(950f, 95f);
                _levelText.fontSize = 56;
                _levelText.fontStyle = FontStyles.Bold;
                _levelText.color = new Color(1f, 0.88f, 0.2f); // Giallo oro arcade
                _levelText.outlineColor = Color.black;
                _levelText.outlineWidth = 0.32f;
            }
        }

        private static void SetupSingleCardLayout(Button btn, TMP_Text titleTxt, TMP_Text descTxt, Image borderImg, float yPos)
        {
            if (btn == null) return;

            RectTransform cardRt = btn.GetComponent<RectTransform>();
            if (cardRt != null)
            {
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = new Vector2(0f, yPos);
                cardRt.sizeDelta = new Vector2(950f, 275f);
            }

            // Sfondo scuro e profondo cyberpunk / sci-fi arcade: contrasto perfetto con scritte bianche
            Image btnImg = btn.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);
            }

            // Configura o crea bordo neon per la carta
            if (borderImg == null && cardRt != null)
            {
                // Se non c'è già un bordo, aggiunge o configura una cornice Outline / child border
                Outline outline = btn.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = btn.gameObject.AddComponent<Outline>();
                }
                outline.effectDistance = new Vector2(5f, -5f);
                outline.useGraphicAlpha = false;
            }

            // Formattazione Titolo: bianco con contorno nero, con spaziatura generosa
            if (titleTxt != null)
            {
                RectTransform titleRt = titleTxt.rectTransform;
                titleRt.anchorMin = new Vector2(0.5f, 0.5f);
                titleRt.anchorMax = new Vector2(0.5f, 0.5f);
                titleRt.pivot = new Vector2(0.5f, 0.5f);
                titleRt.anchoredPosition = new Vector2(0f, 52f);
                titleRt.sizeDelta = new Vector2(900f, 130f);

                titleTxt.color = Color.white;
                titleTxt.outlineColor = Color.black;
                titleTxt.outlineWidth = 0.34f;
                titleTxt.fontSize = 38;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.alignment = TextAlignmentOptions.Center;
                titleTxt.enableAutoSizing = true;
                titleTxt.fontSizeMin = 24;
                titleTxt.fontSizeMax = 44;
                titleTxt.lineSpacing = 22f;
                titleTxt.paragraphSpacing = 0f;
            }

            // Formattazione Descrizione: bianco con contorno nero, ben distanziata sotto
            if (descTxt != null)
            {
                RectTransform descRt = descTxt.rectTransform;
                descRt.anchorMin = new Vector2(0.5f, 0.5f);
                descRt.anchorMax = new Vector2(0.5f, 0.5f);
                descRt.pivot = new Vector2(0.5f, 0.5f);
                descRt.anchoredPosition = new Vector2(0f, -50f);
                descRt.sizeDelta = new Vector2(880f, 95f);

                descTxt.color = Color.white;
                descTxt.outlineColor = Color.black;
                descTxt.outlineWidth = 0.28f;
                descTxt.fontSize = 28;
                descTxt.alignment = TextAlignmentOptions.Center;
                descTxt.enableAutoSizing = true;
                descTxt.fontSizeMin = 20;
                descTxt.fontSizeMax = 32;
                descTxt.lineSpacing = 16f;
            }
        }

        // ── Inizializzazione Catalogo Potenziamenti ────────────────────────────

        private void InitializeUpgrades()
        {
            _allUpgrades = new UpgradeOption[]
            {
                // ── 1. COMUNI (Verde: #00E676) ─────────────────────────────────
                new UpgradeOption
                {
                    Title       = "MICRO-TURBINE",
                    Description = "+20% Velocità di Movimento",
                    Rarity      = UpgradeRarity.Common,
                    Apply       = () => PlayerController.Instance?.ApplySpeedBonus(0.20f)
                },
                new UpgradeOption
                {
                    Title       = "SINTESI RAPIDA",
                    Description = "+25% Cadenza di Fuoco",
                    Rarity      = UpgradeRarity.Common,
                    Apply       = () => PlayerShooter.Instance?.ApplyFireRateBonus(0.25f)
                },
                new UpgradeOption
                {
                    Title       = "DARDI IONICI",
                    Description = "+20% Danno Proiettili",
                    Rarity      = UpgradeRarity.Common,
                    Apply       = () => PlayerShooter.Instance?.ApplyDamageBonus(0.20f)
                },
                new UpgradeOption
                {
                    Title       = "NANO-MAGNETE",
                    Description = "+40% Raggio Raccolta XP",
                    Rarity      = UpgradeRarity.Common,
                    Apply       = () => PlayerController.Instance?.ApplyMagnetBonus(0.40f)
                },
                new UpgradeOption
                {
                    Title       = "MEMBRANA CELLULARE",
                    Description = "+30 Salute Massima (Espande la barra vita)",
                    Rarity      = UpgradeRarity.Common,
                    Apply       = () =>
                    {
                        var p = GameObject.FindWithTag("Player");
                        p?.GetComponent<HealthComponent>()?.IncreaseMaxHealth(30f);
                    }
                },
                new UpgradeOption
                {
                    Title       = "BIO-SCANNER",
                    Description = "+30% Gittata di Tiro",
                    Rarity      = UpgradeRarity.Common,
                    Apply       = () => PlayerShooter.Instance?.ApplyRangeBonus(0.30f)
                },

                // ── 2. RARI (Blu elettrico: #00B0FF) ───────────────────────────
                new UpgradeOption
                {
                    Title       = "MULTISPARO DOPPIO",
                    Description = "Spara 2 dardi ionici contemporaneamente!",
                    Rarity      = UpgradeRarity.Rare,
                    Apply       = () => PlayerShooter.Instance?.AddProjectiles(1)
                },
                new UpgradeOption
                {
                    Title       = "DARDI PERFORANTI",
                    Description = "I proiettili trapassano +1 nemico!",
                    Rarity      = UpgradeRarity.Rare,
                    Apply       = () => PlayerShooter.Instance?.AddPierce(1)
                },
                new UpgradeOption
                {
                    Title       = "SFERE ORBITALI",
                    Description = "Barriera di sfere rotanti difensive",
                    Rarity      = UpgradeRarity.Rare,
                    Apply       = () => OrbitalWeapon.Instance?.UnlockOrUpgrade()
                },
                new UpgradeOption
                {
                    Title       = "IMPULSO SHOCKWAVE",
                    Description = "Onda d'urto a 360° periodica",
                    Rarity      = UpgradeRarity.Rare,
                    Apply       = () => ShockwaveWeapon.Instance?.UnlockOrUpgrade()
                },
                new UpgradeOption
                {
                    Title       = "BIO-RIGENERAZIONE",
                    Description = "Rigenera passivamente +1 HP/sec (max 2.5 HP/sec)",
                    Rarity      = UpgradeRarity.Rare,
                    Apply       = () =>
                    {
                        var p = GameObject.FindWithTag("Player");
                        var h = p?.GetComponent<HealthComponent>();
                        if (h != null) h.RegenPerSecond = Mathf.Min(2.5f, h.RegenPerSecond + 1.0f);
                    }
                },
                new UpgradeOption
                {
                    Title       = "SOVRACCARICO CINETICO",
                    Description = "+35% Danno e +15% Movimento",
                    Rarity      = UpgradeRarity.Rare,
                    Apply       = () =>
                    {
                        PlayerShooter.Instance?.ApplyDamageBonus(0.35f);
                        PlayerController.Instance?.ApplySpeedBonus(0.15f);
                    }
                },

                // ── 3. EPICI (Viola neon: #D500F9) ─────────────────────────────
                new UpgradeOption
                {
                    Title       = "MULTISPARO TRIPLO",
                    Description = "Spara 3 dardi a ventaglio angolare!",
                    Rarity      = UpgradeRarity.Epic,
                    Apply       = () => PlayerShooter.Instance?.AddProjectiles(2)
                },
                new UpgradeOption
                {
                    Title       = "COLPO CRITICO IONICO",
                    Description = "+25% Probabilità Critico (2x Danno dorato)",
                    Rarity      = UpgradeRarity.Epic,
                    Apply       = () => PlayerShooter.Instance?.AddCritChance(0.25f)
                },
                new UpgradeOption
                {
                    Title       = "NANO-CORAZZA TITANICA",
                    Description = "-15% a tutti i danni subiti (max 35%) + 35 HP Max",
                    Rarity      = UpgradeRarity.Epic,
                    Apply       = () =>
                    {
                        var p = GameObject.FindWithTag("Player");
                        var h = p?.GetComponent<HealthComponent>();
                        if (h != null)
                        {
                            h.DamageReductionPercent = Mathf.Clamp(h.DamageReductionPercent + 0.15f, 0f, 0.35f);
                            h.IncreaseMaxHealth(35f);
                        }
                    }
                },
                new UpgradeOption
                {
                    Title       = "SOVRACCARICO ORBITALE",
                    Description = "+2 Sfere orbitali e raggio difensivo esteso",
                    Rarity      = UpgradeRarity.Epic,
                    Apply       = () =>
                    {
                        OrbitalWeapon.Instance?.UnlockOrUpgrade();
                        OrbitalWeapon.Instance?.UnlockOrUpgrade();
                    }
                },
                new UpgradeOption
                {
                    Title       = "IPER-MAGNETE QUANTICO",
                    Description = "+120% Raggio Magnete e +15% Movimento",
                    Rarity      = UpgradeRarity.Epic,
                    Apply       = () =>
                    {
                        PlayerController.Instance?.ApplyMagnetBonus(1.20f);
                        PlayerController.Instance?.ApplySpeedBonus(0.15f);
                    }
                },

                // ── 4. LEGGENDARI (Oro: #FFD700) ───────────────────────────────
                new UpgradeOption
                {
                    Title       = "TEMPESTA DI PLASMA",
                    Description = "+3 Dardi a ventaglio, +2 perforazione e +40% cadenza!",
                    Rarity      = UpgradeRarity.Legendary,
                    Apply       = () =>
                    {
                        PlayerShooter.Instance?.AddProjectiles(3);
                        PlayerShooter.Instance?.AddPierce(2);
                        PlayerShooter.Instance?.ApplyFireRateBonus(0.40f);
                    }
                },
                new UpgradeOption
                {
                    Title       = "PROTOCOLLO IMMORTALE",
                    Description = "+1 Rianimazione d'emergenza (max 1) e +1.5 HP/sec!",
                    Rarity      = UpgradeRarity.Legendary,
                    Apply       = () =>
                    {
                        var p = GameObject.FindWithTag("Player");
                        var h = p?.GetComponent<HealthComponent>();
                        if (h != null)
                        {
                            h.RevivesRemaining = Mathf.Min(1, h.RevivesRemaining + 1);
                            h.RegenPerSecond   = Mathf.Min(2.5f, h.RegenPerSecond + 1.5f);
                        }
                    }
                },
                new UpgradeOption
                {
                    Title       = "SOVRACCARICO NUCLEARE",
                    Description = "+30% Probabilità Critico e Danno Critico moltiplicato!",
                    Rarity      = UpgradeRarity.Legendary,
                    Apply       = () =>
                    {
                        PlayerShooter.Instance?.AddCritChance(0.30f);
                        PlayerShooter.Instance?.AddCritMultiplier(1.5f);
                    }
                },
                new UpgradeOption
                {
                    Title       = "BARRIERA ASSOLUTA",
                    Description = "Sfere + Shockwave potenziati e -10% Danno subito!",
                    Rarity      = UpgradeRarity.Legendary,
                    Apply       = () =>
                    {
                        OrbitalWeapon.Instance?.UnlockOrUpgrade();
                        ShockwaveWeapon.Instance?.UnlockOrUpgrade();
                        var p = GameObject.FindWithTag("Player");
                        var h = p?.GetComponent<HealthComponent>();
                        if (h != null)
                        {
                            h.DamageReductionPercent = Mathf.Clamp(h.DamageReductionPercent + 0.10f, 0f, 0.35f);
                        }
                    }
                }
            };
        }

        // ── API pubblica ───────────────────────────────────────────────────────

        /// <summary>
        /// Mostra 3 carte casuali ponderate per rarità in base alla Wave corrente.
        /// </summary>
        public void Show()
        {
            int currentWave = GameManager.Instance != null ? GameManager.Instance.Wave : 1;

            _selectedIndices[0] = -1;
            _selectedIndices[1] = -1;
            _selectedIndices[2] = -1;

            for (int i = 0; i < 3; i++)
            {
                UpgradeRarity rarity = RollRarity(currentWave);
                int chosenIdx = PickUpgradeIndex(rarity, _selectedIndices, i);
                _selectedIndices[i] = chosenIdx;
                _currentOptions[i]  = _allUpgrades[chosenIdx];
            }

            SetCard(_card1Button, _card1Title, _card1Desc, _card1RarityText, _card1Border, _currentOptions[0]);
            SetCard(_card2Button, _card2Title, _card2Desc, _card2RarityText, _card2Border, _currentOptions[1]);
            SetCard(_card3Button, _card3Title, _card3Desc, _card3RarityText, _card3Border, _currentOptions[2]);

            if (_levelText != null && XPManager.Instance != null)
            {
                _sb.Clear();
                _sb.Append("★ LEVEL UP! ★  LIVELLO ").Append(XPManager.Instance.CurrentLevel);
                _levelText.SetText(_sb);
            }

            if (_panel != null) _panel.SetActive(true);
            Time.timeScale = 0f; // Pausa gioco
            MobileGame.Utilities.HapticManager.LightImpact();
        }

        // ── Selezione e Ponderazione Rarità ────────────────────────────────────

        private UpgradeRarity RollRarity(int wave)
        {
            float roll = Random.value;

            if (wave <= 1)
            {
                // Wave 1: 80% Comune, 20% Raro
                return roll < 0.80f ? UpgradeRarity.Common : UpgradeRarity.Rare;
            }
            if (wave <= 2)
            {
                // Wave 2: 65% Comune, 30% Raro, 5% Epico
                if (roll < 0.65f) return UpgradeRarity.Common;
                if (roll < 0.95f) return UpgradeRarity.Rare;
                return UpgradeRarity.Epic;
            }
            if (wave <= 4)
            {
                // Wave 3-4: 45% Comune, 35% Raro, 15% Epico, 5% Leggendario
                if (roll < 0.45f) return UpgradeRarity.Common;
                if (roll < 0.80f) return UpgradeRarity.Rare;
                if (roll < 0.95f) return UpgradeRarity.Epic;
                return UpgradeRarity.Legendary;
            }

            // Wave 5+: 30% Comune, 35% Raro, 23% Epico, 12% Leggendario
            if (roll < 0.30f) return UpgradeRarity.Common;
            if (roll < 0.65f) return UpgradeRarity.Rare;
            if (roll < 0.88f) return UpgradeRarity.Epic;
            return UpgradeRarity.Legendary;
        }

        private int PickUpgradeIndex(UpgradeRarity rarity, int[] alreadyChosen, int chosenCount)
        {
            _candidateIndices.Clear();

            // Cerca prima upgrade con la rarità richiesta non ancora scelti
            for (int i = 0; i < _allUpgrades.Length; i++)
            {
                if (_allUpgrades[i].Rarity != rarity) continue;

                bool chosen = false;
                for (int c = 0; c < chosenCount; c++)
                {
                    if (alreadyChosen[c] == i) { chosen = true; break; }
                }

                if (!chosen) _candidateIndices.Add(i);
            }

            // Fallback se tutti gli upgrade di quella rarità sono già estratti
            if (_candidateIndices.Count == 0)
            {
                for (int i = 0; i < _allUpgrades.Length; i++)
                {
                    bool chosen = false;
                    for (int c = 0; c < chosenCount; c++)
                    {
                        if (alreadyChosen[c] == i) { chosen = true; break; }
                    }
                    if (!chosen) _candidateIndices.Add(i);
                }
            }

            if (_candidateIndices.Count == 0) return 0;
            return _candidateIndices[Random.Range(0, _candidateIndices.Count)];
        }

        // ── Privato ────────────────────────────────────────────────────────────

        private void SelectUpgrade(int index)
        {
            AudioManager.Instance?.PlayButtonClick();
            _currentOptions[index].Apply?.Invoke();
            if (_panel != null) _panel.SetActive(false);
            Time.timeScale = 1f; // Riprendi il gioco
        }

        private static void SetCard(Button btn, TMP_Text titleTxt, TMP_Text descTxt, TMP_Text rarityTxt, Image borderImg, UpgradeOption opt)
        {
            Color rarityColor = GetRarityColor(opt.Rarity);
            string rarityName = GetRarityName(opt.Rarity);

            if (rarityTxt != null)
            {
                rarityTxt.text = rarityName;
                rarityTxt.color = rarityColor;
                rarityTxt.outlineColor = Color.black;
                rarityTxt.outlineWidth = 0.25f;
            }

            if (titleTxt != null)
            {
                // Titolo bianco con outline nero; etichetta rarità stilizzata senza parentesi, ben visibile e distanziata
                if (rarityTxt == null)
                {
                    string hexColor = ColorUtility.ToHtmlStringRGB(rarityColor);
                    titleTxt.text = $"<size=110%><b><color=#{hexColor}>{rarityName}</color></b></size>\n<size=100%><b>{opt.Title}</b></size>";
                }
                else
                {
                    titleTxt.text = $"<b>{opt.Title}</b>";
                }
                titleTxt.color = Color.white;
                titleTxt.outlineColor = Color.black;
                titleTxt.outlineWidth = 0.34f;
            }

            if (descTxt != null)
            {
                descTxt.text = opt.Description;
                descTxt.color = Color.white;
                descTxt.outlineColor = Color.black;
                descTxt.outlineWidth = 0.28f;
            }

            // Sfondo scuro costante per contrasto ottimale
            if (btn != null)
            {
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);
                }

                // Tinge il contorno / outline della carta con il colore della rarità
                var outline = btn.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = rarityColor;
                    outline.effectDistance = new Vector2(4f, -4f);
                }
            }

            // Se esiste un'immagine bordo separata
            if (borderImg != null)
            {
                borderImg.color = rarityColor;
            }
        }

        public static Color GetRarityColor(UpgradeRarity rarity) => rarity switch
        {
            UpgradeRarity.Common    => new Color(0.0f, 0.90f, 0.46f), // Verde neon
            UpgradeRarity.Rare      => new Color(0.0f, 0.69f, 1.00f), // Blu elettrico
            UpgradeRarity.Epic      => new Color(0.84f, 0.00f, 1.00f), // Viola neon
            UpgradeRarity.Legendary => new Color(1.0f, 0.84f, 0.00f), // Oro
            _ => Color.white
        };

        public static string GetRarityName(UpgradeRarity rarity) => rarity switch
        {
            UpgradeRarity.Common    => "COMUNE",
            UpgradeRarity.Rare      => "RARO",
            UpgradeRarity.Epic      => "EPICO",
            UpgradeRarity.Legendary => "LEGGENDARIO",
            _ => string.Empty
        };

        public static string GetRarityTag(UpgradeRarity rarity) => rarity switch
        {
            UpgradeRarity.Common    => "<color=#00E676>COMUNE</color>",
            UpgradeRarity.Rare      => "<color=#00B0FF>RARO</color>",
            UpgradeRarity.Epic      => "<color=#D500F9>EPICO</color>",
            UpgradeRarity.Legendary => "<color=#FFD700>LEGGENDARIO</color>",
            _ => string.Empty
        };
    }
}
