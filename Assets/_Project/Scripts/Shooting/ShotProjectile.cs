using System;
using UnityEngine;

namespace Game.Shooting
{
    /// <summary>
    /// Чисто визуальный снаряд выстрела. Точка попадания вычисляется аналитически
    /// заранее (см. <see cref="Game.Level.PathGeometry.RaycastFirstNode"/>, вызывается
    /// из <see cref="ShotLauncher"/> в момент запуска) — сам снаряд лишь линейно летит
    /// от точки старта к уже известной точке и сообщает о прибытии колбэком. Никакой
    /// физики/коллайдеров: движение полностью детерминировано и совпадает с тем, что
    /// посчитала аналитика.
    /// Также поддерживает режим превью во время заряда: стоит неподвижно у края игрока
    /// и лишь меняет видимый радиус по мере роста массы выстрела.
    /// </summary>
    public sealed class ShotProjectile : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;

        private float _radius;
        private bool _isFlying;
        private float _speed;
        private Vector3 _from;
        private Vector3 _to;
        private float _travelDistance;
        private float _traveled;
        private Action _onArrived;

        /// <summary>Текущий радиус снаряда.</summary>
        public float Radius => _radius;

        /// <summary>Летит ли снаряд сейчас (false в режиме превью и после прибытия).</summary>
        public bool IsFlying => _isFlying;

        /// <summary>Задаёт радиус снаряда и обновляет визуальный масштаб.</summary>
        public void Configure(float radius)
        {
            _radius = Mathf.Max(0f, radius);
            ApplyScale();
        }

        /// <summary>Режим превью: снаряд неподвижно стоит в мировой точке (обычно у края игрока).</summary>
        public void SetPreviewPosition(Vector3 worldPosition)
        {
            _isFlying = false;
            transform.position = worldPosition;
        }

        /// <summary>
        /// Запускает прямолинейный полёт из <paramref name="from"/> в <paramref name="to"/>
        /// с постоянной скоростью <paramref name="speed"/>. По достижении цели вызывает
        /// <paramref name="onArrived"/> ровно один раз.
        /// </summary>
        public void Launch(Vector3 from, Vector3 to, float speed, Action onArrived)
        {
            _from = from;
            _to = to;
            _speed = Mathf.Max(0.01f, speed);
            _onArrived = onArrived;
            _traveled = 0f;
            _travelDistance = Vector3.Distance(from, to);

            transform.position = from;
            _isFlying = true;
        }

        private void Update()
        {
            if (!_isFlying)
            {
                return;
            }

            _traveled += _speed * Time.deltaTime;
            float t = _travelDistance > 0f ? Mathf.Clamp01(_traveled / _travelDistance) : 1f;
            transform.position = Vector3.Lerp(_from, _to, t);

            if (t >= 1f)
            {
                _isFlying = false;
                Action callback = _onArrived;
                _onArrived = null;
                callback?.Invoke();
            }
        }

        private void ApplyScale()
        {
            Transform target = visualRoot != null ? visualRoot : transform;
            target.localScale = Vector3.one * (_radius * 2f);
        }
    }
}
