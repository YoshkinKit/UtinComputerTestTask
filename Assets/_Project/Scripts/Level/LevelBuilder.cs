using System;
using System.Collections.Generic;
using Game.Config;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Детерминированный генератор раскладки уровня по <see cref="LevelConfig"/>: маршрут,
    /// сужающийся коридор, стены вдоль коридора, плотные кластеры и одиночные препятствия.
    /// Возвращает чистые данные (<see cref="LevelLayout"/>) — ни одного обращения к сцене,
    /// поэтому вся геометрия уровня покрывается обычными EditMode-тестами.
    /// <para/>
    /// Ключевое решение: завалы ставятся не «на точку маршрута», а НА ЛУЧ от точки остановки
    /// игрока к двери. Выстрел в игре летит строго на дверь (свободного прицеливания нет),
    /// поэтому только такое размещение гарантирует, что выстрел попадёт именно в тот завал,
    /// который перекрыл путь, а не в стену рядом. Дистанция вдоль луча подбирается поиском:
    /// завал обязан перекрывать следующий сегмент и не задевать уже пройденный.
    /// </summary>
    public static class LevelBuilder
    {
        /// <summary>
        /// Клиренс для проверки «завал перекрывает сегмент»: намеренно крошечный, чтобы завал
        /// оставался непроходимым даже для шара, ужавшегося почти до критического размера.
        /// </summary>
        private const float BlockClearance = 0.15f;

        /// <summary>Шаг и диапазон поиска дистанции завала вдоль луча на дверь, метры.</summary>
        private const float DistanceSearchStep = 0.05f;
        private const float DistanceSearchRange = 4f;
        private const float DistanceSearchStart = 0.5f;

        /// <summary>
        /// Насколько дальше минимально допустимой дистанции отодвигается завал, метры.
        /// Минимум по построению означает «завал вплотную к шару» — читается как будто шар
        /// стоит внутри кучи. Отступ берётся только пока завал продолжает перекрывать сегмент.
        /// </summary>
        private const float BlockerStandoff = 0.8f;

        /// <summary>Технический зазор при отбраковке стен, залезающих в коридор, метры.</summary>
        private const float WallEpsilon = 0.02f;

        /// <summary>Свободная зона без стен перед дверью и после точки старта, метры.</summary>
        private const float DoorApproachClearance = 1.5f;
        private const float StartApproachClearance = 1f;

        /// <summary>
        /// Строит раскладку уровня. Ширина коридора берётся из проектного профиля сужения
        /// (<see cref="LevelConfig.ExpectedStartRadius"/> → <see cref="LevelConfig.ExpectedEndRadius"/>)
        /// и НЕ подстраивается под фактический размер шара.
        /// <para/>
        /// Это принципиально. Сужающийся коридор — не декорация, а сама причина, по которой
        /// шар обязан похудеть: не влезающий в проход шар упирается в стены и вынужден тратить
        /// массу. Если же ширину подгонять под результат прогона, возникает обратная связь
        /// «шире коридор → шире кластеры → дороже проход → тоньше шар → уже коридор», и уровень
        /// становится бистабильным: соседние значения баланса дают то 6% запаса, то 166%.
        /// Геометрия уровня зафиксирована, а за соответствие баланса ей отвечает
        /// <see cref="LevelBudgetValidator"/>.
        /// </summary>
        /// <param name="config">Параметры генерации.</param>
        /// <param name="maxNeighborGap">
        /// Максимальный зазор между поверхностями, при котором препятствия становятся соседями
        /// в <see cref="Game.Obstacles.InfectionGraph"/>. Нужен, чтобы гарантировать настоящую
        /// изоляцию одиночных препятствий: вокруг них стены сносятся на заведомо больший радиус.
        /// </param>
        public static LevelLayout Build(LevelConfig config, float maxNeighborGap)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Vector3 start = Flatten(config.StartPoint);
            Vector3 end = Flatten(config.EndPoint);

            Vector3 axis = end - start;
            float length = axis.magnitude;
            Vector3 direction = length < 1e-4f ? Vector3.forward : axis / length;
            Vector3 perpendicular = Perpendicular(direction);

            int pathCount = Mathf.Max(6, config.PathPointCount);
            float amplitude = ResolveAmplitude(config, start, end, direction, perpendicular, pathCount, length);

            Vector3[] points = new Vector3[pathCount + 1];
            for (int i = 0; i < pathCount; i++)
            {
                points[i] = PathPoint(start, direction, perpendicular, length, amplitude, Progress(i, pathCount));
            }

            // Последняя точка — за дверью: её достижение и есть победа, отдельный триггер цели не нужен.
            points[pathCount] = end + direction * Mathf.Max(0f, config.PastDoorDistance);

            float margin = Mathf.Max(1f, config.CorridorMarginFactor);
            float[] widths = new float[pathCount + 1];
            for (int i = 0; i <= pathCount; i++)
            {
                widths[i] = 2f * ExpectedRadius(config, Progress(i, pathCount)) * margin;
            }

            var random = new System.Random(config.Seed);
            var obstacles = new List<ObstacleSpec>();
            var blockers = new List<BlockerSpec>();

            // Порядок важен: завалы идут в списке первыми, поэтому последующая отбраковка
            // стен не может сдвинуть индексы, на которые ссылается BlockerSpec.
            AppendBlockers(config, points, widths, pathCount, end, random, obstacles, blockers);
            AppendCorridorWalls(config, points, widths, pathCount, random, obstacles);
            DropWallsIntrudingIntoCorridor(obstacles, points, widths);
            DropWallsAroundSingleBlockers(obstacles, blockers, config, maxNeighborGap);

            Vector3 doorForward = pathCount >= 2
                ? FlatDirection(points[pathCount - 2], points[pathCount - 1], direction)
                : direction;

            return new LevelLayout(points, widths, obstacles.ToArray(), blockers.ToArray(),
                pathCount - 1, doorForward, amplitude);
        }

        /// <summary>
        /// Подбирает амплитуду S-образного изгиба: стартует с заданной в конфиге и уменьшает её,
        /// пока хоть один сегмент маршрута отклоняется от направления на дверь сильнее
        /// <see cref="LevelConfig.MaxPathDeviationDegrees"/>. Ограничение не косметическое:
        /// чем сильнее маршрут уходит в сторону от двери, тем выше шанс, что выстрел (летящий
        /// строго на дверь) уйдёт мимо перекрывшего путь завала.
        /// </summary>
        private static float ResolveAmplitude(LevelConfig config, Vector3 start, Vector3 end,
            Vector3 direction, Vector3 perpendicular, int pathCount, float length)
        {
            float amplitude = Mathf.Max(0f, config.CurveAmplitude);
            float limit = Mathf.Max(1f, config.MaxPathDeviationDegrees);

            for (int guard = 0; guard < 128 && amplitude > 0.01f; guard++)
            {
                if (MaxDeviationDegrees(start, end, direction, perpendicular, length, pathCount, amplitude) <= limit)
                {
                    break;
                }

                amplitude *= 0.9f;
            }

            return amplitude;
        }

        /// <summary>Наибольший угол между сегментом маршрута и направлением на дверь, градусы.</summary>
        private static float MaxDeviationDegrees(Vector3 start, Vector3 end, Vector3 direction,
            Vector3 perpendicular, float length, int pathCount, float amplitude)
        {
            float worst = 0f;
            Vector3 previous = PathPoint(start, direction, perpendicular, length, amplitude, 0f);

            for (int i = 0; i < pathCount - 1; i++)
            {
                Vector3 next = PathPoint(start, direction, perpendicular, length, amplitude, Progress(i + 1, pathCount));
                Vector3 segment = next - previous;
                Vector3 toDoor = end - previous;

                if (segment.sqrMagnitude > 1e-8f && toDoor.sqrMagnitude > 1e-8f)
                {
                    worst = Mathf.Max(worst, Vector3.Angle(segment, toDoor));
                }

                previous = next;
            }

            return worst;
        }

        /// <summary>Точка маршрута: движение вдоль оси старт→финиш плюс синусоидальное боковое отклонение.</summary>
        private static Vector3 PathPoint(Vector3 start, Vector3 direction, Vector3 perpendicular,
            float length, float amplitude, float t)
        {
            float lateral = amplitude * Mathf.Sin(t * Mathf.PI * 2f);
            return start + direction * (length * t) + perpendicular * lateral;
        }

        /// <summary>
        /// Расставляет завалы: индексы остановки распределяются равномерно по маршруту,
        /// одиночные препятствия чередуются с кластерами (первым игрок всегда встречает
        /// кластер — сначала механика цепного заражения, потом точный выстрел по одиночке).
        /// </summary>
        private static void AppendBlockers(LevelConfig config, Vector3[] points, float[] widths, int pathCount,
            Vector3 door, System.Random random, List<ObstacleSpec> obstacles, List<BlockerSpec> blockers)
        {
            int clusterCount = Mathf.Max(0, config.BlockerClusterCount);
            int singleCount = Mathf.Max(0, config.SingleObstacleCount);
            int total = clusterCount + singleCount;
            if (total == 0)
            {
                return;
            }

            int firstStop = 2;
            int lastStop = Mathf.Max(firstStop, pathCount - 3);

            bool[] isSingle = ChooseSingleSlots(total, singleCount);

            for (int slot = 0; slot < total; slot++)
            {
                int stopIndex = total == 1
                    ? (firstStop + lastStop) / 2
                    : Mathf.RoundToInt(Mathf.Lerp(firstStop, lastStop, slot / (float)(total - 1)));

                stopIndex = Mathf.Clamp(stopIndex, 1, pathCount - 2);

                ObstacleRole role = isSingle[slot] ? ObstacleRole.SingleBlocker : ObstacleRole.BlockerCluster;
                int firstObstacle = obstacles.Count;

                AppendBlockerGroup(config, points, widths, stopIndex, door, role, random, obstacles);

                blockers.Add(new BlockerSpec(stopIndex, role, firstObstacle, obstacles.Count - firstObstacle));
            }
        }

        /// <summary>Равномерно распределяет одиночные препятствия по слотам завалов, не занимая крайние.</summary>
        private static bool[] ChooseSingleSlots(int total, int singleCount)
        {
            var isSingle = new bool[total];
            if (singleCount <= 0 || total <= 2)
            {
                return isSingle;
            }

            int usable = Mathf.Min(singleCount, total - 2);
            for (int s = 0; s < usable; s++)
            {
                int slot = usable == 1
                    ? total / 2
                    : Mathf.RoundToInt(Mathf.Lerp(1, total - 2, s / (float)(usable - 1)));

                isSingle[Mathf.Clamp(slot, 1, total - 2)] = true;
            }

            return isSingle;
        }

        /// <summary>
        /// Строит один завал и добавляет его препятствия в общий список. Дистанция вдоль луча
        /// на дверь подбирается перебором: берётся первая, при которой завал уже не задевает
        /// пройденный сегмент, но ещё перекрывает следующий.
        /// </summary>
        private static void AppendBlockerGroup(LevelConfig config, Vector3[] points, float[] widths,
            int stopIndex, Vector3 door, ObstacleRole role, System.Random random, List<ObstacleSpec> obstacles)
        {
            Vector3 stopPoint = points[stopIndex];
            Vector3 previousPoint = points[Mathf.Max(0, stopIndex - 1)];
            Vector3 nextPoint = points[stopIndex + 1];

            Vector3 fallbackDirection = FlatDirection(previousPoint, stopPoint, Vector3.forward);
            Vector3 rayDirection = FlatDirection(stopPoint, door, fallbackDirection);

            // Самый большой шар, который вообще помещается в коридор в этой точке. Проверять
            // «не задевает пройденный сегмент» нужно именно им: реальный радиус игрока здесь
            // заранее неизвестен, а этот — заведомо верхняя граница.
            float maxBallRadius = 0.5f * widths[stopIndex];

            var buffer = new List<ObstacleSpec>();
            var accepted = new List<ObstacleSpec>();

            float clusterRadius = RandomRange(random, config.ClusterObstacleRadiusRange);
            float clusterGap = RandomRange(random, config.ClusterInnerGapRange);
            int clusterCount = RandomRange(random, config.ClusterObstacleCountRange);
            float singleRadius = RandomRange(random, config.SingleObstacleRadiusRange);

            float limit = DistanceSearchStart + DistanceSearchRange;
            float firstValid = -1f;

            for (float distance = DistanceSearchStart; distance <= limit; distance += DistanceSearchStep)
            {
                buffer.Clear();
                Vector3 center = stopPoint + rayDirection * distance;

                if (role == ObstacleRole.SingleBlocker)
                {
                    buffer.Add(new ObstacleSpec(center, singleRadius, role));
                }
                else
                {
                    FillCluster(buffer, center, rayDirection, widths[stopIndex],
                        clusterRadius, clusterGap, clusterCount);
                }

                bool valid = !Blocks(buffer, previousPoint, stopPoint, maxBallRadius)
                             && Blocks(buffer, stopPoint, nextPoint, BlockClearance);

                if (!valid)
                {
                    // Как только за пределами минимума завал перестал перекрывать сегмент,
                    // дальше отодвигать нельзя — оставляем последний рабочий вариант.
                    if (firstValid >= 0f)
                    {
                        break;
                    }

                    continue;
                }

                if (firstValid < 0f)
                {
                    firstValid = distance;
                }

                accepted.Clear();
                accepted.AddRange(buffer);

                if (distance - firstValid >= BlockerStandoff)
                {
                    break;
                }
            }

            if (accepted.Count == 0)
            {
                // Подходящая дистанция не нашлась (вырожденная геометрия сегмента) — ставим
                // завал сразу за границей пройденного сегмента, чтобы уровень остался связным.
                Vector3 center = stopPoint + rayDirection * (maxBallRadius + clusterRadius + 0.5f);
                if (role == ObstacleRole.SingleBlocker)
                {
                    accepted.Add(new ObstacleSpec(center, singleRadius, role));
                }
                else
                {
                    FillCluster(accepted, center, rayDirection, widths[stopIndex],
                        clusterRadius, clusterGap, clusterCount);
                }
            }

            obstacles.AddRange(accepted);
        }

        /// <summary>
        /// Заполняет плотный кластер: ряды поперёк луча выстрела перекрывают всё сечение
        /// коридора с запасом, зазор между поверхностями — <paramref name="gap"/> (маленький,
        /// чтобы волна заражения гарантированно шла по цепочке).
        /// </summary>
        private static void FillCluster(List<ObstacleSpec> target, Vector3 center, Vector3 rayDirection,
            float corridorWidth, float radius, float gap, int desiredCount)
        {
            Vector3 lateral = Perpendicular(rayDirection);

            float pitch = 2f * radius + gap;
            float span = corridorWidth * 1.3f + 2f * radius;
            int columns = Mathf.Max(2, Mathf.CeilToInt(span / pitch));
            int rows = Mathf.Clamp(Mathf.CeilToInt(desiredCount / (float)columns), 1, 3);

            for (int row = 0; row < rows; row++)
            {
                float depthOffset = (row - (rows - 1) * 0.5f) * pitch;

                for (int column = 0; column < columns; column++)
                {
                    float lateralOffset = (column - (columns - 1) * 0.5f) * pitch;
                    Vector3 position = center + lateral * lateralOffset + rayDirection * depthOffset;
                    target.Add(new ObstacleSpec(Flatten(position), radius, ObstacleRole.BlockerCluster));
                }
            }
        }

        /// <summary>Перекрывает ли хоть одно препятствие группы отрезок a→b для шара радиуса <paramref name="radius"/>.</summary>
        private static bool Blocks(List<ObstacleSpec> group, Vector3 a, Vector3 b, float radius)
        {
            foreach (ObstacleSpec spec in group)
            {
                if (PathGeometry.DistancePointToSegment(spec.Position, a, b) < spec.Radius + radius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Расставляет стены коридора: пары препятствий по обе стороны маршрута с шагом
        /// <see cref="LevelConfig.WallSpacingAlongPath"/>. Внутренняя поверхность стены стоит
        /// ровно на границе коридора и наружу от неё — коридор они не сужают, но делают
        /// видимым и служат «проводниками» цепного заражения от кластеров.
        /// </summary>
        private static void AppendCorridorWalls(LevelConfig config, Vector3[] points, float[] widths,
            int pathCount, System.Random random, List<ObstacleSpec> obstacles)
        {
            float spacing = Mathf.Max(0.2f, config.WallSpacingAlongPath);
            float totalLength = PolylineLength(points, pathCount);
            float doorLimit = totalLength - DoorApproachClearance;

            float traveled = 0f;
            float nextSample = StartApproachClearance;

            for (int i = 0; i < pathCount - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 segment = b - a;
                float segmentLength = segment.magnitude;
                if (segmentLength < 1e-4f)
                {
                    continue;
                }

                Vector3 tangent = segment / segmentLength;
                Vector3 lateral = Perpendicular(tangent);

                while (nextSample < traveled + segmentLength)
                {
                    if (nextSample > doorLimit)
                    {
                        return;
                    }

                    float u = (nextSample - traveled) / segmentLength;
                    Vector3 position = a + segment * u;
                    float halfWidth = 0.5f * Mathf.Lerp(widths[i], widths[i + 1], u);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        float radius = RandomRange(random, config.WallObstacleRadiusRange);
                        float jitter = (float)random.NextDouble() * Mathf.Max(0f, config.WallJitter);

                        // Технический зазор входит в отступ, иначе стена, поставленная ровно на
                        // границу коридора, тут же отбраковывалась бы проверкой ниже.
                        float offset = halfWidth + radius + WallEpsilon + jitter;
                        Vector3 wallPosition = position + lateral * (side * offset);
                        obstacles.Add(new ObstacleSpec(Flatten(wallPosition), radius, ObstacleRole.CorridorWall));
                    }

                    nextSample += spacing;
                }

                traveled += segmentLength;
            }
        }

        /// <summary>
        /// Отбраковывает стены, которые из-за изгиба маршрута всё-таки залезли внутрь коридора.
        /// Проверка идёт по всей ломаной, а не по одному сегменту: на поворотах ближайшим к
        /// стене может оказаться соседний сегмент, а не тот, вдоль которого её ставили.
        /// </summary>
        private static void DropWallsIntrudingIntoCorridor(List<ObstacleSpec> obstacles, Vector3[] points, float[] widths)
        {
            obstacles.RemoveAll(spec =>
            {
                if (spec.Role != ObstacleRole.CorridorWall)
                {
                    return false;
                }

                for (int i = 0; i < points.Length - 1; i++)
                {
                    Vector3 segment = points[i + 1] - points[i];
                    float lengthSqr = segment.sqrMagnitude;
                    float t = lengthSqr < 1e-12f
                        ? 0f
                        : Mathf.Clamp01(Vector3.Dot(spec.Position - points[i], segment) / lengthSqr);

                    // Ширина берётся именно в ближайшей точке сегмента: коридор сужается вдоль
                    // маршрута, и сравнение с шириной на его концах отбраковывало бы стены,
                    // стоящие корректно.
                    float distance = Vector3.Distance(spec.Position, points[i] + segment * t);
                    float halfWidth = 0.5f * Mathf.Lerp(widths[i], widths[i + 1], t);

                    if (distance - halfWidth < spec.Radius)
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        /// <summary>
        /// Расчищает площадку вокруг одиночных препятствий: сносит стены ближе радиуса изоляции.
        /// Радиус берётся с запасом относительно <paramref name="maxNeighborGap"/>, поэтому
        /// одиночное препятствие гарантированно остаётся без соседей в графе заражения —
        /// цепная реакция до него не дотянется, нужен прямой выстрел.
        /// </summary>
        private static void DropWallsAroundSingleBlockers(List<ObstacleSpec> obstacles, List<BlockerSpec> blockers,
            LevelConfig config, float maxNeighborGap)
        {
            foreach (BlockerSpec blocker in blockers)
            {
                if (blocker.Role != ObstacleRole.SingleBlocker || blocker.ObstacleCount == 0)
                {
                    continue;
                }

                ObstacleSpec single = obstacles[blocker.FirstObstacle];
                float required = maxNeighborGap + single.Radius + config.WallObstacleRadiusRange.y + WallEpsilon;
                float isolation = Mathf.Max(config.SingleObstacleIsolationDistance, required);

                obstacles.RemoveAll(spec => spec.Role == ObstacleRole.CorridorWall
                                            && Vector3.Distance(spec.Position, single.Position) < isolation);
            }
        }

        /// <summary>Длина ломаной по точкам маршрута до двери включительно.</summary>
        private static float PolylineLength(Vector3[] points, int pathCount)
        {
            float total = 0f;
            for (int i = 0; i < pathCount - 1; i++)
            {
                total += Vector3.Distance(points[i], points[i + 1]);
            }

            return total;
        }

        /// <summary>Ожидаемый радиус игрока в доле <paramref name="t"/> маршрута.</summary>
        private static float ExpectedRadius(LevelConfig config, float t)
        {
            return Mathf.Lerp(config.ExpectedStartRadius, config.ExpectedEndRadius, Mathf.Clamp01(t));
        }

        private static float Progress(int index, int pathCount)
        {
            return pathCount <= 1 ? 0f : index / (float)(pathCount - 1);
        }

        private static Vector3 Perpendicular(Vector3 direction)
        {
            return new Vector3(-direction.z, 0f, direction.x);
        }

        private static Vector3 Flatten(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private static Vector3 FlatDirection(Vector3 from, Vector3 to, Vector3 fallback)
        {
            Vector3 delta = Flatten(to) - Flatten(from);
            return delta.sqrMagnitude < 1e-8f ? fallback : delta.normalized;
        }

        private static float RandomRange(System.Random random, Vector2 range)
        {
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        private static int RandomRange(System.Random random, Vector2Int range)
        {
            int min = Mathf.Min(range.x, range.y);
            int max = Mathf.Max(range.x, range.y);
            return random.Next(min, max + 1);
        }
    }
}
