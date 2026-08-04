using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Настройки приложения, которые нельзя задать в PlayerSettings: частота кадров и
    /// запрет гашения экрана. На мобильных Unity по умолчанию ограничивает частоту 30 кадрами,
    /// а вся игра — про точность удержания тапа, поэтому 30 кадров ощущаются вязко.
    /// </summary>
    public sealed class ApplicationSettings : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
