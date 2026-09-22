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
        [SerializeField] private float     _fireRate       = 1.5f;
        [SerializeField] private float     _detectionRange = 10f;
        [SerializeField] private LayerMask _enemyLayerMask;

        // Pre-allocati in Awake (AGENTS.md §4)
        private readonly Collider2D[]     _overlapBuffer  = new Collider2D[32];
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

            _returnCallback = ReturnProjectile;

            var container = new GameObject("ProjectilePool").transform;
            container.SetParent(transform);

            _pool = new ObjectPool<Projectile>(
                factory:         () => Instantiate(_projectilePrefab, container),
                onGet:           p  => p.gameObject.SetActive(true),
                onRelease:       p  => { p.gameObject.SetActive(false); p.transform.SetParent(container); },
                initialCapacity: _poolSize
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

            _fireTimer += Time.deltaTime;
            float interval = 1f / (_fireRate * _fireRateMultiplier);
            if (_fireTimer >= interval)
            {
                _fireTimer = 0f;
                TryShoot();
            }
        }

        // ── Sparo ──────────────────────────────────────────────────────────────

        private void TryShoot()
        {
            float range = _detectionRange * _rangeMultiplier;
            int count = Physics2D.OverlapCircle(
                transform.position, range, _enemyFilter, _overlapBuffer);

            if (count == 0) return;

            Transform nearest   = null;
            float     minDistSq = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_overlapBuffer[i] == null) continue;
                float distSq = ((Vector2)_overlapBuffer[i].transform.position
                                - (Vector2)transform.position).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest   = _overlapBuffer[i].transform;
                }
            }

            if (nearest == null) return;

            Vector2 baseDir = ((Vector2)nearest.position - (Vector2)transform.position).normalized;
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
                p.transform.position = transform.position;
                p.SetDamage(currentDamage, _pierceCount);
                p.Launch(shootDir, _returnCallback, isCrit);
            }

            AudioManager.Instance?.PlayShoot();
        }

        // ── API upgrade ────────────────────────────────────────────────────────

        public void ApplyFireRateBonus(float multiplier) => _fireRateMultiplier *= (1f + multiplier);
        public void ApplyDamageBonus(float multiplier)   => _damageMultiplier   *= (1f + multiplier);
        public void ApplyRangeBonus(float multiplier)    => _rangeMultiplier    *= (1f + multiplier);

        public void AddProjectiles(int count)            => _projectilesPerShot += count;
        public void AddPierce(int count)                 => _pierceCount        += count;
        public void AddCritChance(float chance)          => _critChance         = Mathf.Clamp01(_critChance + chance);
        public void AddCritMultiplier(float bonus)       => _critMultiplier     += bonus;

        // ── Pool ───────────────────────────────────────────────────────────────

        private void ReturnProjectile(Projectile p) => _pool.Release(p);
    }
}
