using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Weapons
{
    /// <summary>
    /// Singola sfera orbitale: ruota attorno al nano-bot e infligge danno ai nemici al contatto.
    /// Zero allocazioni nei loop fisici (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class OrbitalProjectile : MonoBehaviour
    {
        private float _damage = 20f;

        private void Awake()
        {
            var col = GetComponent<CircleCollider2D>();
            col.isTrigger = true;
        }

        public void SetDamage(float damage) => _damage = damage;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Enemy")) return;
            other.GetComponent<HealthComponent>()?.TakeDamage(_damage);
            MobileGame.UI.DamageNumberManager.Spawn(other.transform.position, Mathf.RoundToInt(_damage), false);
        }
    }
}
