using DG.Tweening;
using Game.Config;
using Game.Core;
using Game.Player;
using Game.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Игровой HUD: полоса оставшегося размера шара, индикатор заряжаемого выстрела и
    /// подсказка по управлению. Только подписки на события — HUD ничего не опрашивает
    /// в Update и ничего не решает: состояние приходит из <see cref="GameController"/>,
    /// <see cref="PlayerBall"/> и <see cref="ChargeController"/>.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [Header("Источники данных")]
        [SerializeField] private PlayerBall player;
        [SerializeField] private PlayerConfig playerConfig;
        [SerializeField] private ChargeController chargeController;
        [SerializeField] private GameController gameController;

        [Header("Размер шара")]
        [SerializeField] private Slider massBar;
        [Tooltip("Заливка полосы — нужна отдельно от слайдера только чтобы красить её в цвет опасности.")]
        [SerializeField] private Image massFill;
        [SerializeField] private Text massLabel;
        [SerializeField] private Color safeColor = new Color(0.30f, 0.80f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.35f, 0.30f);

        [Tooltip("Доля оставшегося запаса, ниже которой полоса считается критической.")]
        [SerializeField] private float dangerThreshold = 0.3f;

        [Header("Заряд выстрела")]
        [SerializeField] private CanvasGroup chargeGroup;
        [SerializeField] private Slider chargeBar;

        [Header("Подсказка")]
        [SerializeField] private CanvasGroup hintGroup;

        private bool _chargeVisible;
        private Tween _massTween;
        private Tween _chargeTween;

        /// <summary>
        /// Опорный радиус выстрела для шкалы заряда: весь запас массы сверх критического.
        /// Константа, а не «сколько осталось сейчас» — иначе шкала растягивалась бы по мере
        /// похудения шара и одинаковый выстрел выглядел бы каждый раз по-разному.
        /// </summary>
        private float MaxUsefulShotRadius =>
            MassUtils.RadiusFromMass(Mathf.Max(0.0001f, playerConfig.StartMass - playerConfig.CriticalMass));

        private void OnEnable()
        {
            player.MassChanged += HandleMassChanged;
            chargeController.ChargeChanged += HandleChargeChanged;
            chargeController.ShotReleased += HandleShotReleased;
            chargeController.Overcharged += HideCharge;
            gameController.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            player.MassChanged -= HandleMassChanged;
            chargeController.ChargeChanged -= HandleChargeChanged;
            chargeController.ShotReleased -= HandleShotReleased;
            chargeController.Overcharged -= HideCharge;
            gameController.StateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            HandleMassChanged(player.Mass);
            SetChargeVisible(false, instant: true);
            HandleStateChanged(gameController.State);
        }

        private void HandleMassChanged(float mass)
        {
            float radius = MassUtils.RadiusFromMass(mass);
            float span = Mathf.Max(0.0001f, playerConfig.StartRadius - playerConfig.CriticalRadius);
            float fraction = Mathf.Clamp01((radius - playerConfig.CriticalRadius) / span);

            if (massBar != null)
            {
                _massTween?.Kill();
                _massTween = UiTween.Fill(massBar, fraction, 0.15f);
            }

            if (massFill != null)
            {
                massFill.color = Color.Lerp(dangerColor, safeColor,
                    Mathf.InverseLerp(0f, Mathf.Max(0.01f, dangerThreshold), fraction));
            }

            if (massLabel != null)
            {
                massLabel.text = $"Размер  {radius:F2} м";
            }
        }

        private void HandleChargeChanged(float shotRadius)
        {
            if (!_chargeVisible)
            {
                SetChargeVisible(true, instant: false);
            }

            if (chargeBar != null)
            {
                chargeBar.value = Mathf.Clamp01(shotRadius / MaxUsefulShotRadius);
            }
        }

        private void HandleShotReleased(float shotMass, Shooting.ShotProjectile projectile)
        {
            HideCharge();
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.Idle)
            {
                HideCharge();
            }

            if (hintGroup != null)
            {
                UiTween.Fade(hintGroup, state == GameState.Idle ? 1f : 0f, 0.2f);
            }
        }

        private void HideCharge()
        {
            SetChargeVisible(false, instant: false);
        }

        private void SetChargeVisible(bool visible, bool instant)
        {
            _chargeVisible = visible;

            if (chargeGroup == null)
            {
                return;
            }

            _chargeTween?.Kill();

            if (instant)
            {
                chargeGroup.alpha = visible ? 1f : 0f;
                return;
            }

            _chargeTween = UiTween.Fade(chargeGroup, visible ? 1f : 0f, visible ? 0.1f : 0.25f);
        }
    }
}
