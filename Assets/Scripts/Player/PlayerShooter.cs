using UnityEngine;
using MobileGame.Audio;
using MobileGame.Utilities;
using MobileGame.Entities;
using MobileGame.Core;

namespace MobileGame.Player
{
    /// <summary>
    /// Spara automaticamente al nemico più vicino con un ObjectPool di Projectile.
    /// Espone metodi upgrade per danno, cadenza e range (AGENTS.md §4).
    /// </summary>
    public class PlayerShooter : MonoBehaviour
    {
        public static PlayerShooter Instance { get; private set; }

        [Header("Proiettile")]
        [SerializeField] private Projectile _projectilePrefab;
        [SerializeField] private int        _poolSize       = 20;
        [SerializeField] private float      _baseDamage     = 25f;

        [Header("Fuoco")]
        [SerializeField] private float     _fireRate       = 1.8f;
        [SerializeField] private float     _detectionRange = 10f;
        [SerializeField] private LayerMask _enemyLayerMask;

        // Pre-allocati in Awake (AGENTS.md §4)
        private readonly Collider2D[]     _overlapBuffer  = new Collider2D[96];
        private ObjectPool<Projectile>    _pool;
        private System.Action<Projectile> _returnCallback;
        private ContactFilter2D           _enemyFilter;

        // Moltiplicatori upgrade
        private float _damageMultiplier   = 1f;
        private float _fireRateMultiplier = 1f;
        private float _rangeMultiplier    = 1f;

        // Nuove statistiche potenziamenti avanzati
        private int   _projectilesPerShot = 1;
        private int   _pierceCount        = 1;
        private float _critChance         = 0f;
        private float _critMultiplier     = 2f;

        private float _fireTimer;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Garantisce che il raggio copra l'intero schermo visibile sul telefono
            if (_detectionRange < 7.5f) _detectionRange = 8.5f;

            _returnCallback = ReturnProjectile;

            var container = new GameObject("ProjectilePool").transform;
            container.SetParent(transform);

            int initialCap = Mathf.Max(_poolSize, 64);
            _pool = new ObjectPool<Projectile>(
                factory:         () => Instantiate(_projectilePrefab, container),
                onGet:           null,
                onRelease:       p  => { p.gameObject.SetActive(false); p.transform.SetParent(container); },
                initialCapacity: initialCap
            );

            _enemyFilter.SetLayerMask(_enemyLayerMask);
            _enemyFilter.useTriggers = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Update ─────────────────────────────────────────────────────────────

        private void Update()
        {
            if (GameManager.Instance?.CurrentState != GameState.Playing) return;

            float interval = 1f / (_fireRate * _fireRateMultiplier);
            _fireTimer += Time.deltaTime;

            if (_fireTimer >= interval)
            {
                if (TryShoot())
                {
                    _fireTimer = 0f;
                }
                else
                {
                    // Se non ci sono nemici a tiro, mantiene l'arma carica e pronta
                    // a sparare all'istante non appena un nemico entra nel raggio visivo!
                    _fireTimer = interval;
                }
            }
        }

        // ── Sparo ──────────────────────────────────────────────────────────────

        private bool TryShoot()
        {
            float range = _detectionRange * _rangeMultiplier;
            int count = Physics2D.OverlapCircle(
                transform.position, range, _enemyFilter, _overlapBuffer);

            if (count == 0) return false;

            Transform nearest   = null;
            float     minDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (col == null || !col.enabled) continue;

                // Escludi nemici già morti o in animazione di sgretolamento
                HealthComponent hp = col.GetComponent<HealthComponent>();
                if (hp != null && hp.IsDead) continue;

                float distSq = ((Vector2)col.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest   = col.transform;
                }
            }

            if (nearest == null) return false;

            Vector2 diff = (Vector2)nearest.position - (Vector2)transform.position;
            Vector2 baseDir = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.up;
            bool isCrit = Random.value < _critChance;
            float currentDamage = _baseDamage * _damageMultiplier * (isCrit ? _critMultiplier : 1f);

            int shotCount = Mathf.Clamp(_projectilesPerShot, 1, 8);
            float spreadAngle = 12f; // gradi di ventaglio per proiettile
            float startAngle = -((shotCount - 1) * spreadAngle) / 2f;

            for (int i = 0; i < shotCount; i++)
            {
                float angle = startAngle + i * spreadAngle;
                Vector2 shootDir = Quaternion.Euler(0f, 0f, angle) * baseDir;

                Projectile p = _pool.Get();
                p.PrepareAndLaunch(transform.position, shootDir, _returnCallback, currentDamage, _pierceCount, isCrit);
            }

            AudioManager.Instance?.PlayShoot();
            return true;
        }

        // ── API upgrade ────────────────────────────────────────────────────────

        public void ApplyFireRateBonus(float multiplier) => _fireRateMultiplier = Mathf.Min(2.5f, _fireRateMultiplier * (1f + multiplier));
        public void ApplyDamageBonus(float multiplier)   => _damageMultiplier   = Mathf.Min(2.8f, _damageMultiplier * (1f + multiplier));
        public void ApplyRangeBonus(float multiplier)    => _rangeMultiplier    = Mathf.Min(1.8f, _rangeMultiplier * (1f + multiplier));

        public void AddProjectiles(int count)            => _projectilesPerShot = Mathf.Min(5, _projectilesPerShot + count);
        public void AddPierce(int count)                 => _pierceCount        = Mathf.Min(4, _pierceCount + count);
        public void AddCritChance(float chance)          => _critChance         = Mathf.Clamp(_critChance + chance, 0f, 0.60f);
        public void AddCritMultiplier(float bonus)       => _critMultiplier     = Mathf.Min(3.5f, _critMultiplier + bonus);

        // ── Pool ───────────────────────────────────────────────────────────────

        private void ReturnProjectile(Projectile p) => _pool.Release(p);
    }
}
