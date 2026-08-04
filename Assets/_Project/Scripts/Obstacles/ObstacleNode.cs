using UnityEngine;

namespace Game.Obstacles
{
    /// <summary>
    /// Неизменяемый снимок препятствия для чистой логики заражения: позиция и радиус.
    /// Не содержит ссылок на сцену — <see cref="InfectionGraph"/> и
    /// <see cref="InfectionSimulator"/> работают только с этими данными.
    /// </summary>
    public readonly struct ObstacleNode
    {
        /// <summary>Мировая позиция центра препятствия.</summary>
        public readonly Vector3 Position;

        /// <summary>Радиус препятствия, метры.</summary>
        public readonly float Radius;

        public ObstacleNode(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }
}
