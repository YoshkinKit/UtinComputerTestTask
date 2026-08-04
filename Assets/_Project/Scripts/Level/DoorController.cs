using System;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Открывает дверь, когда игрок приближается на <see cref="openDistance"/> метров
    /// (5 м по ТЗ). Дистанция измеряется в логической плоскости y = 0. Победа определяется
    /// не здесь, а по <see cref="Player.PlayerMover.ReachedEnd"/> — последняя точка маршрута
    /// ставится за дверью, отдельный триггер цели не нужен.
    /// </summary>
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private DoorView view;
        [Tooltip("Дистанция до игрока, на которой дверь открывается, метры (5 м по ТЗ).")]
        [SerializeField] private float openDistance = 5f;
        [SerializeField] private float openDuration = 0.6f;

        private bool _isOpen;

        /// <summary>Дверь только что открылась.</summary>
        public event Action Opened;

        /// <summary>Открыта ли дверь.</summary>
        public bool IsOpen => _isOpen;

        /// <summary>Дистанция открытия двери, метры.</summary>
        public float OpenDistance => openDistance;

        private void Update()
        {
            if (_isOpen || player == null)
            {
                return;
            }

            Vector3 doorLogical = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 playerLogical = new Vector3(player.position.x, 0f, player.position.z);

            if (Vector3.Distance(doorLogical, playerLogical) <= openDistance)
            {
                _isOpen = true;

                if (view != null)
                {
                    view.Open(openDuration);
                }

                Opened?.Invoke();
            }
        }
    }
}
