using Game.Obstacles;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Чистая геометрия для проверки прохода шара по коридору и для детекта первого
    /// столкновения летящего выстрела с препятствием. Без MonoBehaviour и обращений к сцене —
    /// работает только с <see cref="InfectionGraph"/> и явными точками/радиусами.
    /// </summary>
    public static class PathGeometry
    {
        /// <summary>Кратчайшее расстояние от точки до отрезка [a, b].</summary>
        public static float DistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-12f)
            {
                return Vector3.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSqr);
            Vector3 closest = a + ab * t;
            return Vector3.Distance(point, closest);
        }

        /// <summary>
        /// Пройдёт ли шар радиуса <paramref name="radius"/> по отрезку a→b, не задев ни один
        /// живой узел графа (узел мешает, если расстояние от его центра до отрезка меньше
        /// node.Radius + radius).
        /// </summary>
        public static bool IsSegmentClear(InfectionGraph graph, Vector3 a, Vector3 b, float radius)
        {
            int count = graph.Count;
            for (int i = 0; i < count; i++)
            {
                if (!graph.IsAlive(i))
                {
                    continue;
                }

                ObstacleNode node = graph.GetNode(i);
                float distance = DistancePointToSegment(node.Position, a, b);
                if (distance < node.Radius + radius)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Индекс живого узла, первым встреченного вдоль отрезка a→b (т.е. с минимальным
        /// параметром t ближайшей на отрезке точки, а не с минимальным «сырым» евклидовым
        /// расстоянием до <paramref name="a"/> — это разные критерии, если узлы лежат по
        /// разные стороны коридора). При равном t тай-брейк — по меньшему индексу узла.
        /// -1, если путь чист.
        /// </summary>
        public static int FindFirstBlocker(InfectionGraph graph, Vector3 a, Vector3 b, float radius)
        {
            int bestIndex = -1;
            float bestT = float.MaxValue;
            int count = graph.Count;

            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;

            for (int i = 0; i < count; i++)
            {
                if (!graph.IsAlive(i))
                {
                    continue;
                }

                ObstacleNode node = graph.GetNode(i);
                float segmentDistance = DistancePointToSegment(node.Position, a, b);
                if (segmentDistance >= node.Radius + radius)
                {
                    continue;
                }

                float t = lengthSqr < 1e-12f ? 0f : Mathf.Clamp01(Vector3.Dot(node.Position - a, ab) / lengthSqr);

                // Строгое "<" при переборе индексов по возрастанию само по себе даёт
                // тай-брейк по меньшему индексу при равном t.
                if (t < bestT)
                {
                    bestT = t;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Первое касание летящего шара-выстрела радиуса <paramref name="shotRadius"/> с живым
        /// узлом графа: луч из <paramref name="origin"/> в направлении <paramref name="direction"/>
        /// проверяется против сфер препятствий, раздутых на shotRadius (сумма Минковского).
        /// <paramref name="impactPoint"/> — центр шара-выстрела в момент касания.
        /// </summary>
        public static bool RaycastFirstNode(InfectionGraph graph, Vector3 origin, Vector3 direction,
            float shotRadius, out int index, out Vector3 impactPoint)
        {
            index = -1;
            impactPoint = origin;

            Vector3 dir = direction;
            float dirLength = dir.magnitude;
            if (dirLength < 1e-12f)
            {
                return false;
            }

            dir /= dirLength;

            float bestT = float.MaxValue;
            int count = graph.Count;

            for (int i = 0; i < count; i++)
            {
                if (!graph.IsAlive(i))
                {
                    continue;
                }

                ObstacleNode node = graph.GetNode(i);
                float inflatedRadius = node.Radius + shotRadius;

                Vector3 toCenter = node.Position - origin;
                float tClosest = Vector3.Dot(toCenter, dir);
                float perpDistSqr = toCenter.sqrMagnitude - tClosest * tClosest;
                float inflatedRadiusSqr = inflatedRadius * inflatedRadius;

                if (perpDistSqr > inflatedRadiusSqr)
                {
                    continue;
                }

                float halfChord = Mathf.Sqrt(Mathf.Max(0f, inflatedRadiusSqr - perpDistSqr));
                float tEnter = tClosest - halfChord;
                float tExit = tClosest + halfChord;

                if (tExit < 0f)
                {
                    // Сфера целиком позади точки старта луча — промах в эту сторону.
                    continue;
                }

                // Если старт луча уже внутри раздутой сферы, касание засчитывается немедленно (t = 0).
                float tHit = Mathf.Max(0f, tEnter);

                if (tHit < bestT)
                {
                    bestT = tHit;
                    index = i;
                }
            }

            if (index < 0)
            {
                return false;
            }

            impactPoint = origin + dir * bestT;
            return true;
        }
    }
}
