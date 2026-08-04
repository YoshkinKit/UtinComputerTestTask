using DG.Tweening;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Экран итога: победа или поражение с конкретной причиной. Причину не выдумывает —
    /// берёт готовый <see cref="LoseReason"/> из <see cref="GameController"/>, чтобы текст
    /// на экране и логика проигрыша не могли разойтись.
    /// </summary>
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] private GameController gameController;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text title;
        [SerializeField] private Text subtitle;
        [SerializeField] private Button restartButton;
        [SerializeField] private RectTransform panel;

        [Header("Цвета заголовка")]
        [SerializeField] private Color winColor = new Color(0.35f, 0.90f, 0.50f);
        [SerializeField] private Color loseColor = new Color(1f, 0.45f, 0.40f);

        private void OnEnable()
        {
            gameController.Won += HandleWon;
            gameController.Lost += HandleLost;

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }
        }

        private void OnDisable()
        {
            gameController.Won -= HandleWon;
            gameController.Lost -= HandleLost;

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(Restart);
            }
        }

        private void Start()
        {
            Hide();
        }

        /// <summary>Перезапускает уровень с нуля.</summary>
        public void Restart()
        {
            // Твины живут вне сцены, а их цели — внутри: без явной остановки перезагрузка
            // оставила бы висеть анимации на уничтоженных объектах.
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void HandleWon()
        {
            Show("Победа", "Шар добрался до двери", winColor);
        }

        private void HandleLost(LoseReason reason)
        {
            string message = reason switch
            {
                LoseReason.Overcharged => "Тап удержан слишком долго — шар перекачан в выстрел",
                LoseReason.NotEnoughMass => "Оставшейся массы не хватит, чтобы расчистить путь",
                _ => "Уровень не пройден"
            };

            Show("Поражение", message, loseColor);
        }

        private void Show(string titleText, string subtitleText, Color titleColor)
        {
            if (title != null)
            {
                title.text = titleText;
                title.color = titleColor;
            }

            if (subtitle != null)
            {
                subtitle.text = subtitleText;
            }

            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
                UiTween.Fade(group, 1f, 0.3f);
            }

            if (panel != null)
            {
                panel.localScale = Vector3.one * 0.85f;
                panel.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack).SetLink(gameObject);
            }
        }

        private void Hide()
        {
            if (group == null)
            {
                return;
            }

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }
}
