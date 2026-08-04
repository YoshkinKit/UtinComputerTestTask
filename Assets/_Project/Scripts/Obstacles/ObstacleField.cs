using System;
using System.Collections;
using System.Collections.Generic;
using Game.Config;
using UnityEngine;

namespace Game.Obstacles
{
    /// <summary>
    /// Собирает все <see cref="Obstacle"/> в детях, строит по ним <see cref="InfectionGraph"/>
    /// и служит единственной точкой применения результатов заражения к сцене: убивает узлы
    /// графа и просит соответствующие <see cref="Obstacle"/> проиграть взрыв с задержкой,
    /// пропорциональной глубине волны.
    /// </summary>
    public sealed class ObstacleField : MonoBehaviour
    {
        [SerializeField] private InfectionConfig infectionConfig;

        private Obstacle[] _obstacles = Array.Empty<Obstacle>();
        private InfectionGraph _graph;

        /// <summary>Раскрывается после того, как волна взрывов, вызванная <see cref="ApplyInfection"/>, полностью отыграла.</summary>
        public event Action FieldChanged;

        /// <summary>Граф заражения текущего поля препятствий.</summary>
        public InfectionGraph Graph => _graph;

        /// <summary>Количество живых препятствий на данный момент.</summary>
        public int AliveCount { get; private set; }

        private void Awake()
        {
            Rebuild();
        }

        /// <summary>Препятствие по индексу узла в графе.</summary>
        public Obstacle GetObstacle(int index) => _obstacles[index];

        /// <summary>
        /// Применяет уже посчитанный список заражённых узлов: убивает их в графе, запускает
        /// у соответствующих препятствий взрыв с задержкой hit.Depth * explodeDelayPerDepth
        /// (видимая цепная реакция расходится по волне) и возвращает суммарную длительность
        /// волны (максимальная задержка + explodeDuration) — контроллер игры ждёт именно
        /// столько, прежде чем считать путь окончательно расчищенным.
        /// </summary>
        public float ApplyInfection(List<InfectionHit> hits)
        {
            if (hits == null || hits.Count == 0)
            {
                return 0f;
            }

            float maxDelay = 0f;

            foreach (InfectionHit hit in hits)
            {
                if (!_graph.IsAlive(hit.Index))
                {
                    continue;
                }

                _graph.Kill(hit.Index);
                AliveCount--;

                float delay = hit.Depth * infectionConfig.ExplodeDelayPerDepth;
                if (delay > maxDelay)
                {
                    maxDelay = delay;
                }

                Obstacle obstacle = _obstacles[hit.Index];
                obstacle.MarkInfected();
                obstacle.Explode(delay, infectionConfig.ExplodeDuration);
            }

            float totalDuration = maxDelay + infectionConfig.ExplodeDuration;
            StartCoroutine(NotifyAfterWave(totalDuration));
            return totalDuration;
        }

        /// <summary>Пересобирает граф заражения с нуля по текущим дочерним <see cref="Obstacle"/> (для генератора уровня).</summary>
        public void Rebuild()
        {
            _obstacles = GetComponentsInChildren<Obstacle>(includeInactive: true);

            var nodes = new List<ObstacleNode>(_obstacles.Length);
            for (int i = 0; i < _obstacles.Length; i++)
            {
                _obstacles[i].SetNodeIndex(i);
                nodes.Add(new ObstacleNode(_obstacles[i].LogicalPosition, _obstacles[i].Radius));
            }

            _graph = new InfectionGraph(nodes, infectionConfig.MaxNeighborGap);
            AliveCount = _obstacles.Length;
        }

        private IEnumerator NotifyAfterWave(float duration)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            FieldChanged?.Invoke();
        }
    }
}
