using Game.Cameras;
using Game.Shooting;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Собирает «ощущение удара» в одном месте: превращает игровые события в тряску камеры.
    /// Отдельным компонентом — чтобы ни <see cref="ShotLauncher"/>, ни <see cref="GameController"/>
    /// ничего не знали про камеру и обратную связь, и их можно было тестировать без сцены.
    /// </summary>
    public sealed class GameFeedback : MonoBehaviour
    {
        [SerializeField] private ShotLauncher shotLauncher;
        [SerializeField] private CameraFollow cameraFollow;

        [Header("Тряска на попадание")]
        [SerializeField] private float minShakeStrength = 0.12f;
        [SerializeField] private float maxShakeStrength = 0.5f;

        [Tooltip("Длительность волны взрывов, при которой тряска достигает максимума, секунды.")]
        [SerializeField] private float shakeReferenceWave = 0.6f;

        private void OnEnable()
        {
            shotLauncher.ShotResolved += HandleShotResolved;
        }

        private void OnDisable()
        {
            shotLauncher.ShotResolved -= HandleShotResolved;
        }

        private void HandleShotResolved(bool hasHit, float waveDuration)
        {
            if (!hasHit || cameraFollow == null)
            {
                return;
            }

            // Длительность волны — прямая мера размера цепной реакции: чем длиннее цепочка
            // взрывов, тем сильнее удар. Отдельный счётчик разрушенных препятствий не нужен.
            float scale = Mathf.Clamp01(waveDuration / Mathf.Max(0.01f, shakeReferenceWave));
            float strength = Mathf.Lerp(minShakeStrength, maxShakeStrength, scale);

            cameraFollow.Shake(strength, Mathf.Max(0.25f, waveDuration * 0.6f));
        }
    }
}
