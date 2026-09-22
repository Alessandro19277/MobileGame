using UnityEngine;

namespace MobileGame.Utilities
{
    /// <summary>
    /// Fornisce feedback aptico (vibrazione) ottimizzato per dispositivi mobili Android.
    /// Utilizza chiamate native safe che non allocano memoria e non causano errori nell'editor o su PC/Mac.
    /// Zero allocazioni heap (AGENTS.md §4).
    /// </summary>
    public static class HapticManager
    {
        private static bool _hapticsEnabled = true;

        public static bool HapticsEnabled
        {
            get => _hapticsEnabled;
            set => _hapticsEnabled = value;
        }

        /// <summary>
        /// Feedback leggero: ideale per raccolta gemme XP, colpi andati a segno o avanzamento di livello.
        /// </summary>
        public static void LightImpact()
        {
            if (!_hapticsEnabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Breve impulso di vibrazione (15ms)
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null && vibrator.Call<bool>("hasVibrator"))
                    {
                        // API 26+ VibrationEffect.createOneShot(15, VibrationEffect.DEFAULT_AMPLITUDE)
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            int sdkInt = version.GetStatic<int>("SDK_INT");
                            if (sdkInt >= 26)
                            {
                                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)15, 60))
                                {
                                    vibrator.Call("vibrate", effect);
                                }
                            }
                            else
                            {
                                vibrator.Call("vibrate", (long)15);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback trasparente
            }
#endif
        }

        /// <summary>
        /// Feedback medio: ideale per colpi subiti dal giocatore o rallentamento criogenico.
        /// </summary>
        public static void MediumImpact()
        {
            if (!_hapticsEnabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null && vibrator.Call<bool>("hasVibrator"))
                    {
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            int sdkInt = version.GetStatic<int>("SDK_INT");
                            if (sdkInt >= 26)
                            {
                                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)35, 140))
                                {
                                    vibrator.Call("vibrate", effect);
                                }
                            }
                            else
                            {
                                vibrator.Call("vibrate", (long)35);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback trasparente
            }
#endif
        }

        /// <summary>
        /// Feedback pesante: ideale per sconfitta di un boss patogeno o Game Over.
        /// </summary>
        public static void HeavyImpact()
        {
            if (!_hapticsEnabled) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null && vibrator.Call<bool>("hasVibrator"))
                    {
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            int sdkInt = version.GetStatic<int>("SDK_INT");
                            if (sdkInt >= 26)
                            {
                                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)70, 255))
                                {
                                    vibrator.Call("vibrate", effect);
                                }
                            }
                            else
                            {
                                vibrator.Call("vibrate", (long)70);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback trasparente
            }
#endif
        }
    }
}
