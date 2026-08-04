using System;
using System.Collections;
using DG.Tweening;
using Game.Config;
using Game.Level;
using Game.Obstacles;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Кинематическое авто-продвижение игрока по <see cref="LevelPath"/>: пока следующий
    /// сегмент маршрута свободен (аналитически, через <see cref="PathGeometry.IsSegmentClear"/>,
    /// без физики), шар сам прыгает вперёд по дуге; на первом занятом сегменте — стоп и
    /// событие <see cref="Blocked"/>. Дуга прыжка — DOTween-твин, корутина лишь дожидается
    /// его завершения и решает, прыгать ли дальше.
    /// </summary>
    public sealed class PlayerMover : MonoBehaviour
    {
        [SerializeField] private PlayerBall player;
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private LevelPath levelPath;
        [SerializeField] private ObstacleField obstacleField;

        private int _currentIndex;
        private bool _isMoving;

        /// <summary>Игрок достиг последней точки маршрута.</summary>
        public event Action ReachedEnd;

        /// <summary>Путь заблокирован: аргумент — индекс точки, с которой начинается непроходимый сегмент.</summary>
        public event Action<int> Blocked;

        /// <summary>Один прыжок завершён (для HUD/камеры).</summary>
        public event Action HopCompleted;

        /// <summary>Индекс точки маршрута, где сейчас стоит игрок.</summary>
        public int CurrentIndex => _currentIndex;

        /// <summary>Идёт ли сейчас движение (прыжки).</summary>
        public bool IsMoving => _isMoving;

        /// <summary>Достиг ли игрок последней точки маршрута.</summary>
        public bool AtEnd => _currentIndex >= levelPath.PointCount - 1;

        /// <summary>
        /// Пытается начать авто-продвижение: прыгает вперёд по всем подряд идущим свободным
        /// сегментам, пока не упрётся в завал или не дойдёт до конца маршрута. Возвращает false,
        /// если движение уже идёт (вызов игнорируется) — сам факт продвижения асинхронный,
        /// о результате сообщают события <see cref="Blocked"/>/<see cref="ReachedEnd"/>.
        /// </summary>
        public bool TryAdvance()
        {
            if (_isMoving)
            {
                return false;
            }

            StartCoroutine(AdvanceRoutine());
            return true;
        }

        private IEnumerator AdvanceRoutine()
        {
            _isMoving = true;

            while (_currentIndex < levelPath.PointCount - 1)
            {
                Vector3 current = levelPath.GetPoint(_currentIndex);
                Vector3 next = levelPath.GetPoint(_currentIndex + 1);
                float clearance = player.Radius * playerConfig.ClearanceFactor;

                if (!PathGeometry.IsSegmentClear(obstacleField.Graph, current, next, clearance))
                {
                    _isMoving = false;
                    Blocked?.Invoke(_currentIndex);
                    yield break;
                }

                yield return HopTo(next);
                _currentIndex++;
                HopCompleted?.Invoke();

                if (playerConfig.HopPauseDuration > 0f)
                {
                    yield return new WaitForSeconds(playerConfig.HopPauseDuration);
                }
            }

            _isMoving = false;
            ReachedEnd?.Invoke();
        }

        private IEnumerator HopTo(Vector3 targetLogical)
        {
            float landingHeight = player.Radius;
            Vector3 targetPosition = new Vector3(targetLogical.x, landingHeight, targetLogical.z);
            float duration = Mathf.Max(0.01f, playerConfig.HopDuration);

            // DOJump сам держит параболу по Y; горизонталь оставляем линейной, иначе прыжок
            // читается как рывок с торможением в воздухе.
            Tween hop = player.transform
                .DOJump(targetPosition, playerConfig.HopHeight, numJumps: 1, duration)
                .SetEase(Ease.Linear)
                .SetLink(player.gameObject);

            yield return hop.WaitForCompletion();

            player.transform.position = targetPosition;
        }
    }
}
