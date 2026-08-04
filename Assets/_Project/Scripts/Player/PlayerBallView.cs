using DG.Tweening;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Визуальное представление шара-игрока. Радиус применяется мгновенно — от него зависит
    /// геймплейная логика, и «догоняющий» визуал врал бы игроку о размере шара. Всё остальное
    /// (сквош во время заряда, отдача при выстреле) живёт в отдельном множителе масштаба,
    /// который анимируется независимо: два твина никогда не дерутся за localScale.
    /// </summary>
    public sealed class PlayerBallView : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;

        [Header("Заряд")]
        [SerializeField] private Vector3 chargeSquash = new Vector3(1.08f, 0.9f, 1.08f);
        [SerializeField] private float chargeSquashDuration = 0.45f;

        [Header("Выстрел")]
        [SerializeField] private Vector3 shotRecoilPunch = new Vector3(-0.18f, 0.22f, -0.18f);
        [SerializeField] private float shotRecoilDuration = 0.3f;

        private Vector3 _squash = Vector3.one;
        private float _radius = 0.5f;
        private Tween _squashTween;

        private Transform Target => visualRoot != null ? visualRoot : transform;

        /// <summary>Устанавливает видимый радиус шара (масштаб единичной сферы = диаметр).</summary>
        public void SetRadius(float radius)
        {
            _radius = radius;
            ApplyScale();
        }

        /// <summary>Запускает зацикленный «дышащий» сквош на время заряда выстрела.</summary>
        public void PlaySquash()
        {
            _squashTween?.Kill();
            _squash = Vector3.one;

            _squashTween = DOTween.To(() => _squash, value => { _squash = value; ApplyScale(); },
                    chargeSquash, chargeSquashDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        /// <summary>Гасит сквош заряда и плавно возвращает шар к обычной форме.</summary>
        public void StopSquash()
        {
            _squashTween?.Kill();
            _squashTween = DOTween.To(() => _squash, value => { _squash = value; ApplyScale(); },
                    Vector3.one, 0.15f)
                .SetEase(Ease.OutSine)
                .SetLink(gameObject);
        }

        /// <summary>Отдача в момент выстрела: шар «выплёвывает» снаряд и упруго возвращается.</summary>
        public void PlayShootRecoil()
        {
            _squashTween?.Kill();
            _squash = Vector3.one;

            _squashTween = DOTween.Punch(() => _squash, value => { _squash = value; ApplyScale(); },
                    shotRecoilPunch, shotRecoilDuration, vibrato: 6, elasticity: 0.6f)
                .SetLink(gameObject);
        }

        private void ApplyScale()
        {
            float diameter = _radius * 2f;
            Target.localScale = new Vector3(diameter * _squash.x, diameter * _squash.y, diameter * _squash.z);
        }
    }
}
