using DG.Tweening;
using UnityEngine;

namespace Game.Cameras
{
    /// <summary>
    /// Камера «через плечо»: держит игрока в кадре и смотрит вдоль коридора в сторону двери.
    /// Смещение и точка взгляда задаются в мировых координатах (их выставляет сборщик сцены
    /// по оси уровня), поэтому камера не крутится вслед за изгибами маршрута — картинка
    /// остаётся стабильной, и по ней видно, куда полетит выстрел.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Tooltip("Смещение камеры относительно игрока в мировых координатах.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 9f, -11f);

        [Tooltip("Смещение точки взгляда относительно игрока — камера смотрит вперёд по коридору.")]
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0f, 5f);

        [Tooltip("Скорость подтягивания камеры к целевой позиции (0 — жёсткая привязка).")]
        [SerializeField] private float damping = 5f;

        private Vector3 _shakeOffset;
        private Tween _shakeTween;

        private void OnEnable()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;
            transform.position = damping <= 0f
                ? desired
                : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-damping * Time.deltaTime));

            // Поворот считается ДО добавления тряски: иначе камера доворачивалась бы на цель
            // при каждом дрожании и картинка вихляла бы вместо честного удара.
            transform.LookAt(target.position + lookOffset);
            transform.position += _shakeOffset;
        }

        /// <summary>Мгновенно ставит камеру в целевое положение — без «наезда» из точки старта сцены.</summary>
        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            transform.position = target.position + offset;
            transform.LookAt(target.position + lookOffset);
        }

        /// <summary>Тряска камеры на <paramref name="duration"/> секунд с амплитудой <paramref name="strength"/> метров.</summary>
        public void Shake(float strength, float duration)
        {
            _shakeTween?.Kill();
            _shakeOffset = Vector3.zero;

            if (strength <= 0f || duration <= 0f)
            {
                return;
            }

            _shakeTween = DOTween.Shake(() => _shakeOffset, value => _shakeOffset = value,
                    duration, strength, vibrato: 14, randomness: 90f, ignoreZAxis: false, fadeOut: true)
                .OnComplete(() => _shakeOffset = Vector3.zero)
                .SetLink(gameObject);
        }
    }
}
