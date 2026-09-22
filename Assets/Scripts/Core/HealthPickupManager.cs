using UnityEngine;
using MobileGame.Entities;
using MobileGame.Utilities;

namespace MobileGame.Core
{
    /// <summary>
    /// Gestisce il pool di drop curativi (Croce Verde Arcade).
    /// Pre-alloca gli oggetti in Awake: zero GC a runtime (AGENTS.md §4).
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public class HealthPickupManager : MonoBehaviour
    {
        private static HealthPickupManager _instance;

        public static HealthPickupManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("HealthPickupManager");
                    _instance = go.AddComponent<HealthPickupManager>();
                }
                return _instance;
            }
        }

        [Header("Configurazione")]
        [SerializeField] private int   _poolSize   = 15;
        [SerializeField] private float _healAmount = 15f;

        private ObjectPool<HealthCross>    _pool;
        private System.Action<HealthCross> _returnCrossCallback;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _returnCrossCallback = ReturnCross;

            var container = new GameObject("HealthCrossPool").transform;
            container.SetParent(transform);

            _pool = new ObjectPool<HealthCross>(
                factory:         () => CreateCross(container),
                onGet:           c  => c.gameObject.SetActive(true),
                onRelease:       c  => { c.gameObject.SetActive(false); c.transform.SetParent(container); },
                initialCapacity: _poolSize
            );
        }

        private HealthCross CreateCross(Transform container)
        {
            var go = new GameObject("HealthCross");
            go.transform.SetParent(container);
            go.SetActive(false);
            return go.AddComponent<HealthCross>();
        }

        // ── API pubblica ───────────────────────────────────────────────────────

        /// <summary>Genera una croce verde curativa alla posizione specificata.</summary>
        public void SpawnCross(Vector2 position)
        {
            if (_pool == null) return;
            HealthCross cross = _pool.Get();
            cross.transform.position = position;
            cross.Initialize(_healAmount, _returnCrossCallback);
        }

        private void ReturnCross(HealthCross cross) => _pool.Release(cross);
    }
}
