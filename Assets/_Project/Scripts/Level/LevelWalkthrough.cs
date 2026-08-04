using System.Collections.Generic;
using Game.Config;
using Game.Obstacles;
using Game.Utils;
using UnityEngine;

namespace Game.Level
{
    /// <summary>Итог прогона уровня идеальным игроком.</summary>
    public readonly struct WalkthroughResult
    {
        /// <summary>Удалось ли дойти до последней точки маршрута.</summary>
        public readonly bool Completed;

        /// <summary>Сколько выстрелов потребовалось.</summary>
        public readonly int ShotCount;

        /// <summary>Масса, потраченная на выстрелы.</summary>
        public readonly float SpentMass;

        /// <summary>Радиус игрока в конце прогона.</summary>
        public readonly float FinalRadius;

        /// <summary>Индекс точки маршрута, на которой прогон остановился.</summary>
        public readonly int StoppedAtIndex;

        /// <summary>Причина неудачи (пусто, если уровень пройден).</summary>
        public readonly string Failure;

        public WalkthroughResult(bool completed, int shotCount, float spentMass, float finalRadius,
            int stoppedAtIndex, string failure)
        {
            Completed = completed;
            ShotCount = shotCount;
            SpentMass = spentMass;
            FinalRadius = finalRadius;
            StoppedAtIndex = stoppedAtIndex;
            Failure = failure;
        }
    }

    /// <summary>
    /// Прогон уровня «идеальным игроком»: та же последовательность решений, что и в рантайме
    /// (<see cref="Game.Player.PlayerMover"/> + <see cref="Game.Core.GameController"/>), но
    /// на чистых данных и без сцены — каждый выстрел берётся минимально достаточным
    /// (<see cref="LevelSolver.TryFindMinimalShotRadius"/>).
    /// <para/>
    /// Отвечает на вопрос «уровень вообще проходим и сколько массы на это нужно». Им
    /// пользуются сам генератор (подгонка ширины коридора под фактический размер шара),
    /// редакторный сборщик сцены и тесты. Валидатор бюджета с требованием запаса 20%
    /// (этап E) строится поверх этого же прогона.
    /// </summary>
    public static class LevelWalkthrough
    {
        private const float ShotRadiusTolerance = 0.01f;

        /// <summary>Прогоняет уровень и возвращает, чем он закончился.</summary>
        public static WalkthroughResult Simulate(LevelLayout layout, PlayerConfig playerConfig,
            InfectionConfig infectionConfig)
        {
            InfectionGraph graph = BuildGraph(layout, infectionConfig.MaxNeighborGap);
            InfectionSettings settings = infectionConfig.ToSettings();
            var hits = new List<InfectionHit>();

            Vector3[] points = layout.PathPoints;
            Vector3 door = layout.DoorPosition;

            float mass = playerConfig.StartMass;
            float startMass = mass;
            int index = 0;
            int shotCount = 0;

            // Страховка от зацикливания: выстрелов заведомо не может быть больше, чем препятствий.
            int shotLimit = layout.Obstacles.Length + 1;

            while (index < points.Length - 1)
            {
                float radius = MassUtils.RadiusFromMass(mass);
                float clearance = radius * playerConfig.ClearanceFactor;

                if (PathGeometry.IsSegmentClear(graph, points[index], points[index + 1], clearance))
                {
                    index++;
                    continue;
                }

                if (shotCount >= shotLimit)
                {
                    return Failed(shotCount, startMass - mass, mass, index,
                        "прогон не сходится: превышен лимит выстрелов");
                }

                float availableMass = mass - playerConfig.CriticalMass;
                if (availableMass <= 0f)
                {
                    return Failed(shotCount, startMass - mass, mass, index, "масса упала до критической");
                }

                Vector3 origin = points[index];
                Vector3 direction = DirectionTo(origin, door);
                float maxShotRadius = MassUtils.RadiusFromMass(availableMass);

                bool solvable = LevelSolver.TryFindMinimalShotRadius(graph, origin, direction,
                    points[index], points[index + 1], clearance, settings, maxShotRadius,
                    ShotRadiusTolerance, out float shotRadius);

                if (!solvable)
                {
                    return Failed(shotCount, startMass - mass, mass, index,
                        $"сегмент {index}→{index + 1} не расчищается даже максимальным доступным выстрелом");
                }

                ApplyShot(graph, origin, direction, shotRadius, settings, hits);
                mass -= MassUtils.MassFromRadius(shotRadius);
                shotCount++;
            }

            float finalRadius = MassUtils.RadiusFromMass(mass);
            return new WalkthroughResult(true, shotCount, startMass - mass, finalRadius, index, string.Empty);
        }

        /// <summary>Строит граф заражения по раскладке уровня.</summary>
        public static InfectionGraph BuildGraph(LevelLayout layout, float maxNeighborGap)
        {
            var nodes = new List<ObstacleNode>(layout.Obstacles.Length);
            foreach (ObstacleSpec spec in layout.Obstacles)
            {
                nodes.Add(new ObstacleNode(spec.Position, spec.Radius));
            }

            return new InfectionGraph(nodes, maxNeighborGap);
        }

        private static WalkthroughResult Failed(int shotCount, float spentMass, float mass, int index, string failure)
        {
            return new WalkthroughResult(false, shotCount, spentMass, MassUtils.RadiusFromMass(mass), index, failure);
        }

        private static void ApplyShot(InfectionGraph graph, Vector3 origin, Vector3 direction, float shotRadius,
            in InfectionSettings settings, List<InfectionHit> hits)
        {
            if (!PathGeometry.RaycastFirstNode(graph, origin, direction, shotRadius, out _, out Vector3 impact))
            {
                return;
            }

            InfectionSimulator.Simulate(graph, impact, shotRadius, settings, hits);
            foreach (InfectionHit hit in hits)
            {
                graph.Kill(hit.Index);
            }
        }

        private static Vector3 DirectionTo(Vector3 from, Vector3 to)
        {
            Vector3 delta = new Vector3(to.x - from.x, 0f, to.z - from.z);
            return delta.sqrMagnitude < 1e-8f ? Vector3.forward : delta.normalized;
        }
    }
}
