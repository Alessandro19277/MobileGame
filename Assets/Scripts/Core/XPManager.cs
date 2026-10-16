using UnityEngine;
using MobileGame.Audio;
using MobileGame.Entities;
using MobileGame.Utilities;
using MobileGame.UI;

namespace MobileGame.Core
{
    /// <summary>
    /// Gestisce XP, livelli e il pool di gemme XP.
    /// Al level up pausa il gioco e notifica LevelUpScreen.
    /// Zero alloc in AddXP: solo aritmetica int/float (AGENTS.md §4).
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public class XPManager : MonoBehaviour
    {
        public static XPManager Instance { get; private set; }

        [Header("XP Gem Pool")]
        [SerializeField] private XPGem _gemPrefab;
        [SerializeField] private int   _gemPoolSize = 50;

        [Header("XP per gem")]
        [SerializeField] private int _xpPerGem = 10;

        public int   CurrentXP      { get; private set; }
        public int   CurrentLevel   { get; private set; } = 1;
        public int   XPForNextLevel { get; private set; }
        public float MagnetMultiplier { get; set; } = 1f; // modificato dagli upgrade

        // (current, max) — zero alloc quando invocati
        public event System.Action<int, int> OnXPChanged;
        public event System.Action<int>      OnLevelUp;

        private ObjectPool<XPGem>    _gemPool;
        private System.Action<XPGem> _returnGemCallback; // pre-allocato in Awake

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _returnGemCallback = ReturnGem;
            XPForNextLevel     = CalculateThreshold(1);

            var container = new GameObject("XPGemPool").transform;
            container.SetParent(transform);

            _gemPool = new ObjectPool<XPGem>(
                factory:         () => Instantiate(_gemPrefab, container),
                onGet:           g  => g.gameObject.SetActive(true),
                onRelease:       g  => { g.gameObject.SetActive(false); g.transform.SetParent(container); },
                initialCapacity: _gemPoolSize
            );
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── API pubblica ───────────────────────────────────────────────────────

        public void AddXP(int amount)
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            CurrentXP += amount;
            AudioManager.Instance?.PlayXPPickup();
            OnXPChanged?.Invoke(CurrentXP, XPForNextLevel);

            if (CurrentXP >= XPForNextLevel) LevelUp();
        }

        public void SpawnGem(Vector2 position)
        {
            if (ArenaBoundary.Instance != null)
            {
                position = ArenaBoundary.Instance.ClampPosition(position, 0.4f);
            }
            XPGem gem = _gemPool.Get();
            gem.transform.position = position;
            gem.Initialize(_xpPerGem, _returnGemCallback);
        }

        // ── Privato ────────────────────────────────────────────────────────────

        private void LevelUp()
        {
            CurrentXP      -= XPForNextLevel;
            CurrentLevel++;
            XPForNextLevel  = CalculateThreshold(CurrentLevel);

            AudioManager.Instance?.PlayLevelUp();
            OnLevelUp?.Invoke(CurrentLevel);
            Time.timeScale = 0f;               // pausa
            LevelUpScreen.Instance?.Show();
        }

        private void ReturnGem(XPGem gem) => _gemPool.Release(gem);

        /// <summary>
        /// Curva di progressione XP bilanciata:
        /// Il livello 2 richiede 35 XP (~3-4 nemici abbattuti, circa 10-12s di gioco),
        /// poi cresce in modo stimolante senza sovraccaricare subito il giocatore di carte.
        /// </summary>
        private static int CalculateThreshold(int level)
        {
            if (level <= 1) return 35;
            if (level == 2) return 80;
            if (level == 3) return 150;
            if (level == 4) return 240;
            return Mathf.RoundToInt(45f * Mathf.Pow(level, 1.38f));
        }
    }
}
