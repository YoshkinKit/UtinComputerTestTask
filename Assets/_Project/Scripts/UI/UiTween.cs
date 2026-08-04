using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Твины UI на голом ядре DOTween.
    /// <para/>
    /// Шорткаты вида <c>canvasGroup.DOFade()</c> и <c>image.DOFillAmount()</c> живут не в
    /// <c>DOTween.dll</c>, а в исходниках <c>Assets/Plugins/Demigiant/DOTween/Modules</c>.
    /// Эти файлы попадают в предопределённую сборку Assembly-CSharp, а весь код проекта лежит
    /// в asmdef-сборках, которые Assembly-CSharp не видят по определению — значит шорткаты
    /// оттуда недоступны. Вариант «сгенерировать DOTween свой asmdef» добавляет в проект
    /// генерируемый файл, который придётся поддерживать; здесь достаточно нескольких вызовов
    /// <see cref="DOTween.To(DOGetter{float}, DOSetter{float}, float, float)"/>, поэтому
    /// зависимость остаётся одной чистой DLL.
    /// </summary>
    public static class UiTween
    {
        /// <summary>Плавно меняет прозрачность группы.</summary>
        public static Tween Fade(CanvasGroup group, float target, float duration)
        {
            return DOTween.To(() => group.alpha, value => group.alpha = value, target, duration)
                .SetLink(group.gameObject);
        }

        /// <summary>Плавно меняет заполнение полосы-индикатора.</summary>
        public static Tween Fill(Slider slider, float target, float duration)
        {
            return DOTween.To(() => slider.value, value => slider.value = value, target, duration)
                .SetLink(slider.gameObject);
        }
    }
}
