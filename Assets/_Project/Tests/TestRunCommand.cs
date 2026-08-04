using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Запускает EditMode-тесты из меню и кладёт короткий отчёт в файл. Нужно, чтобы прогон
    /// тестов не требовал ручного открытия окна Test Runner — результат можно прочитать
    /// из скрипта, из CI или снаружи редактора.
    /// </summary>
    public static class TestRunCommand
    {
        /// <summary>Путь отчёта. Library не попадает в систему контроля версий — это временный артефакт.</summary>
        public const string ReportPath = "Library/EditModeTestResults.txt";

        [MenuItem("Game/Run EditMode Tests", priority = 20)]
        public static void RunEditModeTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new ReportWriter());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));

            Debug.Log($"EditMode-тесты запущены, отчёт будет в {ReportPath}");
        }

        private sealed class ReportWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                File.WriteAllText(ReportPath, "running\n");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var report = new StringBuilder();
                report.AppendLine($"passed={result.PassCount} failed={result.FailCount} " +
                                  $"skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
                report.AppendLine($"duration={result.Duration:F2}s");

                AppendFailures(result, report);
                File.WriteAllText(ReportPath, report.ToString());

                Debug.Log($"EditMode-тесты: {result.PassCount} passed, {result.FailCount} failed");
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            private static void AppendFailures(ITestResultAdaptor result, StringBuilder report)
            {
                if (!result.HasChildren)
                {
                    if (result.TestStatus == TestStatus.Failed)
                    {
                        report.AppendLine($"FAILED: {result.FullName}");
                        report.AppendLine($"  {result.Message}");
                    }

                    return;
                }

                foreach (ITestResultAdaptor child in result.Children)
                {
                    AppendFailures(child, report);
                }
            }
        }
    }
}
