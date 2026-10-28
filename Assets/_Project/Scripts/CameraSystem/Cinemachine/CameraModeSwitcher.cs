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
    /// Lock-on always picks targets by what the camera shows, in both modes.
    /// </summary>
    public sealed class CameraModeSwitcher : MonoBehaviour
    {
        [SerializeField] private CameraMode startMode = CameraMode.Isometric;
        [SerializeField] private ThirdPersonCamera thirdPerson;
        [SerializeField] private CinemachineBrain brain;
        [SerializeField] private IsometricCameraRig isometric;
        [SerializeField] private LockOnController lockOn;
        [SerializeField] private Transform player;

        [Header("Isometric auto-follow")]
        [SerializeField, Tooltip("When not locked on, slowly turn the isometric view behind the way the player walks.")]
        private bool autoFollowIsometric;
        [SerializeField, Range(0f, 1f), Tooltip("Turn rate as a share of the rig's turn speed, at full sideways run.")]
        private float isometricFollowStrength = 0.4f;
        [SerializeField, Tooltip("Player speed (m/s) at which the view turns at full rate.")]
        private float isometricFollowFullSpeed = 4f;
        [SerializeField, Tooltip("Seconds after turning the view by hand before auto-follow takes over again.")]
        private float isometricFollowDelay = 1f;

        [Header("Input")]
        [SerializeField] private InputAction toggleMode = new InputAction("ToggleCameraMode", InputActionType.Button);
        [SerializeField] private InputAction rotateIsometric = new InputAction("RotateIsometric", InputActionType.Value);

        public CameraMode Mode { get; private set; }

        private Vector3 _lastPlayerPosition;
        private float _lastManualTurnTime = float.NegativeInfinity;

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

        private void Start()
        {
            if (player != null) _lastPlayerPosition = player.position;
            SetMode(startMode);
        }

        public void SetMode(CameraMode mode)
        {
            Mode = mode;
            bool iso = mode == CameraMode.Isometric;

            if (thirdPerson != null) thirdPerson.enabled = !iso;
            if (brain != null) brain.enabled = iso;
            if (isometric != null) isometric.gameObject.SetActive(iso);
            if (lockOn != null) lockOn.SetView(transform);
        }

        private void OnToggle(InputAction.CallbackContext _) =>
            SetMode(Mode == CameraMode.Isometric ? CameraMode.ThirdPerson : CameraMode.Isometric);

        // Hold Z / C to turn the isometric view around the player.
        private void Update()
        {
            Vector3 moved = Vector3.zero;
            if (player != null)
            {
                moved = player.position - _lastPlayerPosition;
                _lastPlayerPosition = player.position;
            }

            if (Mode != CameraMode.Isometric || isometric == null) return;
            float direction = rotateIsometric.ReadValue<float>();
            if (Mathf.Abs(direction) > 0.1f)
            {
                isometric.Turn(direction);
                _lastManualTurnTime = Time.time;
            }
            else AutoFollowIsometric(moved);
        }

        // Same idea as the third-person auto-follow: turn when running sideways, never when running
        // straight at the camera.
        private void AutoFollowIsometric(Vector3 moved)
        {
            float dt = Time.deltaTime;
            if (!autoFollowIsometric || player == null || dt <= 0f) return;
            if (lockOn != null && lockOn.HasTarget) return;
            if (Time.time - _lastManualTurnTime < isometricFollowDelay) return;

            Vector3 flat = new Vector3(moved.x, 0f, moved.z);
            float speed = flat.magnitude / dt;
            if (speed < 0.5f) return;

            float angle = Mathf.DeltaAngle(isometric.Yaw, Quaternion.LookRotation(flat).eulerAngles.y);
            float sideways = Mathf.Sin(angle * Mathf.Deg2Rad);
            isometric.Turn(sideways * isometricFollowStrength * Mathf.Clamp01(speed / isometricFollowFullSpeed));
        }
    }
}
