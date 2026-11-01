using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Marchlands.Editor
{
    /// <summary>
    /// Сборки одного проекта под разные платформы: телефон (Android, APK) и ПК (Windows, папка с exe).
    /// Номер сборки — следующий после последнего в папке Builds. Управление выбирается само по платформе:
    /// на телефоне — пальцы, на ПК — мышь и клавиатура.
    /// </summary>
    public static class BuildMenu
    {
        private const string BuildsRoot = "Builds";

        [MenuItem("Marchlands/Сборка Android")]
        public static void BuildAndroid()
        {
            string folder = Path.Combine(BuildsRoot, "Android");
            int number = NextNumber(folder, @"Marchlands-(\d+)\.apk$");
            Build(BuildTarget.Android, Path.Combine(folder, $"Marchlands-{number}.apk"));
        }

        [MenuItem("Marchlands/Сборка Windows")]
        public static void BuildWindows()
        {
            string folder = Path.Combine(BuildsRoot, "Windows");
            int number = NextNumber(folder, @"Marchlands-(\d+)$");
            ApplyWindowsSettings();
            Build(BuildTarget.StandaloneWindows64, Path.Combine(folder, $"Marchlands-{number}", "Marchlands.exe"));
        }

        /// <summary>Сцена выделенного сервера комнат: в клиентские сборки не входит.</summary>
        private const string ServerScene = "Assets/_Project/Scenes/Server.unity";

        /// <summary>
        /// Сервер комнат для VPS: Linux Dedicated Server (без графики и звука), Mono — сборке под Linux из Windows так
        /// не нужен отдельный набор инструментов. Папка Builds/Server/Marchlands-Server-N с MarchlandsServer.x86_64.
        /// </summary>
        [MenuItem("Marchlands/Сборка сервера (Linux)")]
        public static void BuildServer()
        {
            string folder = Path.Combine(BuildsRoot, "Server");
            int number = NextNumber(folder, @"Marchlands-Server-(\d+)$");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
            string path = Path.Combine(folder, $"Marchlands-Server-{number}", "MarchlandsServer.x86_64");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Run(new BuildPlayerOptions
            {
                scenes = new[] { ServerScene },
                locationPathName = path,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            });
        }

        /// <summary>Окно ПК-версии: по умолчанию во весь экран, 1920×1080, размер окна можно менять.</summary>
        public static void ApplyWindowsSettings()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
        }

        private static void Build(BuildTarget target, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Run(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled && scene.path != ServerScene).Select(scene => scene.path).ToArray(),
                locationPathName = path,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None
            });
        }

        private static void Run(BuildPlayerOptions options)
        {
            string path = options.locationPathName;
            BuildReport report = BuildPipeline.BuildPlayer(options);
            // Бывает «успех» без файла (например, модуль платформы установлен, но редактор не перезапущен).
            if (report.summary.result == BuildResult.Succeeded && !File.Exists(path))
            {
                Debug.LogError($"Сборка не удалась: {path} не создан. Если модуль платформы только что установлен — перезапустите редактор.");
                return;
            }

            if (report.summary.result == BuildResult.Succeeded)
            {
                // Отладочные данные Burst игроку не нужны (Unity сам называет папку DoNotShip).
                string folder = Path.GetDirectoryName(path);
                foreach (string debug in Directory.GetDirectories(folder, "*_DoNotShip"))
                {
                    Directory.Delete(debug, true);
                }

                Debug.Log($"Сборка готова: {path} ({report.summary.totalSize / (1024 * 1024)} МБ)");
            }
            else
            {
                Debug.LogError($"Сборка не удалась ({report.summary.result}): {path}");
            }
        }

        /// <summary>Следующий номер сборки в папке: больше всех найденных по шаблону (с группой номера).</summary>
        private static int NextNumber(string folder, string pattern)
        {
            int last = 0;
            if (Directory.Exists(folder))
            {
                var regex = new Regex(pattern);
                foreach (string entry in Directory.GetFileSystemEntries(folder))
                {
                    Match match = regex.Match(Path.GetFileName(entry));
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    {
                        last = Mathf.Max(last, number);
                    }
                }
            }

            return last + 1;
        }
    }
}
