using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace QuietRoom.EditorTools
{
    // Сборка отдельного приложения для macOS, которое открывается на весь экран.
    public static class GameBuilder
    {
        const string ScenePath = "Assets/_QuietRoom/Scenes/Basement.unity";

        [MenuItem("Подвал/Собрать игру (.app)")]
        public static void BuildMenu()
        {
            var report = Build();
            if (report == null) return;
            EditorUtility.RevealInFinder(report);
        }

        public static string Build()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            // Сборка — вне ~/Documents: папка синхронизируется с iCloud, тот плодит копии «… 2»
            // прямо во время записи, и собранная игра падала на старте (level0 is corrupted).
            string outDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "Games", "Подвал");
            string appPath = Path.Combine(outDir, "Подвал.app");
            Directory.CreateDirectory(outDir);

            PlayerSettings.productName = "Подвал";
            PlayerSettings.companyName = "Nikolay";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = false;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.macRetinaSupport = true;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = appPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.CleanBuildCache,   // без кэша: иначе сцена и скрипты могут разъехаться и игра падает на старте
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[QuietRoom] Сборка не удалась: " + report.summary.result);
                return null;
            }

            // Ключ OpenRouter кладём рядом с приложением — LlmClient ищет его там.
            string key = Path.Combine(projectRoot, "llm.json");
            if (File.Exists(key)) File.Copy(key, Path.Combine(outDir, "llm.json"), true);

            Debug.Log("[QuietRoom] Игра собрана: " + appPath);
            return appPath;
        }

        // Сборка под Windows (для друга): ~/Games/Подвал-Windows/Подвал.exe + llm.json рядом.
        // Нужен модуль Windows Build Support (Mono) в Unity Hub.
        [MenuItem("Подвал/Собрать игру для Windows (.exe)")]
        public static string BuildWindows()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                Debug.LogError("[QuietRoom] Нет модуля Windows Build Support — поставь его в Unity Hub и перезапусти редактор.");
                return null;
            }
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "Games", "Подвал-Windows");
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            string exe = Path.Combine(outDir, "Подвал.exe");

            PlayerSettings.productName = "Подвал";
            PlayerSettings.companyName = "Nikolay";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SplashScreen.show = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[QuietRoom] Сборка под Windows не удалась: " + report.summary.result);
                return null;
            }
            string key = Path.Combine(projectRoot, "llm.json");
            if (File.Exists(key)) File.Copy(key, Path.Combine(outDir, "llm.json"), true);
            // Отладочные символы другу не нужны.
            foreach (var dir in Directory.GetDirectories(outDir, "*BurstDebugInformation_DoNotShip"))
                Directory.Delete(dir, true);
            Debug.Log("[QuietRoom] Игра для Windows собрана: " + exe);
            return exe;
        }
    }
}
