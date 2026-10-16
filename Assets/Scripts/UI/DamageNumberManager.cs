using UnityEngine;
using MobileGame.Utilities;
using TMPro;

namespace MobileGame.UI
{
    /// <summary>
    /// Gestore e pool globale per i numeri di danno fluttuanti in World Space.
    /// Zero allocazioni GC nei frame di gioco (AGENTS.md §4).
    /// </summary>
    public class DamageNumberManager : MonoBehaviour
    {
        public static DamageNumberManager Instance { get; private set; }

        [SerializeField] private int _poolCapacity = 35;

        private ObjectPool<DamageNumber>  _pool;
        private System.Action<DamageNumber> _returnCallback;
        private Transform                 _container;

        private static TMP_FontAsset s_fontAsset;

        public static TMP_FontAsset GetFontAsset()
        {
            if (s_fontAsset != null) return s_fontAsset;

            s_fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (s_fontAsset == null)
            {
                s_fontAsset = TMP_Settings.defaultFontAsset;
            }
            if (s_fontAsset == null)
            {
                s_fontAsset = Resources.Load<TMP_FontAsset>("Fonts/PressStart2P-Regular SDF");
            }
            return s_fontAsset;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _container = new GameObject("DamageNumbersPool").transform;
            _container.SetParent(transform);

            _returnCallback = ReturnToPool;

            _pool = new ObjectPool<DamageNumber>(
                factory: () =>
                {
                    var go = new GameObject("DamageNumber");
                    go.SetActive(false); // Inattivo prima di aggiungere TextMeshPro per evitare HandleUtility mesh generation warning
                    go.transform.SetParent(_container);

                    var tmp = go.AddComponent<TextMeshPro>();
                    var font = GetFontAsset();
                    if (font != null) tmp.font = font;

                    tmp.alignment    = TextAlignmentOptions.Center;
                    tmp.sortingOrder = 10;
                    tmp.outlineWidth = 0.28f;
                    tmp.outlineColor = Color.black;

                    var dn = go.AddComponent<DamageNumber>();
                    return dn;
                },
                onGet:     dn => dn.gameObject.SetActive(true),
                onRelease: dn =>
                {
                    dn.gameObject.SetActive(false);
                    dn.transform.SetParent(_container);
                },
                initialCapacity: _poolCapacity
            );
        }

        private void ReturnToPool(DamageNumber dn)
        {
            _pool?.Release(dn);
        }

        /// <summary>
        /// Mostra un numero di danno fluttuante nella posizione specificata.
        /// Zero allocazioni GC nei loop (AGENTS.md §4).
        /// </summary>
        public static void Spawn(Vector2 position, int damage, bool isCrit = false)
        {
            if (Instance == null)
            {
                var go = new GameObject("DamageNumberManager");
                go.AddComponent<DamageNumberManager>();
            }

            if (Instance != null && Instance._pool != null)
            {
                DamageNumber dn = Instance._pool.Get();
                dn.Initialize(position, damage, isCrit, Instance._returnCallback);
            }
        }

        /// <summary>
        /// Mostra un popup verde neon di guarigione fluttuante (es. +15 HP).
        /// Zero allocazioni GC nei loop (AGENTS.md §4).
        /// </summary>
        public static void SpawnHeal(Vector2 position, int healAmount)
        {
            if (Instance == null)
            {
                var go = new GameObject("DamageNumberManager");
                go.AddComponent<DamageNumberManager>();
            }

            if (Instance != null && Instance._pool != null)
            {
                DamageNumber dn = Instance._pool.Get();
                dn.InitializeHeal(position, healAmount, Instance._returnCallback);
            }
        }
    }
}
