using System.Text;
using Game.Config;
using Game.Level;
using Game.Utils;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Редакторные команды баланса: отчёт по бюджету массы уровня и сброс конфигов к
    /// значениям по умолчанию из кода.
    /// </summary>
    public static class LevelBudgetTools
    {
        [MenuItem("Game/Validate Level Budget", priority = 10)]
        public static void ValidateLevelBudget()
        {
            PlayerConfig playerConfig = LevelSceneBuilder.LoadOrCreate<PlayerConfig>("PlayerConfig");
            InfectionConfig infectionConfig = LevelSceneBuilder.LoadOrCreate<InfectionConfig>("InfectionConfig");
            LevelConfig levelConfig = LevelSceneBuilder.LoadOrCreate<LevelConfig>("LevelConfig");

            LevelLayout layout = LevelBuilder.Build(levelConfig, infectionConfig.MaxNeighborGap);
            LevelBudget budget = LevelBudgetValidator.Evaluate(layout, playerConfig, infectionConfig);

            string report = BuildReport(budget, layout, levelConfig, playerConfig);

            if (budget.MeetsReserve(LevelBudgetValidator.RequiredReserve))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogWarning(report);
            }
        }

        /// <summary>
        /// Перезаписывает конфиги значениями по умолчанию из классов, сохраняя GUID ассетов —
        /// ссылки в сцене не рвутся. Нужно после правки дефолтов в коде: сериализованные
        /// ассеты хранят старые значения и сами не обновляются.
        /// </summary>
        [MenuItem("Game/Reset Configs To Defaults", priority = 11)]
        public static void ResetConfigsToDefaults()
        {
            ResetConfig<PlayerConfig>("PlayerConfig");
            ResetConfig<ShotConfig>("ShotConfig");
            ResetConfig<InfectionConfig>("InfectionConfig");
            ResetConfig<LevelConfig>("LevelConfig");

            AssetDatabase.SaveAssets();
            Debug.Log("Конфиги сброшены к значениям по умолчанию из кода.");
        }

        private static void ResetConfig<T>(string name) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{LevelSceneBuilder.ConfigFolder}/{name}.asset");
            if (asset == null)
            {
                return;
            }

            T defaults = ScriptableObject.CreateInstance<T>();
            EditorUtility.CopySerialized(defaults, asset);

            // CopySerialized переносит и имя объекта — возвращаем его, иначе ассет
            // переименуется в служебное имя временного экземпляра.
            asset.name = name;

            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(asset);
        }

        private static string BuildReport(LevelBudget budget, LevelLayout layout,
            LevelConfig levelConfig, PlayerConfig playerConfig)
        {
            var report = new StringBuilder();
            report.AppendLine("Бюджет уровня");

            if (!budget.Completable)
            {
                report.AppendLine($"УРОВЕНЬ НЕПРОХОДИМ: {budget.Failure}");
                return report.ToString();
            }

            float reservePercent = budget.Reserve * 100f;
            string verdict = budget.MeetsReserve(LevelBudgetValidator.RequiredReserve)
                ? "ТРЕБОВАНИЕ ТЗ ВЫПОЛНЕНО"
                : $"ЗАПАСА НЕ ХВАТАЕТ (нужно {LevelBudgetValidator.RequiredReserve * 100f:F0}%)";

            report.AppendLine($"Запас массы: {reservePercent:F0}% — {verdict}");
            report.AppendLine($"Нужно на прохождение: {budget.RequiredMass:F2} " +
                              $"из доступных {budget.AvailableMass:F2} " +
                              $"(старт {budget.StartMass:F2}, критический порог {playerConfig.CriticalMass:F2})");
            report.AppendLine($"Выстрелов при идеальной игре: {budget.ShotCount}");
            report.AppendLine($"Радиус шара: {playerConfig.StartRadius:F2} м на старте → " +
                              $"{budget.FinalRadius:F2} м у двери " +
                              $"(критический {playerConfig.CriticalRadius:F2} м)");

            float designEndRadius = levelConfig.ExpectedEndRadius;
            float mismatch = budget.FinalRadius - designEndRadius;
            report.AppendLine($"Проектный радиус у двери: {designEndRadius:F2} м, расхождение {mismatch:+0.00;-0.00} м" +
                              (Mathf.Abs(mismatch) <= 0.1f ? " — профиль коридора сходится с балансом" : ""));

            report.AppendLine($"Коридор: {layout.CorridorWidths[0]:F2} м → " +
                              $"{layout.CorridorWidths[layout.DoorPointIndex]:F2} м, " +
                              $"диаметр шара у двери {budget.FinalRadius * 2f:F2} м");

            float perShot = budget.ShotCount > 0 ? budget.RequiredMass / budget.ShotCount : 0f;
            report.AppendLine($"Средний выстрел: масса {perShot:F2}, радиус {MassUtils.RadiusFromMass(perShot):F2} м");

            return report.ToString();
        }
    }
}
