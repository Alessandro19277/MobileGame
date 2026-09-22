using UnityEngine;

namespace MobileGame.Core
{
    /// <summary>
    /// Entry point del gioco. Blocca il framerate a 60 FPS, disabilita il VSync
    /// e impedisce lo spegnimento automatico dello schermo su Android.
    /// Singleton con DontDestroyOnLoad: se ne esiste già uno, il duplicato viene distrutto.
    /// [DefaultExecutionOrder(-1000)] garantisce Awake prima di tutti gli altri script.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameBootstrapper : MonoBehaviour
    {
        private static GameBootstrapper _instance;

        private void Awake()
        {
            // Singleton guard — sicuro anche dopo scene reload (AGENTS.md §3)
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // --- Framerate (AGENTS.md §3) ---
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount  = 0; // VSync off: targetFrameRate ha precedenza

            // --- Schermo sempre acceso su Android ---
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // --- Adattamento Schermo Mobile (Formato Fisso 9:16 Arcade con Letterbox) ---
            ScreenAspectEnforcer.EnsureInstance();
        }
    }
}
