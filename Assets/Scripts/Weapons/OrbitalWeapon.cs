using System.Collections.Generic;
using UnityEngine;

namespace MobileGame.Weapons
{
    /// <summary>
    /// Arma orbitale stile Vampire Survivors (King Bible/Garlic): sfere energetiche
    /// che ruotano costantemente attorno al nano-bot proteggendolo e falciando i nemici.
    /// Rotazione pura in Update: zero allocazioni GC (AGENTS.md §4).
    /// </summary>
    public class OrbitalWeapon : MonoBehaviour
    {
        public static OrbitalWeapon Instance { get; private set; }

        [Header("Prefab Sfera")]
        [SerializeField] private GameObject _orbitalPrefab;

        [Header("Parametri Base")]
        [SerializeField] private float _baseRadius        = 1.8f;
        [SerializeField] private float _baseRotationSpeed = 160f;
        [SerializeField] private float _baseDamage        = 20f;

        public int  Level      { get; private set; } = 0; // 0 = non ancora sbloccata
        public int  MaxLevel   => 4;
        public bool IsUnlocked => Level > 0;

        private Transform                     _orbitContainer;
        private readonly List<OrbitalProjectile> _activeOrbitals = new List<OrbitalProjectile>(8);

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var containerGO = new GameObject("OrbitContainer");
            containerGO.transform.SetParent(transform, false);
            _orbitContainer = containerGO.transform;
        }

        private void Update()
        {
            if (!IsUnlocked || _orbitContainer == null) return;

            // Rotazione continua in Update su struct (zero alloc)
            float currentSpeed = _baseRotationSpeed + (Level - 1) * 30f;
            _orbitContainer.Rotate(0f, 0f, currentSpeed * Time.deltaTime);
        }

        // ── Gestione Livelli & Upgrade ─────────────────────────────────────────

        public void UnlockOrUpgrade()
        {
            if (Level >= MaxLevel) return;
            Level++;
            RebuildOrbitals();
        }

        private void RebuildOrbitals()
        {
            // Pulisci sfere attuali
            for (int i = 0; i < _activeOrbitals.Count; i++)
            {
                if (_activeOrbitals[i] != null)
                    Destroy(_activeOrbitals[i].gameObject);
            }
            _activeOrbitals.Clear();

            if (_orbitalPrefab == null) return;

            // Numero sfere in base al livello: Lv1=2, Lv2=3, Lv3=4, Lv4=5
            int count        = Level + 1;
            float currentRadius = _baseRadius + (Level - 1) * 0.15f;
            float currentDmg    = _baseDamage + (Level - 1) * 7f;

            float stepAngle = (2f * Mathf.PI) / count;

            for (int i = 0; i < count; i++)
            {
                float angle = i * stepAngle;
                Vector3 localPos = new Vector3(
                    Mathf.Cos(angle) * currentRadius,
                    Mathf.Sin(angle) * currentRadius,
                    0f
                );

                GameObject orbGO = Instantiate(_orbitalPrefab, _orbitContainer);
                orbGO.transform.localPosition = localPos;

                var projectile = orbGO.GetComponent<OrbitalProjectile>();
                if (projectile == null)
                    projectile = orbGO.AddComponent<OrbitalProjectile>();

                projectile.SetDamage(currentDmg);
                _activeOrbitals.Add(projectile);
            }
        }
    }
}
