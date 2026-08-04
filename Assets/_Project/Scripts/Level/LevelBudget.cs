using Game.Config;
using UnityEngine;

namespace Game.Level
{
    /// <summary>Бюджет массы уровня: сколько нужно на прохождение и сколько есть.</summary>
    public readonly struct LevelBudget
    {
        /// <summary>Проходим ли уровень вообще.</summary>
        public readonly bool Completable;

        /// <summary>Масса, которую тратит идеальный игрок (каждый выстрел минимально достаточный).</summary>
        public readonly float RequiredMass;

        /// <summary>Масса, доступная игроку: всё, что выше критического размера.</summary>
        public readonly float AvailableMass;

        /// <summary>Стартовая масса шара.</summary>
        public readonly float StartMass;

        /// <summary>Радиус шара у двери при идеальном прохождении.</summary>
        public readonly float FinalRadius;

        /// <summary>Количество выстрелов при идеальном прохождении.</summary>
        public readonly int ShotCount;

        /// <summary>Причина непроходимости (пусто, если уровень проходим).</summary>
        public readonly string Failure;

        public LevelBudget(bool completable, float requiredMass, float availableMass, float startMass,
            float finalRadius, int shotCount, string failure)
        {
            Completable = completable;
            RequiredMass = requiredMass;
            AvailableMass = availableMass;
            StartMass = startMass;
            FinalRadius = finalRadius;
            ShotCount = shotCount;
            Failure = failure;
        }

        /// <summary>
        /// Запас массы сверх необходимого, долей от необходимого: 0.2 — ровно требуемые ТЗ 20%.
        /// Это и есть допуск на неидеальную игру: <see cref="RequiredMass"/> считается по
        /// минимально достаточным выстрелам, которые живой игрок точно не повторит.
        /// </summary>
        public float Reserve => RequiredMass <= 1e-5f ? float.PositiveInfinity : AvailableMass / RequiredMass - 1f;

        /// <summary>Хватает ли запаса (уровень проходим И запас не меньше требуемого).</summary>
        public bool MeetsReserve(float requiredReserve) => Completable && Reserve >= requiredReserve;
    }

    /// <summary>
    /// Проверка требования ТЗ «від самого початку розміру кулі повинно вистачити з запасом 20%».
    /// <para/>
    /// Считает не «на глаз», а тем же прогоном, что и рантайм-детектор проигрыша
    /// (<see cref="LevelWalkthrough"/>): бинарным поиском берётся минимально достаточный
    /// выстрел на каждый завал, их суммарная масса и есть стоимость прохождения. Запас —
    /// это доля сверх неё, то есть ровно допуск на неидеальную игру.
    /// </summary>
    public static class LevelBudgetValidator
    {
        /// <summary>Требуемый по ТЗ запас массы — 20%.</summary>
        public const float RequiredReserve = 0.2f;

        /// <summary>Считает бюджет уже построенной раскладки уровня.</summary>
        public static LevelBudget Evaluate(LevelLayout layout, PlayerConfig playerConfig,
            InfectionConfig infectionConfig)
        {
            WalkthroughResult run = LevelWalkthrough.Simulate(layout, playerConfig, infectionConfig);
            float available = Mathf.Max(0f, playerConfig.StartMass - playerConfig.CriticalMass);

            return new LevelBudget(run.Completed, run.SpentMass, available, playerConfig.StartMass,
                run.FinalRadius, run.ShotCount, run.Failure);
        }

        /// <summary>Генерирует уровень по конфигу и сразу считает его бюджет.</summary>
        public static LevelBudget Evaluate(LevelConfig levelConfig, PlayerConfig playerConfig,
            InfectionConfig infectionConfig)
        {
            LevelLayout layout = LevelBuilder.Build(levelConfig, infectionConfig.MaxNeighborGap);
            return Evaluate(layout, playerConfig, infectionConfig);
        }
    }
}
