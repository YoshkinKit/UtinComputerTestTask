using System;
using System.Collections;
using Game.Config;
using Game.Level;
using Game.Obstacles;
using Game.Player;
using Game.Shooting;
using Game.Utils;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Единая точка состояний игры и всех условий победы/поражения. Слушает события
    /// заряда, выстрела, продвижения и решает, что делать дальше — переключение состояний
    /// централизовано здесь, остальные компоненты только сообщают о фактах через события.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [Header("Конфиги")]
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private InfectionConfig infectionConfig;

        [Header("Компоненты")]
        [SerializeField] private PlayerBall player;
        [SerializeField] private ChargeController chargeController;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ShotLauncher shotLauncher;
        [SerializeField] private ObstacleField obstacleField;
        [SerializeField] private LevelPath levelPath;
        [SerializeField] private DoorController door;

        private GameState _state = GameState.Idle;
        private LoseReason _loseReason = LoseReason.None;

        /// <summary>Состояние изменилось.</summary>
        public event Action<GameState> StateChanged;

        /// <summary>Игрок дошёл до двери.</summary>
        public event Action Won;

        /// <summary>Игра проиграна, аргумент — причина.</summary>
        public event Action<LoseReason> Lost;

        /// <summary>Текущее состояние игры.</summary>
        public GameState State => _state;

        /// <summary>Причина поражения (валидна только при State == Lost).</summary>
        public LoseReason LoseReason => _loseReason;

        private void OnEnable()
        {
            chargeController.ShotReleased += HandleShotReleased;
            chargeController.Overcharged += HandleOvercharged;
            shotLauncher.ShotResolved += HandleShotResolved;
            mover.ReachedEnd += HandleReachedEnd;
            mover.Blocked += HandleBlocked;
        }

        private void OnDisable()
        {
            chargeController.ShotReleased -= HandleShotReleased;
            chargeController.Overcharged -= HandleOvercharged;
            shotLauncher.ShotResolved -= HandleShotResolved;
            mover.ReachedEnd -= HandleReachedEnd;
            mover.Blocked -= HandleBlocked;
        }

        private void Start()
        {
            player.Initialize(playerConfig);
            SetState(GameState.Advancing);
            mover.TryAdvance();
        }

        private void HandleReachedEnd()
        {
            if (IsGameOver())
            {
                return;
            }

            SetState(GameState.Won);
            Won?.Invoke();
        }

        private void HandleBlocked(int blockedIndex)
        {
            if (IsGameOver())
            {
                return;
            }

            SetState(GameState.Idle);
        }

        private void HandleShotReleased(float shotMass, ShotProjectile projectile)
        {
            if (_state != GameState.Idle)
            {
                return;
            }

            SetState(GameState.ShotFlying);
            shotLauncher.LaunchRented(projectile, shotMass);
        }

        private void HandleOvercharged()
        {
            if (IsGameOver())
            {
                return;
            }

            FailWith(LoseReason.Overcharged);
        }

        private void HandleShotResolved(bool hasHit, float waveDuration)
        {
            if (IsGameOver())
            {
                return;
            }

            SetState(GameState.Resolving);
            StartCoroutine(ResolveAfterWave(waveDuration));
        }

        private IEnumerator ResolveAfterWave(float waveDuration)
        {
            if (waveDuration > 0f)
            {
                yield return new WaitForSeconds(waveDuration);
            }

            if (IsGameOver())
            {
                yield break;
            }

            SetState(GameState.Advancing);

            if (mover.CurrentIndex < levelPath.PointCount - 1)
            {
                Vector3 current = levelPath.GetPoint(mover.CurrentIndex);
                Vector3 next = levelPath.GetPoint(mover.CurrentIndex + 1);
                float clearance = player.Radius * playerConfig.ClearanceFactor;

                bool stillBlocked = !PathGeometry.IsSegmentClear(obstacleField.Graph, current, next, clearance);
                if (stillBlocked && !CanClearRemainingPath(current, next, clearance))
                {
                    FailWith(LoseReason.NotEnoughMass);
                    yield break;
                }
            }

            // Либо сегмент уже чист, либо расчистить его в принципе ещё возможно — отдаём
            // ход PlayerMover: если сегмент всё ещё занят, он сам поднимет Blocked и мы
            // вернёмся в Idle ждать следующего выстрела; так проверка "хватит ли массы"
            // не блокирует нормальный игровой цикл "выстрел -> частичная расчистка -> ещё выстрел".
            mover.TryAdvance();
        }

        /// <summary>
        /// Проверка проигрыша «не хватило массы». Прогоняет через <see cref="LevelSolver"/>
        /// гипотетический выстрел максимально доступного радиуса (вся оставшаяся масса сверх
        /// критической) в текущий заблокированный сегмент коридора.
        /// <para/>
        /// Одного такого выстрела достаточно как критерия, а не нужно перебирать всю
        /// последовательность возможных выстрелов, потому что: направление выстрела всегда
        /// фиксировано на дверь (не свободное прицеливание), точка попадания при неизменном
        /// блокирующем кластере — та же самая для любого радиуса выстрела в эту сторону, а
        /// заражение от фиксированной точки попадания монотонно по радиусу выстрела (доказано
        /// тестами <c>InfectionSimulatorTests</c> этапа A: результат для большего радиуса —
        /// надмножество результата для меньшего). Значит один выстрел максимально доступного
        /// радиуса заражает надмножество того, что заразила бы любая последовательность более
        /// мелких выстрелов в ту же точку, и доминирует любую такую последовательность: если
        /// даже он не расчищает сегмент (или требует больше массы, чем осталось) — расчистить
        /// сегмент не удастся вообще никаким распределением оставшейся массы по выстрелам.
        /// </summary>
        private bool CanClearRemainingPath(Vector3 segmentA, Vector3 segmentB, float clearance)
        {
            float availableMass = player.Mass - playerConfig.CriticalMass;
            if (availableMass <= 0f)
            {
                return false;
            }

            float maxShotRadius = MassUtils.RadiusFromMass(availableMass);
            Vector3 playerLogical = player.LogicalPosition;
            Vector3 direction = ComputeDirectionToDoor(playerLogical);

            bool found = LevelSolver.TryFindMinimalShotRadius(obstacleField.Graph, playerLogical, direction,
                segmentA, segmentB, clearance, infectionConfig.ToSettings(), maxShotRadius,
                tolerance: 0.01f, out float minRadius);

            if (!found)
            {
                return false;
            }

            float requiredMass = MassUtils.MassFromRadius(minRadius);
            return requiredMass <= availableMass;
        }

        private Vector3 ComputeDirectionToDoor(Vector3 playerLogical)
        {
            Vector3 doorLogical = new Vector3(door.transform.position.x, 0f, door.transform.position.z);
            Vector3 direction = doorLogical - playerLogical;
            direction.y = 0f;

            return direction.sqrMagnitude < 1e-6f ? Vector3.forward : direction.normalized;
        }

        private void FailWith(LoseReason reason)
        {
            _loseReason = reason;
            SetState(GameState.Lost);
            Lost?.Invoke(reason);
        }

        private bool IsGameOver()
        {
            return _state == GameState.Won || _state == GameState.Lost;
        }

        private void SetState(GameState newState)
        {
            if (_state == newState)
            {
                return;
            }

            _state = newState;
            StateChanged?.Invoke(_state);
        }
    }
}
