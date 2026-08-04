using System;
using Game.Config;
using Game.Core;
using Game.Inputs;
using Game.Shooting;
using Game.Utils;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Перетекание массы игрок → выстрел, пока тап удержан. Активен только когда
    /// <see cref="GameController.State"/> == <see cref="GameState.Idle"/>. На старте заряда
    /// берёт экземпляр снаряда из пула <see cref="ShotLauncher"/> (<see cref="ShotLauncher.RentPreview"/>)
    /// и ведёт его у края игрока в направлении цели; при отпускании ровно этот же экземпляр
    /// улетает (<see cref="ShotLauncher.LaunchRented"/>) — подмены снаряда на глаз не происходит.
    /// </summary>
    public sealed class ChargeController : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private GameController gameController;
        [SerializeField] private PlayerBall player;
        [Tooltip("Визуал шара — только для обратной связи заряда/выстрела, на логику не влияет.")]
        [SerializeField] private PlayerBallView playerView;
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private ShotConfig shotConfig;
        [SerializeField] private ShotLauncher shotLauncher;
        [Tooltip("Точка цели (дверь / конец маршрута) — направление, куда растёт превью-шар выстрела.")]
        [SerializeField] private Transform target;

        private float _holdTime;
        private float _shotMass;
        private bool _isCharging;
        private ShotProjectile _rentedProjectile;

        /// <summary>Текущий радиус заряжаемого выстрела (для HUD).</summary>
        public event Action<float> ChargeChanged;

        /// <summary>Тап отпущен над валидным выстрелом — аргумент: масса, перешедшая в выстрел, и сам запущенный снаряд.</summary>
        public event Action<float, ShotProjectile> ShotReleased;

        /// <summary>Игрок перекачал всю массу ниже критической — немедленный проигрыш.</summary>
        public event Action Overcharged;

        /// <summary>Идёт ли сейчас заряд.</summary>
        public bool IsCharging => _isCharging;

        private void OnEnable()
        {
            inputReader.PressStarted += HandlePressStarted;
            inputReader.PressReleased += HandlePressReleased;
        }

        private void OnDisable()
        {
            inputReader.PressStarted -= HandlePressStarted;
            inputReader.PressReleased -= HandlePressReleased;
        }

        private void Update()
        {
            if (!_isCharging)
            {
                return;
            }

            _holdTime += Time.deltaTime;

            // Скорость перетекания растёт по кривой до MaxHoldTime, а дальше остаётся
            // постоянной на максимуме — перетекание НЕ останавливается по истечении
            // MaxHoldTime, иначе условие проигрыша "перекачал всю массу" было бы недостижимо
            // при короткой кривой заряда.
            float evaluatedHold = Mathf.Min(_holdTime, shotConfig.MaxHoldTime);
            float rate = shotConfig.MassTransferPerSecond * shotConfig.TransferRateOverHold.Evaluate(evaluatedHold);
            float delta = rate * Time.deltaTime;

            float taken = player.TakeMass(delta);
            _shotMass += taken;

            UpdatePreviewVisual();

            if (player.Mass <= playerConfig.CriticalMass)
            {
                CancelChargeAsOvercharged();
                return;
            }

            ChargeChanged?.Invoke(MassUtils.RadiusFromMass(_shotMass));
        }

        private void HandlePressStarted()
        {
            if (gameController.State != GameState.Idle)
            {
                return;
            }

            _isCharging = true;
            _holdTime = 0f;
            _shotMass = 0f;
            _rentedProjectile = shotLauncher.RentPreview();

            if (playerView != null)
            {
                playerView.PlaySquash();
            }

            UpdatePreviewVisual();
        }

        private void HandlePressReleased()
        {
            if (!_isCharging)
            {
                return;
            }

            _isCharging = false;

            if (playerView != null)
            {
                playerView.StopSquash();
            }

            float shotRadius = MassUtils.RadiusFromMass(_shotMass);
            if (shotRadius < shotConfig.MinShotRadius)
            {
                // Слишком короткий тап — это не выстрел, возвращаем снаряд в пул и массу игроку.
                shotLauncher.ReturnPreview(_rentedProjectile);
                _rentedProjectile = null;
                player.AddMass(_shotMass);
                _shotMass = 0f;
                return;
            }

            if (playerView != null)
            {
                playerView.PlayShootRecoil();
            }

            float releasedMass = _shotMass;
            ShotProjectile projectile = _rentedProjectile;
            _rentedProjectile = null;
            _shotMass = 0f;
            ShotReleased?.Invoke(releasedMass, projectile);
        }

        private void CancelChargeAsOvercharged()
        {
            _isCharging = false;

            if (playerView != null)
            {
                playerView.StopSquash();
            }

            if (_rentedProjectile != null)
            {
                shotLauncher.ReturnPreview(_rentedProjectile);
                _rentedProjectile = null;
            }

            Overcharged?.Invoke();
        }

        private void UpdatePreviewVisual()
        {
            if (_rentedProjectile == null)
            {
                return;
            }

            float shotRadius = MassUtils.RadiusFromMass(_shotMass);
            _rentedProjectile.Configure(shotRadius);

            Vector3 direction = ComputeDirectionToTarget();
            Vector3 anchorLogical = player.LogicalPosition + direction * (player.Radius + shotConfig.SpawnGap + shotRadius);
            Vector3 anchorWorld = new Vector3(anchorLogical.x, player.transform.position.y, anchorLogical.z);
            _rentedProjectile.SetPreviewPosition(anchorWorld);
        }

        private Vector3 ComputeDirectionToTarget()
        {
            if (target == null)
            {
                return transform.forward;
            }

            Vector3 playerLogical = player.LogicalPosition;
            Vector3 targetLogical = new Vector3(target.position.x, 0f, target.position.z);
            Vector3 direction = targetLogical - playerLogical;
            direction.y = 0f;

            return direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized;
        }
    }
}
