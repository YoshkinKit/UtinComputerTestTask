using Game.Utils;
using UnityEngine;

namespace Game.Config
{
    /// <summary>
    /// Параметры баланса шара-игрока: стартовый и критический размер, тайминги прыжка.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Header("Размер")]
        [SerializeField] private float startRadius = 1.2f;
        [SerializeField] private float criticalRadius = 0.35f;

        [Header("Прыжок")]
        [SerializeField] private float hopDuration = 0.35f;
        [SerializeField] private float hopHeight = 0.8f;
        [SerializeField] private float hopPauseDuration = 0.05f;

        [Header("Коридор")]
        [Tooltip("Множитель зазора: ширина коридора = 2 * ожидаемый радиус игрока * этот коэффициент.")]
        [SerializeField] private float clearanceFactor = 1.05f;

        /// <summary>Стартовый радиус шара-игрока, метры.</summary>
        public float StartRadius => startRadius;

        /// <summary>Критический радиус: при достижении (или ниже) — мгновенный проигрыш.</summary>
        public float CriticalRadius => criticalRadius;

        /// <summary>Длительность одного прыжка вдоль коридора, секунды.</summary>
        public float HopDuration => hopDuration;

        /// <summary>Высота дуги прыжка, метры.</summary>
        public float HopHeight => hopHeight;

        /// <summary>Пауза между прыжками, секунды.</summary>
        public float HopPauseDuration => hopPauseDuration;

        /// <summary>Коэффициент запаса ширины коридора относительно радиуса игрока.</summary>
        public float ClearanceFactor => clearanceFactor;

        /// <summary>Стартовая масса игрока, полученная из стартового радиуса.</summary>
        public float StartMass => MassUtils.MassFromRadius(startRadius);

        /// <summary>Критическая масса игрока, полученная из критического радиуса.</summary>
        public float CriticalMass => MassUtils.MassFromRadius(criticalRadius);
    }
}
