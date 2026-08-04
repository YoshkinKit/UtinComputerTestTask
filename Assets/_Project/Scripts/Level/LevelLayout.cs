using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Роль препятствия в раскладке уровня. Нужна и генератору (разные правила размещения),
    /// и тестам (проверки формулируются отдельно для стен коридора и для завалов).
    /// </summary>
    public enum ObstacleRole
    {
        /// <summary>Стена коридора: стоит за границей прохода и никогда его не перекрывает.</summary>
        CorridorWall,

        /// <summary>Препятствие в составе плотного кластера — выносится цепным заражением.</summary>
        BlockerCluster,

        /// <summary>Одиночное препятствие вне графа соседей — требует прямого попадания.</summary>
        SingleBlocker
    }

    /// <summary>Одно запланированное препятствие: логическая позиция (y = 0), радиус и роль.</summary>
    public readonly struct ObstacleSpec
    {
        /// <summary>Логическая позиция препятствия (плоскость y = 0).</summary>
        public readonly Vector3 Position;

        /// <summary>Радиус препятствия, метры.</summary>
        public readonly float Radius;

        /// <summary>Роль препятствия в раскладке.</summary>
        public readonly ObstacleRole Role;

        public ObstacleSpec(Vector3 position, float radius, ObstacleRole role)
        {
            Position = position;
            Radius = radius;
            Role = role;
        }
    }

    /// <summary>
    /// Один запланированный завал на маршруте: индекс точки, на которой игрок должен
    /// остановиться, и диапазон принадлежащих завалу препятствий в <see cref="LevelLayout.Obstacles"/>.
    /// Генератор гарантирует, что завал перекрывает сегмент <c>StopIndex → StopIndex + 1</c>
    /// и НЕ перекрывает уже пройденный сегмент <c>StopIndex - 1 → StopIndex</c>.
    /// </summary>
    public readonly struct BlockerSpec
    {
        /// <summary>Индекс точки маршрута, на которой игрок упрётся в этот завал.</summary>
        public readonly int StopIndex;

        /// <summary>Тип завала: плотный кластер или одиночное препятствие.</summary>
        public readonly ObstacleRole Role;

        /// <summary>Индекс первого препятствия завала в <see cref="LevelLayout.Obstacles"/>.</summary>
        public readonly int FirstObstacle;

        /// <summary>Количество препятствий в завале.</summary>
        public readonly int ObstacleCount;

        public BlockerSpec(int stopIndex, ObstacleRole role, int firstObstacle, int obstacleCount)
        {
            StopIndex = stopIndex;
            Role = role;
            FirstObstacle = firstObstacle;
            ObstacleCount = obstacleCount;
        }
    }

    /// <summary>
    /// Готовая раскладка уровня — чистые данные без единой ссылки на сцену. Генерируется
    /// <see cref="LevelBuilder"/>, потребляется редакторным сборщиком сцены и тестами:
    /// благодаря этому геометрию уровня можно проверять в EditMode-тестах, не создавая
    /// ни одного GameObject.
    /// </summary>
    public sealed class LevelLayout
    {
        public LevelLayout(Vector3[] pathPoints, float[] corridorWidths, ObstacleSpec[] obstacles,
            BlockerSpec[] blockers, int doorPointIndex, Vector3 doorForward, float curveAmplitude)
        {
            PathPoints = pathPoints;
            CorridorWidths = corridorWidths;
            Obstacles = obstacles;
            Blockers = blockers;
            DoorPointIndex = doorPointIndex;
            DoorForward = doorForward;
            CurveAmplitude = curveAmplitude;
        }

        /// <summary>
        /// Точки маршрута. Последняя точка стоит за дверью — именно её достижение считается
        /// победой (см. <see cref="Game.Player.PlayerMover.ReachedEnd"/>).
        /// </summary>
        public Vector3[] PathPoints { get; }

        /// <summary>Ширина коридора в каждой точке маршрута (тот же индекс, что в <see cref="PathPoints"/>).</summary>
        public float[] CorridorWidths { get; }

        /// <summary>Все препятствия уровня. Завалы идут первыми, стены коридора — следом.</summary>
        public ObstacleSpec[] Obstacles { get; }

        /// <summary>Запланированные завалы в порядке прохождения маршрута.</summary>
        public BlockerSpec[] Blockers { get; }

        /// <summary>Индекс точки маршрута, в которой стоит дверь.</summary>
        public int DoorPointIndex { get; }

        /// <summary>Позиция двери (плоскость y = 0).</summary>
        public Vector3 DoorPosition => PathPoints[DoorPointIndex];

        /// <summary>Направление «наружу» для двери — касательная маршрута в точке двери.</summary>
        public Vector3 DoorForward { get; }

        /// <summary>Стартовая точка игрока.</summary>
        public Vector3 PlayerStart => PathPoints[0];

        /// <summary>
        /// Фактическая амплитуда S-образного изгиба после авто-уменьшения под ограничение
        /// <see cref="Game.Config.LevelConfig.MaxPathDeviationDegrees"/>.
        /// </summary>
        public float CurveAmplitude { get; }
    }
}
