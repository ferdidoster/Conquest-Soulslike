using Conquest.Targeting;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Conquest.CameraSystem.Cinemachine
{
    public enum CameraMode
    {
        Isometric,
        ThirdPerson,
    }

    /// <summary>
    /// Switches between the Cinemachine isometric rig and the orbiting third-person camera.
    /// Lock-on picks targets relative to the camera in third person and relative to the
    /// character's facing in isometric (the view direction is fixed there).
    /// </summary>
    public sealed class CameraModeSwitcher : MonoBehaviour
    {
        [SerializeField] private CameraMode startMode = CameraMode.Isometric;
        [SerializeField] private ThirdPersonCamera thirdPerson;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private IsometricCameraRig isometric;
        [SerializeField] private LockOnController lockOn;
        [SerializeField] private Transform player;

        [Header("Input")]
        [SerializeField] private InputAction toggleMode = new InputAction("ToggleCameraMode", InputActionType.Button);
        [SerializeField] private InputAction rotateIsometric = new InputAction("RotateIsometric", InputActionType.Value);

        public CameraMode Mode { get; private set; }

        private void Reset()
        {
            toggleMode.AddBinding("<Keyboard>/v");
            toggleMode.AddBinding("<Gamepad>/select");
            rotateIsometric.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/z").With("Positive", "<Keyboard>/c");
        }

        private void Awake()
        {
            if (toggleMode.bindings.Count == 0) Reset();
        }

        private void OnEnable()
        {
            toggleMode.performed += OnToggle;
            toggleMode.Enable();
            rotateIsometric.Enable();
        }

        private void OnDisable()
        {
            toggleMode.performed -= OnToggle;
            toggleMode.Disable();
            rotateIsometric.Disable();
        }

        private void Start() => SetMode(startMode);

        public void SetMode(CameraMode mode)
        {
            Mode = mode;
            bool iso = mode == CameraMode.Isometric;

            if (thirdPerson != null) thirdPerson.enabled = !iso;
            if (brain != null) brain.enabled = iso;
            if (isometric != null) isometric.gameObject.SetActive(iso);
            if (lockOn != null) lockOn.SetView(iso ? player : transform);
        }

        private void OnToggle(InputAction.CallbackContext _) =>
            SetMode(Mode == CameraMode.Isometric ? CameraMode.ThirdPerson : CameraMode.Isometric);

        // Hold Z / C to turn the isometric view around the player.
        private void Update()
        {
            if (Mode != CameraMode.Isometric || isometric == null) return;
            float direction = rotateIsometric.ReadValue<float>();
            if (Mathf.Abs(direction) > 0.1f) isometric.Turn(direction);
        }
    }
}
