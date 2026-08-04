using Game.Obstacles;
using UnityEngine;

namespace Game.Config
{
    /// <summary>
    /// Параметры баланса модели заражения препятствий. Через <see cref="ToSettings"/>
    /// конвертируется в чистую структуру <see cref="InfectionSettings"/>, от которой
    /// зависит непосредственно <see cref="InfectionSimulator"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Infection Config", fileName = "InfectionConfig")]
    public sealed class InfectionConfig : ScriptableObject
    {
        [Header("Волна заражения")]
        [Tooltip("Начальный радиус заражения = радиус выстрела * этот коэффициент. Главный рычаг " +
                 "стоимости прохождения: определяет, какой выстрел нужен, чтобы пробить в завале " +
                 "дыру шириной с шар. Подобран валидатором бюджета (Game → Validate Level Budget).")]
        [SerializeField] private float infectionRadiusPerShotRadius = 2.55f;

        [Range(0f, 1f)]
        [Tooltip("Доля энергии, сохраняющаяся при переходе волны от узла к соседу.")]
        [SerializeField] private float spreadEfficiency = 0.85f;

        [Tooltip("Штраф энергии за метр зазора между поверхностями соседних препятствий. " +
                 "Чем он выше, тем сильнее заражение зависит от плотности упаковки — и тем " +
                 "короче цепочки. При низком штрафе волна расходится через полкарты и делает " +
                 "прохождение почти бесплатным.")]
        [SerializeField] private float energyCostPerMeter = 2.5f;

        [Tooltip("Минимальная энергия, при которой узел ещё считается заражённым.")]
        [SerializeField] private float minEnergy = 0.01f;

        [Header("Граф соседей")]
        [Tooltip("Максимальный зазор между поверхностями, при котором препятствия считаются соседями.")]
        [SerializeField] private float maxNeighborGap = 2.5f;

        [Header("Визуализация цепной реакции")]
        [Tooltip("Дополнительная задержка взрыва за каждый шаг глубины волны, секунды.")]
        [SerializeField] private float explodeDelayPerDepth = 0.05f;

        [Tooltip("Длительность анимации взрыва одного препятствия, секунды.")]
        [SerializeField] private float explodeDuration = 0.25f;

        /// <summary>Начальный радиус заражения = радиус выстрела * этот коэффициент.</summary>
        public float InfectionRadiusPerShotRadius => infectionRadiusPerShotRadius;

        /// <summary>Доля энергии, сохраняющаяся при переходе волны от узла к соседу.</summary>
        public float SpreadEfficiency => spreadEfficiency;

        /// <summary>Штраф энергии за метр зазора между поверхностями соседних препятствий.</summary>
        public float EnergyCostPerMeter => energyCostPerMeter;

        /// <summary>Минимальная энергия, при которой узел ещё считается заражённым.</summary>
        public float MinEnergy => minEnergy;

        /// <summary>Максимальный зазор между поверхностями, при котором препятствия считаются соседями.</summary>
        public float MaxNeighborGap => maxNeighborGap;

        /// <summary>Дополнительная задержка взрыва за каждый шаг глубины волны, секунды.</summary>
        public float ExplodeDelayPerDepth => explodeDelayPerDepth;

        /// <summary>Длительность анимации взрыва одного препятствия, секунды.</summary>
        public float ExplodeDuration => explodeDuration;

        /// <summary>
        /// Конвертирует конфиг в чистую структуру параметров симуляции заражения,
        /// не зависящую от ScriptableObject.
        /// </summary>
        public InfectionSettings ToSettings()
        {
            return new InfectionSettings(infectionRadiusPerShotRadius, spreadEfficiency,
                energyCostPerMeter, minEnergy);
        }
    }
}
