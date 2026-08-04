using UnityEngine;

namespace Game.Config
{
    /// <summary>
    /// Параметры баланса заряда и полёта выстрела.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Shot Config", fileName = "ShotConfig")]
    public sealed class ShotConfig : ScriptableObject
    {
        [Header("Заряд")]
        [Tooltip("Базовая скорость перетекания массы игрок -> выстрел, масса/сек.")]
        [SerializeField] private float massTransferPerSecond = 1.2f;

        [Tooltip("Множитель скорости перетекания массы в зависимости от времени удержания тапа.")]
        [SerializeField] private AnimationCurve transferRateOverHold = AnimationCurve.Linear(0f, 1f, 2.5f, 2f);

        [Tooltip("Максимальное время удержания тапа, после которого заряд перестаёт расти.")]
        [SerializeField] private float maxHoldTime = 2.5f;

        [Header("Полёт")]
        [SerializeField] private float shotSpeed = 18f;
        [SerializeField] private float minShotRadius = 0.1f;

        [Tooltip("Зазор между поверхностью игрока и стартовой позицией выстрела при спавне.")]
        [SerializeField] private float spawnGap = 0.05f;

        [Tooltip("Максимальное время жизни выстрела в полёте, секунды (страховка от промаха).")]
        [SerializeField] private float maxLifetime = 5f;

        /// <summary>Базовая скорость перетекания массы игрок -> выстрел, масса/сек.</summary>
        public float MassTransferPerSecond => massTransferPerSecond;

        /// <summary>Кривая множителя скорости перетекания массы от времени удержания.</summary>
        public AnimationCurve TransferRateOverHold => transferRateOverHold;

        /// <summary>Максимальное время удержания тапа.</summary>
        public float MaxHoldTime => maxHoldTime;

        /// <summary>Скорость полёта выстрела, метры/сек.</summary>
        public float ShotSpeed => shotSpeed;

        /// <summary>Минимальный радиус выстрела (страховка от нулевого/вырожденного шара).</summary>
        public float MinShotRadius => minShotRadius;

        /// <summary>Зазор между игроком и точкой спавна выстрела.</summary>
        public float SpawnGap => spawnGap;

        /// <summary>Максимальное время жизни выстрела в полёте.</summary>
        public float MaxLifetime => maxLifetime;
    }
}
