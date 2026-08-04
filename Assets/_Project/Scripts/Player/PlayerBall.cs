using System;
using Game.Config;
using Game.Utils;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Модель шара-игрока: масса — единственный источник истины, радиус и визуальный
    /// масштаб всегда выводятся из неё через <see cref="MassUtils"/>. Игрок всегда стоит
    /// на земле (y = 0): центр шара держится на высоте текущего радиуса.
    /// </summary>
    public sealed class PlayerBall : MonoBehaviour
    {
        [SerializeField] private PlayerBallView view;

        private float _mass;

        /// <summary>Текущая масса игрока.</summary>
        public event Action<float> MassChanged;

        /// <summary>Текущая масса игрока.</summary>
        public float Mass => _mass;

        /// <summary>Текущий радиус игрока, вычисленный из массы.</summary>
        public float Radius => MassUtils.RadiusFromMass(_mass);

        /// <summary>Логическая позиция (проекция на плоскость y = 0) для чистой геометрии/симуляции.</summary>
        public Vector3 LogicalPosition => new Vector3(transform.position.x, 0f, transform.position.z);

        /// <summary>Инициализирует игрока стартовой массой из конфига.</summary>
        public void Initialize(PlayerConfig config)
        {
            SetMass(config.StartMass);
        }

        /// <summary>Напрямую задаёт массу (клампится в диапазон [0, +inf)), обновляет визуал и позицию по Y.</summary>
        public void SetMass(float mass)
        {
            _mass = Mathf.Max(0f, mass);
            ApplyRadiusToTransformAndView();
            MassChanged?.Invoke(_mass);
        }

        /// <summary>
        /// Пытается изъять запрошенную массу у игрока (например, для перетекания в выстрел).
        /// Не позволяет массе уйти в минус — возвращает фактически изъятое количество,
        /// которое может быть меньше запрошенного.
        /// </summary>
        public float TakeMass(float requested)
        {
            if (requested <= 0f)
            {
                return 0f;
            }

            float taken = Mathf.Min(requested, _mass);
            SetMass(_mass - taken);
            return taken;
        }

        /// <summary>Возвращает игроку массу (например, при отмене слишком короткого тапа).</summary>
        public void AddMass(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            SetMass(_mass + amount);
        }

        private void ApplyRadiusToTransformAndView()
        {
            float radius = Radius;

            Vector3 position = transform.position;
            position.y = radius;
            transform.position = position;

            if (view != null)
            {
                view.SetRadius(radius);
            }
        }
    }
}
