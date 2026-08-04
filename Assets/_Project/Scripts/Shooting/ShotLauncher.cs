using System;
using System.Collections.Generic;
using Game.Config;
using Game.Level;
using Game.Obstacles;
using Game.Player;
using Game.Utils;
using UnityEngine;

namespace Game.Shooting
{
    /// <summary>
    /// Держит пул <see cref="ShotProjectile"/> и превращает событие «выстрел отпущен» в
    /// реальный полёт + заражение. Точка попадания вычисляется аналитически один раз в
    /// момент запуска (<see cref="PathGeometry.RaycastFirstNode"/>), сам снаряд летит
    /// к уже известной точке чисто визуально.
    /// </summary>
    public sealed class ShotLauncher : MonoBehaviour
    {
        [Header("Пул снарядов")]
        [SerializeField] private ShotProjectile projectilePrefab;
        [SerializeField] private Transform poolParent;
        [SerializeField] private int prewarmCount = 4;

        [Header("Конфиги")]
        [SerializeField] private ShotConfig shotConfig;
        [SerializeField] private InfectionConfig infectionConfig;

        [Header("Ссылки")]
        [SerializeField] private PlayerBall player;
        [SerializeField] private ObstacleField obstacleField;
        [Tooltip("Точка цели (дверь / конец маршрута) — источник направления выстрела: строго на цель, без свободного прицеливания.")]
        [SerializeField] private Transform target;

        private ObjectPool<ShotProjectile> _pool;
        private readonly List<InfectionHit> _hitBuffer = new List<InfectionHit>();

        /// <summary>
        /// Снаряд прибыл к цели: аргумент — попал ли он во что-нибудь, второй — суммарная
        /// длительность запущенной волны взрывов (0, если попадания не было или волна пуста).
        /// </summary>
        public event Action<bool, float> ShotResolved;

        private void Awake()
        {
            _pool = new ObjectPool<ShotProjectile>(projectilePrefab, poolParent, prewarmCount);
        }

        /// <summary>
        /// Берёт из пула экземпляр снаряда для превью заряда (радиус 0, режим превью).
        /// Тот же самый экземпляр затем либо улетает через <see cref="LaunchRented"/>,
        /// либо возвращается в пул через <see cref="ReturnPreview"/> — визуально это один
        /// и тот же шар от начала заряда до выстрела (или отмены), без подмены на глаз.
        /// </summary>
        public ShotProjectile RentPreview()
        {
            ShotProjectile projectile = _pool.Get();
            projectile.Configure(0f);
            return projectile;
        }

        /// <summary>Возвращает в пул экземпляр, взятый через <see cref="RentPreview"/>, без выстрела (отменённый/слишком короткий тап).</summary>
        public void ReturnPreview(ShotProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            _pool.Release(projectile);
        }

        /// <summary>
        /// Запускает в полёт уже взятый через <see cref="RentPreview"/> снаряд массой
        /// <paramref name="shotMass"/> от текущей позиции игрока в направлении цели. Точка
        /// попадания (если она есть) вычисляется сразу же — снаряду остаётся только
        /// визуально долететь до неё.
        /// </summary>
        public void LaunchRented(ShotProjectile projectile, float shotMass)
        {
            if (projectile == null)
            {
                return;
            }

            float shotRadius = MassUtils.RadiusFromMass(shotMass);

            Vector3 playerLogical = player.LogicalPosition;
            Vector3 direction = ComputeDirectionToTarget(playerLogical);

            bool hasHit = PathGeometry.RaycastFirstNode(obstacleField.Graph, playerLogical, direction,
                shotRadius, out _, out Vector3 impactPoint);

            float spawnDistance = player.Radius + shotConfig.SpawnGap + shotRadius;
            Vector3 spawnLogical = playerLogical + direction * spawnDistance;

            // Страховка от промаха "в никуда": снаряд летит не дальше, чем позволяет
            // его максимальное время жизни на паспортной скорости (см. ShotConfig.MaxLifetime).
            float missDistance = shotConfig.ShotSpeed * shotConfig.MaxLifetime;
            Vector3 destinationLogical = hasHit ? impactPoint : spawnLogical + direction * missDistance;

            float visualHeight = player.transform.position.y;
            Vector3 worldSpawn = new Vector3(spawnLogical.x, visualHeight, spawnLogical.z);
            Vector3 worldDestination = new Vector3(destinationLogical.x, visualHeight, destinationLogical.z);

            projectile.Configure(shotRadius);
            projectile.Launch(worldSpawn, worldDestination, shotConfig.ShotSpeed,
                () => OnProjectileArrived(projectile, hasHit, impactPoint, shotRadius));
        }

        private void OnProjectileArrived(ShotProjectile projectile, bool hasHit, Vector3 impactPoint, float shotRadius)
        {
            _pool.Release(projectile);

            float waveDuration = 0f;
            if (hasHit)
            {
                InfectionSimulator.Simulate(obstacleField.Graph, impactPoint, shotRadius,
                    infectionConfig.ToSettings(), _hitBuffer);
                waveDuration = obstacleField.ApplyInfection(_hitBuffer);
            }

            ShotResolved?.Invoke(hasHit, waveDuration);
        }

        private Vector3 ComputeDirectionToTarget(Vector3 playerLogical)
        {
            Vector3 targetLogical = new Vector3(target.position.x, 0f, target.position.z);
            Vector3 direction = targetLogical - playerLogical;
            direction.y = 0f;

            return direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized;
        }
    }
}
