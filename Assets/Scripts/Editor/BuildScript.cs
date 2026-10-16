using System;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MobileGame.Editor
{
    /// <summary>
    /// Script Editor per automatizzare la generazione del pacchetto Android (.apk).
    /// Risolve automaticamente il conflitto di classi duplicate Kotlin in Gradle
    /// (com.android.build.gradle.internal.tasks.CheckDuplicatesRunnable).
    /// </summary>
    public static class BuildScript
    {
        private const string DEFAULT_OUTPUT_DIR = "Builds/Android";
        private const string DEFAULT_APK_NAME   = "Nanopilot_v1.0.apk";
        private const string PACKAGE_NAME       = "com.NanopilotStudio.Nanopilot";

        [MenuItem("Build/Build Android APK", false, 100)]
        public static void BuildAndroidAPK()
        {
            PerformBuild(false);
        }

        [MenuItem("Build/Build and Run Android APK", false, 101)]
        public static void BuildAndRunAndroidAPK()
        {
            PerformBuild(true);
        }

        [MenuItem("Build/Export Leaderboard Seed", false, 110)]
        public static void ExportLeaderboardSeedMenu()
        {
            ExportLeaderboardSeed();
            EditorUtility.DisplayDialog("Leaderboard Seed", "Classifica locale esportata in Assets/Resources/default_leaderboard.json per l'APK!", "OK");
        }

        /// <summary>
        /// Metodo invocabile da riga di comando tramite:
        /// -executeMethod MobileGame.Editor.BuildScript.BuildAndroidAPKBatch
        /// </summary>
        public static void BuildAndroidAPKBatch()
        {
            PerformBuild(false, true);
        }

        private static void PerformBuild(bool autoRun, bool isBatchMode = false)
        {
            Debug.Log("[BuildScript] Avvio procedura di build Android APK...");

            // 1. Assicura che la piattaforma di destinazione sia Android
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[BuildScript] Cambio BuildTarget su Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            // 2. Configura le impostazioni per produrre un file .apk (non .aab)
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            // 3. Verifica package name e orientamento
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PACKAGE_NAME);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.Android.forceInternetPermission = true;

            // 4. Cartella di destinazione
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(projectRoot, DEFAULT_OUTPUT_DIR);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string fullApkPath = Path.Combine(outputDirectory, DEFAULT_APK_NAME);

            // Esporta i record attuali del PC come seed iniziale nell'APK
            ExportLeaderboardSeed();

            // Pre-patch di sicurezza sui file Gradle temporanei esistenti
            PatchExistingGradleFiles(projectRoot);

            // 5. Scene abilitate
            string[] scenes = GetBuildScenes();
            if (scenes.Length == 0)
            {
                string errorMsg = "[BuildScript] ERRORE: Nessuna scena trovata nelle Build Settings!";
                Debug.LogError(errorMsg);
                if (!isBatchMode)
                {
                    EditorUtility.DisplayDialog("Build Error", errorMsg, "OK");
                }
                return;
            }

            // 6. Opzioni di build
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = autoRun ? BuildOptions.AutoRunPlayer : BuildOptions.None
            };

            Debug.Log($"[BuildScript] Compilazione in corso verso: {fullApkPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string successMsg = $"Build Android completata con successo!\n\nDimensione: {summary.totalSize / (1024 * 1024):F1} MB\nFile: {fullApkPath}";
                Debug.Log($"[BuildScript] {successMsg}");

                if (!isBatchMode)
                {
                    EditorUtility.RevealInFinder(fullApkPath);
                    EditorUtility.DisplayDialog("Build Android APK", successMsg, "OK");
                }
            }
            else if (summary.result == BuildResult.Failed)
            {
                string failMsg = $"[BuildScript] Build FALLITA con {summary.totalErrors} errori. Controlla la console di Unity.";
                Debug.LogError(failMsg);
                if (!isBatchMode)
                {
                    EditorUtility.DisplayDialog("Build Fallita", failMsg, "OK");
                }
            }
        }

        private static void PatchExistingGradleFiles(string projectRoot)
        {
            string beeGradlePath = Path.Combine(projectRoot, "Library/Bee/Android/Prj/IL2CPP/Gradle");
            if (Directory.Exists(beeGradlePath))
            {
                AndroidGradleDuplicateClassFixer.ApplyKotlinDuplicatesFix(
                    Path.Combine(beeGradlePath, "unityLibrary")
                );
            }
        }

        private static string[] GetBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            var list = new System.Collections.Generic.List<string>();

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    list.Add(scenes[i].path);
                }
            }

            // Fallback se nessuna scena è selezionata
            if (list.Count == 0 && File.Exists("Assets/Scenes/SampleScene.unity"))
            {
                list.Add("Assets/Scenes/SampleScene.unity");
            }

            return list.ToArray();
        }

        private static void ExportLeaderboardSeed()
        {
            string currentPrefs = PlayerPrefs.GetString("NanoSurvivor_Leaderboard", string.Empty);
            if (!string.IsNullOrEmpty(currentPrefs))
            {
                string resourcesDir = Path.Combine(Application.dataPath, "Resources");
                if (!Directory.Exists(resourcesDir)) Directory.CreateDirectory(resourcesDir);
                string seedPath = Path.Combine(resourcesDir, "default_leaderboard.json");
                File.WriteAllText(seedPath, currentPrefs);
                AssetDatabase.Refresh();
                Debug.Log("[BuildScript] Classifica locale esportata come seed per l'APK in: " + seedPath);
            }
        }
    }

    /// <summary>
    /// Post-processore Gradle automatico: risolve il conflitto di classi duplicate tra
    /// kotlin-stdlib 1.8+ e i vecchi kotlin-stdlib-jdk7/jdk8 iniettati da librerie terze.
    /// </summary>
    public class AndroidGradleDuplicateClassFixer : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 999;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            ApplyKotlinDuplicatesFix(path);
        }

        public static void ApplyKotlinDuplicatesFix(string unityLibraryPath)
        {
            try
            {
                // 1. unityLibrary/build.gradle
                string unityLibraryGradle = Path.Combine(unityLibraryPath, "build.gradle");
                FixModuleGradle(unityLibraryGradle);

                // 2. launcher/build.gradle & root build.gradle
                string rootGradleDir = Directory.GetParent(unityLibraryPath)?.FullName;
                if (!string.IsNullOrEmpty(rootGradleDir) && Directory.Exists(rootGradleDir))
                {
                    string launcherGradle = Path.Combine(rootGradleDir, "launcher", "build.gradle");
                    FixModuleGradle(launcherGradle);

                    string rootGradle = Path.Combine(rootGradleDir, "build.gradle");
                    FixRootGradle(rootGradle);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[BuildScript] Impossibile applicare fix gradle: " + ex.Message);
            }
        }

        private static void FixModuleGradle(string filePath)
        {
            if (!File.Exists(filePath)) return;
            string content = File.ReadAllText(filePath);
            if (content.Contains("kotlin-stdlib-jdk7")) return; // Già applicato

            string fix = "\n\nconfigurations.all {\n" +
                         "    exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk7'\n" +
                         "    exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk8'\n" +
                         "}\n" +
                         "dependencies {\n" +
                         "    constraints {\n" +
                         "        implementation(\"org.jetbrains.kotlin:kotlin-stdlib-jdk7:1.8.22\") {\n" +
                         "            because(\"kotlin-stdlib-jdk7 is now part of kotlin-stdlib\")\n" +
                         "        }\n" +
                         "        implementation(\"org.jetbrains.kotlin:kotlin-stdlib-jdk8:1.8.22\") {\n" +
                         "            because(\"kotlin-stdlib-jdk8 is now part of kotlin-stdlib\")\n" +
                         "        }\n" +
                         "    }\n" +
                         "}\n";

            File.AppendAllText(filePath, fix);
            Debug.Log("[BuildScript] Fix classi duplicate Kotlin applicato a: " + filePath);
        }

        private static void FixRootGradle(string filePath)
        {
            if (!File.Exists(filePath)) return;
            string content = File.ReadAllText(filePath);
            if (content.Contains("kotlin-stdlib-jdk7")) return;

            string fix = "\n\nallprojects {\n" +
                         "    configurations.all {\n" +
                         "        exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk7'\n" +
                         "        exclude group: 'org.jetbrains.kotlin', module: 'kotlin-stdlib-jdk8'\n" +
                         "    }\n" +
                         "}\n";

            File.AppendAllText(filePath, fix);
            Debug.Log("[BuildScript] Fix root gradle applicato a: " + filePath);
        }
    }
}
