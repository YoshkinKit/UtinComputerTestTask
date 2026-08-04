using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Inputs
{
    /// <summary>
    /// Тонкая обёртка над Input System для одного взаимодействия «тап/клик указателем»:
    /// press и release. Работает одинаково для мыши в редакторе и тача на устройстве
    /// благодаря биндингу на абстрактный `&lt;Pointer&gt;/press`.
    /// Нажатия, начавшиеся над UI-элементами (по <see cref="EventSystem"/>), полностью
    /// игнорируются — не поднимают ни PressStarted, ни парный PressReleased.
    /// <para/>
    /// Состояние действия опрашивается в Update, а не через колбэки performed/canceled,
    /// именно из-за проверки UI: <see cref="EventSystem.IsPointerOverGameObject"/> внутри
    /// колбэка Input System возвращает состояние ПРОШЛОГО кадра (Unity про это и предупреждает),
    /// поэтому тап по кнопке мог бы одновременно уйти и в UI, и в геймплей.
    /// </summary>
    public sealed class InputReader : MonoBehaviour
    {
        /// <summary>Указатель нажат (и нажатие началось не над UI).</summary>
        public event Action PressStarted;

        /// <summary>Указатель отпущен (парное событие к учтённому PressStarted).</summary>
        public event Action PressReleased;

        /// <summary>Порог, выше которого значение контрола считается нажатием.</summary>
        private const float PressPoint = 0.5f;

        private InputAction _pressAction;
        private bool _rawPressed;
        private bool _isPressed;
        private bool _activePressStartedOverUi;

        /// <summary>Удерживается ли сейчас нажатие (с учётом фильтрации по UI).</summary>
        public bool IsPressed => _isPressed;

        private void Awake()
        {
            // Тип Value, а не Button, — принципиально. Button-действие срабатывает в момент
            // нажатия и тут же возвращается в Waiting, поэтому опрос ReadValue/IsPressed видит
            // нажатие максимум один кадр: удержание тапа (вся механика заряда) сломалось бы.
            // Value-действие остаётся актуированным всё время, пока контрол зажат.
            _pressAction = new InputAction(name: "Press", type: InputActionType.Value, binding: "<Pointer>/press");
        }

        private void OnEnable()
        {
            _pressAction.Enable();
        }

        private void OnDisable()
        {
            _pressAction.Disable();

            _rawPressed = false;
            _isPressed = false;
            _activePressStartedOverUi = false;
        }

        private void OnDestroy()
        {
            _pressAction.Dispose();
        }

        private void Update()
        {
            bool pressed = _pressAction.ReadValue<float>() > PressPoint;
            if (pressed == _rawPressed)
            {
                return;
            }

            _rawPressed = pressed;

            if (pressed)
            {
                HandlePressStarted();
            }
            else
            {
                HandlePressReleased();
            }
        }

        private void HandlePressStarted()
        {
            _activePressStartedOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (_activePressStartedOverUi)
            {
                return;
            }

            _isPressed = true;
            PressStarted?.Invoke();
        }

        private void HandlePressReleased()
        {
            bool suppressRelease = _activePressStartedOverUi;
            _activePressStartedOverUi = false;

            if (suppressRelease)
            {
                return;
            }

            _isPressed = false;
            PressReleased?.Invoke();
        }
    }
}
