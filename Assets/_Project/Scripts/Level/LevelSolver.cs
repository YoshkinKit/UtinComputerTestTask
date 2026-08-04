using System.Collections.Generic;
using Game.Obstacles;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Чистая (без MonoBehaviour) логика "сколько нужно выстрела, чтобы расчистить сегмент".
    /// Общая для детектора проигрыша «не хватило массы» в рантайме и для редакторного
    /// валидатора бюджета уровня (этап E): оба задают один и тот же вопрос — какой
    /// минимальный радиус выстрела в заданном направлении открывает заданный сегмент коридора.
    /// </summary>
    public static class LevelSolver
    {
        // Переиспользуемый буфер результатов заражения — SimulateShotOnClone вызывается до
        // ~20 раз за один бинарный поиск, аллокация на каждый вызов была бы расточительна.
        private static readonly List<InfectionHit> s_hitBuffer = new List<InfectionHit>();

        /// <summary>
        /// Бинарный поиск минимального радиуса выстрела (в границах [0, maxShotRadius]),
        /// после которого сегмент segmentA→segmentB становится проходим для шара радиуса
        /// playerRadius. Выстрел вылетает из origin в направлении direction — так же, как
        /// в реальной игре (направление на цель, не свободное прицеливание).
        /// <para/>
        /// Корректность не опирается на глобальную монотонность предиката "сегмент чист"
        /// как функции радиуса (в общем случае у неё есть тонкость: при росте радиуса луч
        /// казания в <see cref="PathGeometry.RaycastFirstNode"/> раздувается и теоретически
        /// может "перескочить" на другой узел). Вместо этого поддерживается инвариант цикла:
        /// <c>high</c> всегда — уже подтверждённый работающий радиус (сегмент чист),
        /// <c>low</c> всегда — уже подтверждённый недостаточный радиус (сегмент не чист).
        /// Это гарантирует, что возвращаемый радиус ДЕЙСТВИТЕЛЬНО расчищает сегмент, даже
        /// если для патологических раскладок препятствий он не был бы теоретическим минимумом.
        /// </summary>
        /// <param name="graph">Граф препятствий (не изменяется — все прогоны идут по клонам).</param>
        /// <param name="origin">Точка старта выстрела (логическая позиция игрока).</param>
        /// <param name="direction">Направление выстрела (нормализованное, обычно на дверь).</param>
        /// <param name="segmentA">Начало проверяемого сегмента коридора.</param>
        /// <param name="segmentB">Конец проверяемого сегмента коридора.</param>
        /// <param name="playerRadius">Радиус (с учётом запаса), которым проверяется проходимость сегмента.</param>
        /// <param name="settings">Параметры модели заражения.</param>
        /// <param name="maxShotRadius">Верхняя граница поиска (например, весь доступный игроку радиус).</param>
        /// <param name="tolerance">Точность по радиусу, при которой поиск останавливается.</param>
        /// <param name="shotRadius">Найденный радиус (валиден только если метод вернул true).</param>
        /// <returns>false, если даже maxShotRadius не расчищает сегмент (доступной массы не хватит).</returns>
        public static bool TryFindMinimalShotRadius(
            InfectionGraph graph, Vector3 origin, Vector3 direction,
            Vector3 segmentA, Vector3 segmentB, float playerRadius,
            in InfectionSettings settings, float maxShotRadius, float tolerance, out float shotRadius)
        {
            shotRadius = 0f;
            maxShotRadius = Mathf.Max(0f, maxShotRadius);
            float safeTolerance = Mathf.Max(tolerance, 1e-5f);

            // Даже максимально доступный выстрел не расчищает сегмент — решения нет.
            InfectionGraph maxClone = SimulateShotOnClone(graph, origin, direction, maxShotRadius, settings);
            if (!PathGeometry.IsSegmentClear(maxClone, segmentA, segmentB, playerRadius))
            {
                return false;
            }

            // Тривиальный случай: сегмент чист даже без выстрела (некому мешать).
            InfectionGraph zeroClone = SimulateShotOnClone(graph, origin, direction, 0f, settings);
            if (PathGeometry.IsSegmentClear(zeroClone, segmentA, segmentB, playerRadius))
            {
                shotRadius = 0f;
                return true;
            }

            float low = 0f;
            float high = maxShotRadius;

            while (high - low > safeTolerance)
            {
                float mid = 0.5f * (low + high);
                InfectionGraph midClone = SimulateShotOnClone(graph, origin, direction, mid, settings);

                if (PathGeometry.IsSegmentClear(midClone, segmentA, segmentB, playerRadius))
                {
                    high = mid;
                }
                else
                {
                    low = mid;
                }
            }

            shotRadius = high;
            return true;
        }

        /// <summary>
        /// Прогоняет гипотетический выстрел радиуса <paramref name="shotRadius"/> по клону
        /// графа: раскаст первого узла из origin в направлении direction, симуляция волны
        /// заражения от точки попадания, применение результата (Kill) к клону. Оригинальный
        /// граф не меняется. Если раскаст ни во что не попал — возвращает клон без изменений.
        /// </summary>
        public static InfectionGraph SimulateShotOnClone(InfectionGraph graph, Vector3 origin, Vector3 direction,
            float shotRadius, in InfectionSettings settings)
        {
            InfectionGraph clone = graph.Clone();

            bool hasHit = PathGeometry.RaycastFirstNode(clone, origin, direction, shotRadius,
                out _, out Vector3 impactPoint);

            if (!hasHit)
            {
                return clone;
            }

            InfectionSimulator.Simulate(clone, impactPoint, shotRadius, settings, s_hitBuffer);
            foreach (InfectionHit hit in s_hitBuffer)
            {
                clone.Kill(hit.Index);
            }

            return clone;
        }
    }
}
