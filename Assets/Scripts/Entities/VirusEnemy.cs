using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Entities
{
    /// <summary>
    /// Virus veloce: piccolo, rapido, si muove con traiettoria ondulante organica.
    /// Pochi HP ma arriva in sciami e mette pressione al nano-bot.
    /// Zero allocazioni GC in FixedUpdate (AGENTS.md §4).
    /// </summary>
    public class VirusEnemy : MicrobeEnemy
    {
        [Header("Comportamento Virus")]
        [SerializeField] private float _waveFrequency = 4.2f;
        [SerializeField] private float _waveMagnitude = 0.70f;

        private float _timeOffset;

        protected override void Awake()
        {
            base.Awake();
            _timeOffset = Random.Range(0f, 10f); // sfasa l'oscillazione tra virus diversi
        }

        protected override void FixedUpdate()
        {
            if (_playerTransform == null || _isDying) return;

            Vector2 toPlayer = (Vector2)_playerTransform.position - _rb.position;
            Vector2 forwardDir = toPlayer.normalized;

            // Vettore perpendicolare (struct, zero alloc) per movimento sinusoidale
            Vector2 perpDir = new Vector2(-forwardDir.y, forwardDir.x);
            float sineWave = Mathf.Sin((Time.time + _timeOffset) * _waveFrequency) * _waveMagnitude;

            Vector2 finalDir = (forwardDir + perpDir * sineWave).normalized;
            Vector2 next = _rb.position + finalDir * (_currentSpeed * Time.fixedDeltaTime);

            if (ArenaBoundary.Instance != null)
            {
                next = ArenaBoundary.Instance.ClampPosition(next, 0.35f);
            }

            _rb.MovePosition(next);
        }

        protected override Color GetThemeColor() => new Color(1f, 0.55f, 0.08f); // Arancio virale neon
    }
}
