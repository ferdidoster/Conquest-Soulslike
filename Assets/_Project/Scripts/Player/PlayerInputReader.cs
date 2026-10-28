using System;
using Conquest.CameraSystem;
using Conquest.Core.Input;
using Conquest.Core.Utilities;
using Conquest.Targeting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Conquest.Player
{
    /// <summary>
    /// Translates keyboard/mouse and gamepad input into character intent and camera look.
    /// Bindings are defined in code so the project works without an input asset; swap this
    /// component for another <see cref="ICharacterInput"/> to rebind or support other devices.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour, ICharacterInput, ILookInput
    {
        [SerializeField, Tooltip("Movement is relative to this. Defaults to the main camera.")]
        private Transform view;
        [SerializeField] private LockOnController lockOn;

        [Header("Look")]
        [SerializeField, Tooltip("Degrees per pixel of mouse movement.")] private float mouseSensitivity = 0.12f;
        [SerializeField, Tooltip("Degrees per second at full stick.")] private float stickSensitivity = 200f;
        [SerializeField] private bool invertY;
        [SerializeField, Tooltip("Keep walking the same way while the camera auto-turns, until the move input is released or changed.")]
        private bool holdMoveDirection = true;

        [Header("Dodge / Sprint")]
        [SerializeField, Tooltip("Holding dodge longer than this sprints instead of rolling.")]
        private float sprintHoldTime = 0.3f;
        [SerializeField, Tooltip("Below this stick magnitude a toggled sprint (L3) ends.")]
        private float sprintToggleStopThreshold = 0.2f;

        [Header("Target switching")]
        [SerializeField, Range(0f, 1f)] private float stickFlickThreshold = 0.8f;

        private InputAction _move, _walk, _lookMouse, _lookStick, _light, _heavy, _block, _parry, _jump, _dodge,
            _heal, _sprint, _sprintToggle, _lockOn, _switchScroll;
        private float _dodgePressedAt = -1f;
        private Vector3 _moveForward = Vector3.forward;
        private Vector3 _moveRight = Vector3.right;
        private Vector2 _heldMove;
        private bool _sprintToggled;
        private bool _stickFlickReady = true;

        public Vector3 MoveDirection { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool IsBlockHeld => _block.IsPressed();
        /// <summary>Sprint: hold Shift, click L3 (lasts until the stick is released) or hold dodge.</summary>
        public bool IsSprintHeld => _sprint.IsPressed() || _sprintToggled || IsDodgeHeldForSprint;

        private bool IsDodgeHeldForSprint => _dodgePressedAt >= 0f && Time.time - _dodgePressedAt >= sprintHoldTime;

        public bool IsHeld(CharacterAction action) => action switch
        {
            CharacterAction.LightAttack => _light.IsPressed(),
            CharacterAction.HeavyAttack => _heavy.IsPressed(),
            _ => false,
        };

        public event Action<CharacterAction> ActionRequested;

        private void Awake()
        {
            CreateActions();
            if (view == null && UnityEngine.Camera.main != null) view = UnityEngine.Camera.main.transform;
        }

        public void SetView(Transform viewTransform) => view = viewTransform;
        public void SetLockOn(LockOnController controller) => lockOn = controller;

        private void OnEnable()
        {
            _light.performed += OnLight;
            _heavy.performed += OnHeavy;
            _parry.performed += OnParry;
            _jump.performed += OnJump;
            _heal.performed += OnHeal;
            _dodge.started += OnDodgeStarted;
            _dodge.canceled += OnDodgeReleased;
            _sprintToggle.performed += OnSprintToggle;
            _lockOn.performed += OnLockOn;
            _switchScroll.performed += OnScroll;
            SetActionsEnabled(true);
        }

        private void OnDisable()
        {
            _light.performed -= OnLight;
            _heavy.performed -= OnHeavy;
            _parry.performed -= OnParry;
            _jump.performed -= OnJump;
            _heal.performed -= OnHeal;
            _dodge.started -= OnDodgeStarted;
            _dodge.canceled -= OnDodgeReleased;
            _sprintToggle.performed -= OnSprintToggle;
            _lockOn.performed -= OnLockOn;
            _switchScroll.performed -= OnScroll;
            SetActionsEnabled(false);
        }

        private void Update()
        {
            Vector2 move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            if (move.magnitude < sprintToggleStopThreshold) _sprintToggled = false;
            if (_walk.IsPressed()) move *= 0.5f;

            Vector2 stick = _lookStick.ReadValue<Vector2>();
            bool lockedOn = lockOn != null && lockOn.HasTarget;

            if (lockedOn)
            {
                LookDelta = Vector2.zero;
                HandleStickFlick(stick.x);
            }
            else
            {
                Vector2 look = _lookMouse.ReadValue<Vector2>() * mouseSensitivity
                               + stick * (stickSensitivity * Time.deltaTime);
                if (invertY) look.y = -look.y;
                LookDelta = look;
            }

            UpdateMoveFrame(move, lockedOn);
            MoveDirection = Vector3.ClampMagnitude(_moveForward * move.y + _moveRight * move.x, 1f);
        }

        // Movement is relative to the camera, but while the camera turns on its own to follow the
        // player, the directions are held so walking doesn't curve with it. They catch up with the
        // camera when the input is released or changed, the camera is turned by hand, or locked on.
        private void UpdateMoveFrame(Vector2 move, bool lockedOn)
        {
            bool idle = move.sqrMagnitude < 0.01f;
            bool changed = Vector2.Angle(move, _heldMove) > 30f;
            bool manualLook = LookDelta.magnitude > 0.5f;
            if (!holdMoveDirection || lockedOn || idle || changed || manualLook) RefreshMoveFrame();
            if (!idle) _heldMove = move;
        }

        private void RefreshMoveFrame()
        {
            if (view == null) return;
            _moveForward = view.forward.FlatNormalized();
            _moveRight = view.right.FlatNormalized();
        }

        private void HandleStickFlick(float x)
        {
            if (_stickFlickReady && Mathf.Abs(x) >= stickFlickThreshold)
            {
                lockOn.Switch(Mathf.Sign(x));
                _stickFlickReady = false;
            }
            else if (Mathf.Abs(x) < 0.3f)
            {
                _stickFlickReady = true;
            }
        }

        private void OnLight(InputAction.CallbackContext _) => ActionRequested?.Invoke(CharacterAction.LightAttack);
        private void OnHeavy(InputAction.CallbackContext _) => ActionRequested?.Invoke(CharacterAction.HeavyAttack);
        private void OnParry(InputAction.CallbackContext _) => ActionRequested?.Invoke(CharacterAction.Parry);
        private void OnJump(InputAction.CallbackContext _) => ActionRequested?.Invoke(CharacterAction.Jump);
        private void OnHeal(InputAction.CallbackContext _) => ActionRequested?.Invoke(CharacterAction.Heal);
        private void OnDodgeStarted(InputAction.CallbackContext _) => _dodgePressedAt = Time.time;

        private void OnDodgeReleased(InputAction.CallbackContext _)
        {
            bool wasTap = _dodgePressedAt >= 0f && Time.time - _dodgePressedAt < sprintHoldTime;
            _dodgePressedAt = -1f;
            if (wasTap) ActionRequested?.Invoke(CharacterAction.Dodge);
        }

        private void OnSprintToggle(InputAction.CallbackContext _) => _sprintToggled = !_sprintToggled;

        private void OnLockOn(InputAction.CallbackContext _) => lockOn?.Toggle();

        private void OnScroll(InputAction.CallbackContext ctx)
        {
            float y = ctx.ReadValue<Vector2>().y;
            if (lockOn != null && lockOn.HasTarget && Mathf.Abs(y) > 0.01f) lockOn.Switch(Mathf.Sign(y));
        }

        private void CreateActions()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _walk = Button("Walk", "<Keyboard>/leftAlt");
            _lookMouse = new InputAction("LookMouse", InputActionType.Value, "<Mouse>/delta");
            _lookStick = new InputAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            _light = Button("LightAttack", "<Mouse>/leftButton", "<Gamepad>/rightShoulder");
            _heavy = Button("HeavyAttack", "<Keyboard>/f", "<Gamepad>/rightTrigger");
            _block = Button("Block", "<Mouse>/rightButton", "<Gamepad>/leftShoulder");
            _parry = Button("Parry", "<Keyboard>/leftCtrl", "<Gamepad>/leftTrigger");
            _dodge = Button("Dodge", "<Keyboard>/space", "<Gamepad>/buttonEast");
            _jump = Button("Jump", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            // R and X / Square: free on both, and where Souls games put "use item".
            _heal = Button("Heal", "<Keyboard>/r", "<Gamepad>/buttonWest");
            _sprint = Button("Sprint", "<Keyboard>/leftShift");
            _sprintToggle = Button("SprintToggle", "<Gamepad>/leftStickPress");
            _lockOn = Button("LockOn", "<Keyboard>/q", "<Mouse>/middleButton", "<Gamepad>/rightStickPress");
            _switchScroll = new InputAction("SwitchTarget", InputActionType.Value, "<Mouse>/scroll");
        }

        private static InputAction Button(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            foreach (string binding in bindings) action.AddBinding(binding);
            return action;
        }

        private void SetActionsEnabled(bool enabled)
        {
            foreach (InputAction action in new[]
                     {
                         _move, _walk, _lookMouse, _lookStick, _light, _heavy, _block, _parry, _jump, _dodge, _heal, _sprint,
                         _sprintToggle, _lockOn, _switchScroll
                     })
            {
                if (enabled) action.Enable();
                else action.Disable();
            }
        }
    }
}
