using UnityEngine;

namespace MobileGame.Core
{
    /// <summary>
    /// Componente di salute riusabile per player, nemici e qualsiasi entità.
    /// Nessuna allocazione nei metodi pubblici: eventi C# standard, no boxing.
    /// </summary>
    public class HealthComponent : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;

        public float MaxHealth     => _maxHealth;
        public float CurrentHealth { get; private set; }
        public bool  IsDead        { get; private set; }

        public float DamageReductionPercent { get; set; } = 0f;
        public float RegenPerSecond         { get; set; } = 0f;
        public int   RevivesRemaining       { get; set; } = 0;

        // (current, max) — zero alloc quando invocati
        public event System.Action<float, float> OnHealthChanged;
        public event System.Action               OnDamaged;
        public event System.Action               OnDeath;

        private void Awake()
        {
            CurrentHealth = _maxHealth;
        }

        private void Update()
        {
            if (!IsDead && RegenPerSecond > 0f && CurrentHealth < _maxHealth)
            {
                Heal(RegenPerSecond * Time.deltaTime);
            }
        }

        // ── API pubblica ───────────────────────────────────────────────────────

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            if (DamageReductionPercent > 0f)
            {
                amount *= Mathf.Clamp01(1f - DamageReductionPercent);
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
            OnDamaged?.Invoke();

            if (CurrentHealth <= 0f)
            {
                if (RevivesRemaining > 0)
                {
                    RevivesRemaining--;
                    CurrentHealth = _maxHealth;
                    OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
                }
                else
                {
                    Die();
                }
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(_maxHealth, CurrentHealth + amount);
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        /// <summary>Reimposta la salute al massimo — usato dal pool quando si ricicla l'oggetto.</summary>
        public void ResetHealth()
        {
            IsDead        = false;
            CurrentHealth = _maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        /// <summary>Upgrade: aumenta solo la vita massima (la vita attuale non viene curata gratuitamente).</summary>
        public void IncreaseMaxHealth(float amount)
        {
            _maxHealth += amount;
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        /// <summary>Imposta direttamente la vita massima e resetta la vita attuale (zero GC).</summary>
        public void SetMaxHealth(float amount)
        {
            _maxHealth    = Mathf.Max(1f, amount);
            CurrentHealth = _maxHealth;
            IsDead        = false;
            OnHealthChanged?.Invoke(CurrentHealth, _maxHealth);
        }

        // ── Privato ────────────────────────────────────────────────────────────

        private void Die()
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }
}
