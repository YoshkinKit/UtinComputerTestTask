using System;
using DG.Tweening;
using UnityEngine;

namespace Game.Obstacles
{
    /// <summary>
    /// Визуализация препятствия на DOTween: смена цвета через MaterialPropertyBlock (без
    /// создания instance-материалов) и анимация масштаба — толчок при заражении, схлопывание
    /// при взрыве. Задержка взрыва приходит извне и пропорциональна глубине волны заражения,
    /// поэтому цепная реакция читается как расходящаяся волна, а не как одновременный хлопок.
    /// </summary>
    public sealed class ObstacleView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color infectedColor = new Color(1f, 0.25f, 0.15f);
        [SerializeField] private float infectedColorDuration = 0.12f;
        [SerializeField] private float infectedPunchScale = 0.25f;
        [SerializeField] private float infectedPulseDuration = 0.18f;

        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _baseScale = Vector3.one;
        private Color _color = Color.white;
        private Tween _scaleTween;
        private Tween _colorTween;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _baseScale = transform.localScale;

            if (targetRenderer != null && targetRenderer.sharedMaterial != null &&
                targetRenderer.sharedMaterial.HasProperty(BaseColorId))
            {
                _color = targetRenderer.sharedMaterial.GetColor(BaseColorId);
            }
        }

        /// <summary>
        /// Задаёт «исходный» (не заражённый) масштаб по радиусу препятствия. Вызывается
        /// генератором уровня при спавне — далее толчок/схлопывание анимируются относительно
        /// этого масштаба.
        /// </summary>
        public void SetRadius(float radius)
        {
            _baseScale = Vector3.one * (radius * 2f);
            transform.localScale = _baseScale;
        }

        /// <summary>Меняет цвет на «заражённый» и даёт короткий толчок масштаба.</summary>
        public void PlayInfected()
        {
            _colorTween?.Kill();
            _colorTween = DOTween.To(() => _color, value => ApplyColor(value), infectedColor, infectedColorDuration)
                .SetLink(gameObject);

            _scaleTween?.Kill();
            transform.localScale = _baseScale;
            _scaleTween = transform.DOPunchScale(_baseScale * infectedPunchScale, infectedPulseDuration,
                    vibrato: 6, elasticity: 0.5f)
                .SetLink(gameObject);
        }

        /// <summary>
        /// Через <paramref name="delay"/> секунд схлопывает препятствие за
        /// <paramref name="duration"/> секунд и вызывает <paramref name="onComplete"/>.
        /// </summary>
        public void PlayExplosion(float delay, float duration, Action onComplete)
        {
            // Толчок заражения обязан уступить взрыву: иначе он вернул бы масштаб к базовому
            // уже после того, как объект начал схлопываться.
            _scaleTween?.Kill();

            _scaleTween = transform.DOScale(Vector3.zero, Mathf.Max(0.01f, duration))
                .SetDelay(Mathf.Max(0f, delay))
                .SetEase(Ease.InBack)
                .OnComplete(() => onComplete?.Invoke())
                .SetLink(gameObject);
        }

        private void ApplyColor(Color color)
        {
            _color = color;

            if (targetRenderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
