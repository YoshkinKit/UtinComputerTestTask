using System.Collections.Generic;
using UnityEngine;

namespace Game.Obstacles
{
    /// <summary>
    /// Граф соседства препятствий для чистой симуляции заражения. Топология (кто с кем
    /// сосед и с каким зазором) считается один раз в конструкторе и хранится в плоских
    /// массивах, чтобы перечисление соседей во время симуляции не создавало аллокаций.
    /// Состояние «жив/убит» отделено от топологии, поэтому его можно клонировать для
    /// гипотетических прогонов (валидатор бюджета уровня, детектор проигрыша), не
    /// пересчитывая соседей заново.
    /// </summary>
    public sealed class InfectionGraph
    {
        private readonly ObstacleNode[] _nodes;
        private readonly int[] _neighborStart;
        private readonly int[] _neighborIndices;
        private readonly float[] _neighborGaps;
        private readonly bool[] _alive;

        /// <summary>
        /// Строит граф соседства по списку препятствий. Два узла считаются соседями,
        /// если зазор между их поверхностями не превышает <paramref name="maxNeighborGap"/>.
        /// Отрицательный зазор (пересекающиеся препятствия) сохраняется как ноль.
        /// </summary>
        public InfectionGraph(IReadOnlyList<ObstacleNode> nodes, float maxNeighborGap)
        {
            int count = nodes.Count;
            _nodes = new ObstacleNode[count];
            for (int i = 0; i < count; i++)
            {
                _nodes[i] = nodes[i];
            }

            _alive = new bool[count];
            ReviveAll();

            var adjacencyIndices = new List<int>[count];
            var adjacencyGaps = new List<float>[count];
            for (int i = 0; i < count; i++)
            {
                adjacencyIndices[i] = new List<int>();
                adjacencyGaps[i] = new List<float>();
            }

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    float centerDistance = Vector3.Distance(_nodes[i].Position, _nodes[j].Position);
                    float gap = centerDistance - _nodes[i].Radius - _nodes[j].Radius;
                    if (gap > maxNeighborGap)
                    {
                        continue;
                    }

                    float clampedGap = Mathf.Max(0f, gap);
                    adjacencyIndices[i].Add(j);
                    adjacencyGaps[i].Add(clampedGap);
                    adjacencyIndices[j].Add(i);
                    adjacencyGaps[j].Add(clampedGap);
                }
            }

            _neighborStart = new int[count + 1];
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += adjacencyIndices[i].Count;
            }

            _neighborIndices = new int[total];
            _neighborGaps = new float[total];

            int cursor = 0;
            for (int i = 0; i < count; i++)
            {
                _neighborStart[i] = cursor;
                List<int> indices = adjacencyIndices[i];
                List<float> gaps = adjacencyGaps[i];
                for (int k = 0; k < indices.Count; k++)
                {
                    _neighborIndices[cursor] = indices[k];
                    _neighborGaps[cursor] = gaps[k];
                    cursor++;
                }
            }

            _neighborStart[count] = cursor;
        }

        /// <summary>Приватный конструктор для <see cref="Clone"/>: топология переиспользуется без пересчёта.</summary>
        private InfectionGraph(ObstacleNode[] nodes, int[] neighborStart, int[] neighborIndices, float[] neighborGaps)
        {
            _nodes = nodes;
            _neighborStart = neighborStart;
            _neighborIndices = neighborIndices;
            _neighborGaps = neighborGaps;
            _alive = new bool[nodes.Length];
        }

        /// <summary>Количество узлов в графе.</summary>
        public int Count => _nodes.Length;

        /// <summary>Данные узла (позиция, радиус) по индексу.</summary>
        public ObstacleNode GetNode(int index) => _nodes[index];

        /// <summary>Жив ли узел (не взорван).</summary>
        public bool IsAlive(int index) => _alive[index];

        /// <summary>Помечает узел уничтоженным.</summary>
        public void Kill(int index)
        {
            _alive[index] = false;
        }

        /// <summary>Возвращает всем узлам живое состояние.</summary>
        public void ReviveAll()
        {
            for (int i = 0; i < _alive.Length; i++)
            {
                _alive[i] = true;
            }
        }

        /// <summary>
        /// Создаёт копию графа с независимым состоянием живости узлов (текущим на момент
        /// вызова), но с общей (переиспользуемой) топологией соседей — без пересчёта.
        /// Используется для гипотетических прогонов, которые не должны влиять на оригинал.
        /// </summary>
        public InfectionGraph Clone()
        {
            var clone = new InfectionGraph(_nodes, _neighborStart, _neighborIndices, _neighborGaps);
            System.Array.Copy(_alive, clone._alive, _alive.Length);
            return clone;
        }

        /// <summary>
        /// Возвращает диапазон [start, start + count) в плоских массивах соседей для узла index.
        /// Без аллокаций — вызывающий код читает <see cref="GetNeighborIndex"/> и
        /// <see cref="GetNeighborGap"/> по индексам слота в этом диапазоне.
        /// </summary>
        public void GetNeighbors(int index, out int start, out int count)
        {
            start = _neighborStart[index];
            count = _neighborStart[index + 1] - start;
        }

        /// <summary>Индекс узла-соседа для слота, полученного из <see cref="GetNeighbors"/>.</summary>
        public int GetNeighborIndex(int slot) => _neighborIndices[slot];

        /// <summary>Зазор между поверхностями для слота, полученного из <see cref="GetNeighbors"/>.</summary>
        public float GetNeighborGap(int slot) => _neighborGaps[slot];
    }
}
