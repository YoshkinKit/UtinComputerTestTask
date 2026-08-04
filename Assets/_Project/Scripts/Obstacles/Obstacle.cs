using UnityEngine;

namespace Game.Obstacles
{
    /// <summary>
    /// Сценовый прокси одного препятствия: сериализуемый радиус и ссылка на визуальное
    /// представление. Источник истины по «жив/мёртв» — <see cref="InfectionGraph"/>
    /// внутри <see cref="ObstacleField"/>; этот компонент лишь зеркалит состояние для
    /// сцены/аниматоров и не участвует в самой симуляции заражения напрямую.
    /// </summary>
    public sealed class Obstacle : MonoBehaviour
    {
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private ObstacleView view;

        private int _nodeIndex = -1;
        private bool _isAlive = true;

        /// <summary>Радиус препятствия, метры (используется при построении <see cref="InfectionGraph"/>).</summary>
        public float Radius => radius;

        /// <summary>Индекс этого препятствия в графе заражения, назначается <see cref="ObstacleField"/>.</summary>
        public int NodeIndex => _nodeIndex;

        /// <summary>Живо ли препятствие (зеркалит состояние графа, обновляется через <see cref="Explode"/>).</summary>
        public bool IsAlive => _isAlive;

        /// <summary>Логическая позиция (проекция на плоскость y = 0) — то, что видит чистая геометрия.</summary>
        public Vector3 LogicalPosition => new Vector3(transform.position.x, 0f, transform.position.z);

        /// <summary>Визуальное представление препятствия.</summary>
        public ObstacleView View => view;

        /// <summary>Назначает индекс узла в графе заражения. Вызывается только <see cref="ObstacleField"/>.</summary>
        public void SetNodeIndex(int index)
        {
            _nodeIndex = index;
        }

        /// <summary>
        /// Задаёт радиус препятствия и обновляет визуальный масштаб. Используется генератором
        /// уровня (<see cref="LevelBuilder"/>) сразу после инстанцирования префаба, до сборки графа.
        /// </summary>
        public void Configure(float newRadius)
        {
            radius = Mathf.Max(0.01f, newRadius);

            if (view != null)
            {
                view.SetRadius(radius);
            }
        }

        /// <summary>Проигрывает визуальный отклик «заражено» (смена цвета/пульс), не убивает узел.</summary>
        public void MarkInfected()
        {
            if (view != null)
            {
                view.PlayInfected();
            }
        }

        /// <summary>
        /// Взрывает препятствие: помечает мёртвым, запускает визуальный взрыв с задержкой
        /// <paramref name="delay"/> (пропорциональной глубине волны заражения) и длительностью
        /// <paramref name="duration"/>, по завершении деактивирует GameObject.
        /// </summary>
        public void Explode(float delay, float duration)
        {
            _isAlive = false;

            if (view != null)
            {
                view.PlayExplosion(delay, duration, OnExplosionComplete);
            }
            else
            {
                OnExplosionComplete();
            }
        }

        private void OnExplosionComplete()
        {
            gameObject.SetActive(false);
        }
    }
}
