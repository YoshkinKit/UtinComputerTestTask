using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Obstacles
{
    /// <summary>
    /// Чистая (без MonoBehaviour и ScriptableObject) симуляция волны заражения по графу
    /// препятствий. Единственный публичный метод — <see cref="Simulate"/>. Переиспользуется
    /// тремя потребителями: рантайм-взрывами, редакторным валидатором бюджета уровня
    /// (бинарный поиск минимального радиуса выстрела) и детектором проигрыша «не хватило массы».
    /// </summary>
    public static class InfectionSimulator
    {
        // Рабочие буферы кэшируются между вызовами и переиспользуются по размеру графа,
        // т.к. метод вызывается очень часто (в т.ч. сотни раз подряд в бинарном поиске валидатора).
        private static float[] s_bestEnergy = Array.Empty<float>();
        private static int[] s_bestDepth = Array.Empty<int>();
        private static readonly List<int> s_queue = new List<int>();

        /// <summary>
        /// Прогоняет волну заражения от точки попадания выстрела и заполняет
        /// <paramref name="results"/> заражёнными живыми узлами графа.
        /// </summary>
        /// <param name="graph">Граф препятствий (используется его текущее состояние живости).</param>
        /// <param name="impactPoint">Точка попадания выстрела (центр шара-выстрела в момент касания).</param>
        /// <param name="shotRadius">Радиус шара-выстрела в момент попадания.</param>
        /// <param name="settings">Параметры модели заражения.</param>
        /// <param name="results">
        /// Выходной список: очищается в начале и заполняется хитами, отсортированными
        /// по (Depth, Index) — результат детерминирован при одинаковых входных данных.
        /// </param>
        public static void Simulate(InfectionGraph graph, Vector3 impactPoint, float shotRadius,
            in InfectionSettings settings, List<InfectionHit> results)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            int count = graph.Count;
            if (count == 0)
            {
                return;
            }

            EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                s_bestEnergy[i] = 0f;
                s_bestDepth[i] = 0;
            }

            s_queue.Clear();

            float infectionRadius = shotRadius * settings.RadiusPerShotRadius;

            // 1. Посев: прямое попадание заражает все живые узлы в радиусе R с остаточной
            // энергией E = R - surfaceDist.
            for (int i = 0; i < count; i++)
            {
                if (!graph.IsAlive(i))
                {
                    continue;
                }

                ObstacleNode node = graph.GetNode(i);
                float surfaceDist = Mathf.Max(0f, Vector3.Distance(impactPoint, node.Position) - node.Radius);
                if (surfaceDist > infectionRadius)
                {
                    continue;
                }

                float energy = infectionRadius - surfaceDist;
                if (energy > s_bestEnergy[i])
                {
                    s_bestEnergy[i] = energy;
                    s_bestDepth[i] = 0;
                    s_queue.Add(i);
                }
            }

            // 2. Волна: обход в ширину с релаксацией энергии (как в алгоритме Беллмана-Форда) —
            // узел может быть переоткрыт повторно, если к нему пришла бОльшая энергия, чем
            // была записана ранее. Это не обычный BFS без релаксации: без него волна не
            // распространялась бы дальше по плотным кластерам корректно.
            for (int qi = 0; qi < s_queue.Count; qi++)
            {
                int i = s_queue[qi];
                float energy = s_bestEnergy[i];
                int depth = s_bestDepth[i];

                graph.GetNeighbors(i, out int start, out int neighborCount);
                for (int n = 0; n < neighborCount; n++)
                {
                    int slot = start + n;
                    int j = graph.GetNeighborIndex(slot);
                    if (!graph.IsAlive(j))
                    {
                        continue;
                    }

                    float gap = Mathf.Max(0f, graph.GetNeighborGap(slot));
                    float nextEnergy = energy * settings.SpreadEfficiency - gap * settings.EnergyCostPerMeter;

                    if (nextEnergy > settings.MinEnergy && nextEnergy > s_bestEnergy[j])
                    {
                        s_bestEnergy[j] = nextEnergy;
                        s_bestDepth[j] = depth + 1;
                        s_queue.Add(j);
                    }
                }
            }

            // 3. Сбор результатов: все узлы с итоговой энергией выше порога.
            for (int i = 0; i < count; i++)
            {
                if (s_bestEnergy[i] > settings.MinEnergy)
                {
                    results.Add(new InfectionHit(i, s_bestDepth[i], s_bestEnergy[i]));
                }
            }

            // 4. Строго детерминированная сортировка — обязательна для воспроизводимости
            // между рантаймом, валидатором и тестами.
            results.Sort(CompareHits);
        }

        private static int CompareHits(InfectionHit a, InfectionHit b)
        {
            int depthCompare = a.Depth.CompareTo(b.Depth);
            return depthCompare != 0 ? depthCompare : a.Index.CompareTo(b.Index);
        }

        private static void EnsureCapacity(int count)
        {
            if (s_bestEnergy.Length < count)
            {
                s_bestEnergy = new float[count];
                s_bestDepth = new int[count];
            }
        }
    }
}
