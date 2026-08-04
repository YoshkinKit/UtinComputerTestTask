using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Контейнер маршрута игрока. Точки маршрута — дочерние Transform-объекты этого
    /// GameObject (в порядке иерархии): так маршрут удобно редактировать прямо в сцене —
    /// перетаскивать точки, видеть путь и радиусы коридора без кастомных редакторов.
    /// Ширина коридора в каждой точке хранится отдельным сериализованным массивом
    /// параллельно списку точек. Генерация маршрута (расстановка точек, авто-подбор
    /// ширины под ожидаемый радиус игрока) — задача этапа C; здесь только контейнер и API чтения.
    /// </summary>
    public sealed class LevelPath : MonoBehaviour
    {
        [Tooltip("Ширина коридора в каждой точке маршрута. Индекс i соответствует i-му дочернему " +
                 "Transform в иерархии этого объекта (точки маршрута — не отдельное поле).")]
        [SerializeField] private float[] corridorWidths = System.Array.Empty<float>();

        [Tooltip("Ширина коридора по умолчанию для точек, для которых явно не задано значение " +
                 "в corridorWidths (защита от рассинхронизации размеров массивов).")]
        [SerializeField] private float fallbackCorridorWidth = 2f;

        /// <summary>Количество точек маршрута (= количество дочерних Transform).</summary>
        public int PointCount => transform.childCount;

        /// <summary>
        /// Логическая позиция точки маршрута (проекция на плоскость y = 0, см. этап B —
        /// вся геймплейная математика плоская, визуальная высота объектов роли не играет).
        /// </summary>
        public Vector3 GetPoint(int index)
        {
            Vector3 worldPosition = transform.GetChild(index).position;
            return new Vector3(worldPosition.x, 0f, worldPosition.z);
        }

        /// <summary>Ширина коридора в точке маршрута с заданным индексом.</summary>
        public float GetCorridorWidth(int index)
        {
            if (corridorWidths == null || index >= corridorWidths.Length || index < 0)
            {
                return fallbackCorridorWidth;
            }

            return corridorWidths[index];
        }

        /// <summary>
        /// Программно задаёт массив ширин коридора (по точке на индекс). Используется
        /// генератором уровня (<see cref="LevelBuilder"/>) — сами точки маршрута читаются
        /// напрямую из дочерних Transform и этим методом не затрагиваются.
        /// </summary>
        public void SetCorridorWidths(float[] widths)
        {
            corridorWidths = widths ?? System.Array.Empty<float>();
        }

        private void OnDrawGizmos()
        {
            int count = PointCount;
            if (count == 0)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < count - 1; i++)
            {
                Gizmos.DrawLine(transform.GetChild(i).position, transform.GetChild(i + 1).position);
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 center = transform.GetChild(i).position;
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
                Gizmos.DrawSphere(center, 0.08f);

                float radius = GetCorridorWidth(i) * 0.5f;
                DrawFlatCircle(center, radius, new Color(0.2f, 0.8f, 1f, 0.9f));
            }
        }

        private static void DrawFlatCircle(Vector3 center, float radius, Color color)
        {
            const int segments = 24;
            Gizmos.color = color;

            Vector3 previous = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
