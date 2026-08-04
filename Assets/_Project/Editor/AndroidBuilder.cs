using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Настройка Android-плеера и сборка APK из кода. Настройки живут здесь, а не только
    /// в окне Player Settings, чтобы сборку можно было воспроизвести на чистой машине или
    /// в CI одной командой, ничего не прокликивая.
    /// </summary>
    public static class AndroidBuilder
    {
        private const string OutputFolder = "Builds/Android";
        private const string ApkName = "BallPath.apk";

        /// <summary>Отчёт о сборке. Library не версионируется — это временный артефакт.</summary>
        public const string ReportPath = "Library/AndroidBuildReport.txt";

        [MenuItem("Game/Android/Configure Player Settings", priority = 30)]
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "YoshkinKit";
            PlayerSettings.productName = "Ball Path";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.yoshkinkit.ballpath");

            // Портрет: управление — один палец, игра смотрит вдоль коридора вверх экрана.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // IL2CPP + ARM64 — обязательное сочетание для реальных устройств и Google Play.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android,
                Il2CppCompilerConfiguration.Release);

            // 25 — минимум, который принимает Unity 6; ниже редактор откатывает значение сам.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;

            // APK, а не App Bundle: по ТЗ нужен билд, который просто ставится на устройство.
            EditorUserBuildSettings.buildAppBundle = false;

            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);

            AssetDatabase.SaveAssets();
            Debug.Log("Настройки Android-плеера применены.");
        }

        [MenuItem("Game/Android/Build APK", priority = 31)]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("Активная платформа не Android. Сначала переключите платформу " +
                               "(File → Build Profiles) или выполните Game/Android/Switch Platform.");
                return;
            }

            ConfigurePlayerSettings();
            Directory.CreateDirectory(OutputFolder);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/Game.unity" },
                locationPathName = Path.Combine(OutputFolder, ApkName),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            File.WriteAllText(ReportPath, "building\n");

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception exception)
            {
                File.WriteAllText(ReportPath, "result=Exception\n" + exception + "\n");
                throw;
            }

            BuildSummary summary = report.summary;
            string text =
                $"result={summary.result}\n" +
                $"output={summary.outputPath}\n" +
                $"sizeBytes={summary.totalSize}\n" +
                $"duration={summary.totalTime.TotalSeconds:F0}s\n" +
                $"errors={summary.totalErrors} warnings={summary.totalWarnings}\n";

            File.WriteAllText(ReportPath, text);
            Debug.Log("Сборка APK завершена: " + summary.result + ", " + summary.outputPath);
        }

        /// <summary>
        /// Ставит сборку в очередь редактора и сразу возвращает управление. Нужно для запуска
        /// снаружи (MCP, скрипт): синхронная сборка IL2CPP блокирует редактор на минуты, и
        /// вызывающая сторона отваливается по таймауту, не дождавшись результата.
        /// </summary>
        [MenuItem("Game/Android/Build APK (deferred)", priority = 32)]
        public static void BuildApkDeferred()
        {
            EditorApplication.delayCall += BuildApk;
            Debug.Log("Сборка APK поставлена в очередь.");
        }

        [MenuItem("Game/Android/Switch Platform", priority = 33)]
        public static void SwitchPlatform()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Debug.Log("Платформа уже Android.");
                return;
            }

            EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android);
        }
    }
}
