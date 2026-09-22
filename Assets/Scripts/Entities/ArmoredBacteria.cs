using UnityEngine;

namespace MobileGame.Entities
{
    /// <summary>
    /// Batterio corazzato: lento ma massiccio. Ha un'elevata quantita' di vita
    /// e assorbe i colpi proteggendo gli sciami piu' deboli.
    /// Zero allocazioni GC nei loop fisici (AGENTS.md §4).
    /// </summary>
    public class ArmoredBacteria : MicrobeEnemy
    {
        // I valori di velocita' e danno sono configurabili direttamente da Inspector
        // ma impostiamo valori predefiniti idonei al ruolo di Tank (lento, resistente).

        protected override Color GetThemeColor() => new Color(0.65f, 0.25f, 0.9f); // Viola corazzato
    }
}
