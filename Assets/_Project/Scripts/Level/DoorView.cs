using DG.Tweening;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Анимация створок двери на DOTween. Закрытые позиции запоминаются в Awake, поэтому
    /// повторный вызов <see cref="Open"/> не «уползает» от предыдущего открытия.
    /// </summary>
    public sealed class DoorView : MonoBehaviour
    {
        [SerializeField] private Transform leftPanel;
        [SerializeField] private Transform rightPanel;
        [SerializeField] private Vector3 leftOpenLocalOffset = new Vector3(-1.2f, 0f, 0f);
        [SerializeField] private Vector3 rightOpenLocalOffset = new Vector3(1.2f, 0f, 0f);

        private Vector3 _leftClosed;
        private Vector3 _rightClosed;
        private Sequence _sequence;

        private void Awake()
        {
            if (leftPanel != null)
            {
                _leftClosed = leftPanel.localPosition;
            }

            if (rightPanel != null)
            {
                _rightClosed = rightPanel.localPosition;
            }
        }

        /// <summary>Раздвигает створки за <paramref name="duration"/> секунд.</summary>
        public void Open(float duration)
        {
            _sequence?.Kill();

            float safeDuration = Mathf.Max(0.01f, duration);
            _sequence = DOTween.Sequence().SetLink(gameObject);

            if (leftPanel != null)
            {
                _sequence.Join(leftPanel.DOLocalMove(_leftClosed + leftOpenLocalOffset, safeDuration)
                    .SetEase(Ease.OutCubic));
            }

            if (rightPanel != null)
            {
                _sequence.Join(rightPanel.DOLocalMove(_rightClosed + rightOpenLocalOffset, safeDuration)
                    .SetEase(Ease.OutCubic));
            }
        }
    }
}
