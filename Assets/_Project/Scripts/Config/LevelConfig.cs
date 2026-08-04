using UnityEngine;

namespace Game.Config
{
    /// <summary>
    /// Параметры детерминированной генерации уровня (<see cref="Game.Level.LevelBuilder"/>).
    /// Значения по умолчанию — стартовая точка для итеративного тюнинга на этапе E
    /// (валидатор бюджета и проверка запаса 20%).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Level Config", fileName = "LevelConfig")]
    public sealed class LevelConfig : ScriptableObject
    {
        [Header("Общее")]
        [SerializeField] private int seed = 12345;
        [Tooltip("Точек маршрута до двери включительно. Ещё одна точка добавляется генератором за дверью.")]
        [SerializeField] private int pathPointCount = 20;
        [SerializeField] private Vector3 startPoint = new Vector3(-9f, 0f, -20f);
        [SerializeField] private Vector3 endPoint = new Vector3(9f, 0f, 20f);
        [Tooltip("Насколько дальше endPoint ставится последняя точка маршрута (за дверью) — победная точка.")]
        [SerializeField] private float pastDoorDistance = 2.5f;

        [Header("Форма маршрута")]
        [Tooltip("Амплитуда бокового S-образного отклонения маршрута от прямой старт-финиш, метры.")]
        [SerializeField] private float curveAmplitude = 3.5f;
        [Tooltip("Максимальный угол между направлением сегмента маршрута и направлением на дверь, градусы. " +
                 "Если нарушается — амплитуда кривой автоматически уменьшается. Выстрел всегда летит " +
                 "строго на дверь, поэтому чем больше это значение, тем выше шанс, что выстрел уйдёт " +
                 "мимо перекрывшего путь завала — в стену коридора.")]
        [SerializeField] private float maxPathDeviationDegrees = 12f;

        [Header("Коридор")]
        [Tooltip("Профиль сужения коридора — проектная величина, а не наблюдение. Именно " +
                 "сужение заставляет шар худеть: не влезающий в проход шар упирается в стены " +
                 "и обязан тратить массу. Проверяется валидатором бюджета.")]
        [SerializeField] private float expectedStartRadius = 1.2f;
        [SerializeField] private float expectedEndRadius = 0.70f;
        [Tooltip("Запас ширины коридора относительно ожидаемого радиуса игрока (1.2 = 20% запаса).")]
        [SerializeField] private float corridorMarginFactor = 1.2f;

        [Header("Стены коридора (визуальные, вдоль всего маршрута)")]
        [SerializeField] private float wallSpacingAlongPath = 1.1f;
        [SerializeField] private float wallJitter = 0.15f;
        [SerializeField] private Vector2 wallObstacleRadiusRange = new Vector2(0.30f, 0.45f);

        [Header("Блокирующие кластеры")]
        [SerializeField] private int blockerClusterCount = 5;
        [Tooltip("Желаемое число препятствий в кластере. Ширину кластера задаёт коридор, поэтому " +
                 "это значение влияет на количество рядов в глубину.")]
        [SerializeField] private Vector2Int clusterObstacleCountRange = new Vector2Int(7, 12);
        [SerializeField] private Vector2 clusterObstacleRadiusRange = new Vector2(0.28f, 0.40f);
        [Tooltip("Зазор между поверхностями соседних препятствий кластера. Маленький — волна " +
                 "заражения гарантированно идёт по цепочке.")]
        [SerializeField] private Vector2 clusterInnerGapRange = new Vector2(0.05f, 0.25f);

        [Header("Одиночные препятствия")]
        [SerializeField] private int singleObstacleCount = 3;
        [SerializeField] private Vector2 singleObstacleRadiusRange = new Vector2(0.35f, 0.50f);
        [Tooltip("Радиус расчистки стен вокруг одиночного препятствия. Генератор поднимет его " +
                 "до значения, гарантирующего отсутствие соседей в графе заражения.")]
        [SerializeField] private float singleObstacleIsolationDistance = 3.6f;

        public int Seed => seed;
        public int PathPointCount => pathPointCount;
        public Vector3 StartPoint => startPoint;
        public Vector3 EndPoint => endPoint;
        public float PastDoorDistance => pastDoorDistance;

        public float CurveAmplitude => curveAmplitude;
        public float MaxPathDeviationDegrees => maxPathDeviationDegrees;

        public float ExpectedStartRadius => expectedStartRadius;
        public float ExpectedEndRadius => expectedEndRadius;
        public float CorridorMarginFactor => corridorMarginFactor;

        public float WallSpacingAlongPath => wallSpacingAlongPath;
        public float WallJitter => wallJitter;
        public Vector2 WallObstacleRadiusRange => wallObstacleRadiusRange;

        public int BlockerClusterCount => blockerClusterCount;
        public Vector2Int ClusterObstacleCountRange => clusterObstacleCountRange;
        public Vector2 ClusterObstacleRadiusRange => clusterObstacleRadiusRange;
        public Vector2 ClusterInnerGapRange => clusterInnerGapRange;

        public int SingleObstacleCount => singleObstacleCount;
        public Vector2 SingleObstacleRadiusRange => singleObstacleRadiusRange;
        public float SingleObstacleIsolationDistance => singleObstacleIsolationDistance;
    }
}
