using Conquest.Targeting;
using Unity.Cinemachine;
using UnityEngine;

namespace Conquest.CameraSystem.Cinemachine
{
    /// <summary>
    /// Isometric view built from two Cinemachine cameras: one follows the player, the other frames
    /// the player and the lock-on target together. Cinemachine blends between them whenever the
    /// lock-on target changes. The pitch is fixed; the yaw can be turned freely (<see cref="Turn"/>)
    /// or in steps (<see cref="Rotate"/>), and the view eases toward the new angle.
    /// </summary>
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera followCamera;
        [SerializeField] private CinemachineCamera lockOnCamera;
        [SerializeField] private CinemachineTargetGroup lockOnGroup;
        [SerializeField] private Transform player;
        [SerializeField] private LockOnController lockOn;

        [Header("View")]
        [SerializeField, Range(10f, 80f), Tooltip("Downward camera angle. Classic isometric is 30-35.")]
        private float pitch = 35f;
        [SerializeField, Tooltip("Camera yaw. 45 gives the classic diagonal view.")]
        private float yaw = 45f;
        [SerializeField] private float distance = 25f;
        [SerializeField, Tooltip("Degrees per second while the turn keys are held.")]
        private float turnSpeed = 110f;
        [SerializeField, Tooltip("Seconds the view takes to catch up with a turn.")]
        private float turnSmoothing = 0.12f;
        [SerializeField, Tooltip("Orthographic size (half the visible height in metres).")]
        private float orthographicSize = 6.5f;

        [Header("Look ahead")]
        [SerializeField, Tooltip("Metres the view slides ahead of the player in the direction they walk.")]
        private float walkLookAhead = 2.5f;
        [SerializeField, Tooltip("Player speed (m/s) at which the view leads by the full distance.")]
        private float walkLookAheadFullSpeed = 4f;
        [SerializeField, Tooltip("Seconds the view takes to slide ahead or back.")]
        private float walkLookAheadSmoothing = 0.6f;

        private const int ActivePriority = 20;
        private const int InactivePriority = 0;

        private float _shownYaw;
        private float _yawVelocity;
        private CinemachinePositionComposer _followComposer;
        private Vector3 _baseTargetOffset;
        private Vector3 _lastPlayerPosition;
        private Vector3 _lookAhead;
        private Vector3 _lookAheadVelocity;

        /// <summary>The yaw the view is turning to.</summary>
        public float Yaw => yaw;

        private void Awake()
        {
            _shownYaw = yaw;
            ApplyView();
            if (followCamera != null && followCamera.TryGetComponent(out _followComposer))
                _baseTargetOffset = _followComposer.TargetOffset;
            if (player != null) _lastPlayerPosition = player.position;
        }

        private void LateUpdate()
        {
            UpdateLookAhead();
            if (Mathf.Approximately(_shownYaw, yaw)) return;
            _shownYaw = Mathf.SmoothDampAngle(_shownYaw, yaw, ref _yawVelocity, turnSmoothing);
            if (Mathf.Abs(Mathf.DeltaAngle(_shownYaw, yaw)) < 0.01f) _shownYaw = yaw;
            ApplyView();
        }

        private void OnEnable()
        {
            if (lockOn != null) lockOn.TargetChanged += OnTargetChanged;
            OnTargetChanged(lockOn != null ? lockOn.CurrentTarget : null);
        }

        private void OnDisable()
        {
            if (lockOn != null) lockOn.TargetChanged -= OnTargetChanged;
        }

        private void OnValidate()
        {
            _shownYaw = yaw;
            if (followCamera != null && lockOnCamera != null) ApplyView();
        }

        /// <summary>Turns the view by a step (e.g. 45 or 90 degrees); the camera eases there.</summary>
        public void Rotate(float degrees) => yaw = Mathf.Repeat(yaw + degrees, 360f);

        /// <summary>Turns the view continuously; input is -1..1 (hold to keep turning).</summary>
        public void Turn(float input) => Rotate(Mathf.Clamp(input, -1f, 1f) * turnSpeed * Time.deltaTime);

        /// <summary>Jumps straight to a yaw, without easing.</summary>
        public void SetYaw(float value)
        {
            yaw = Mathf.Repeat(value, 360f);
            _shownYaw = yaw;
            _yawVelocity = 0f;
            ApplyView();
        }

        private void ApplyView()
        {
            Quaternion rotation = Quaternion.Euler(pitch, _shownYaw, 0f);
            foreach (CinemachineCamera cam in new[] { followCamera, lockOnCamera })
            {
                if (cam == null) continue;
                cam.transform.rotation = rotation;
                cam.Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
                cam.Lens.OrthographicSize = orthographicSize;
                cam.Lens.NearClipPlane = 0.1f;
                cam.Lens.FarClipPlane = distance * 4f;
                if (cam.TryGetComponent(out CinemachinePositionComposer composer)) composer.CameraDistance = distance;
            }
        }

        // Slides the view ahead of the player in the way they walk so they see what is coming.
        // The view never rotates, so the controls stay the same.
        private void UpdateLookAhead()
        {
            if (_followComposer == null || player == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 moved = player.position - _lastPlayerPosition;
            _lastPlayerPosition = player.position;
            moved.y = 0f;
            float speed = moved.magnitude / dt;

            Vector3 target = speed > 0.5f
                ? moved.normalized * (walkLookAhead * Mathf.Clamp01(speed / walkLookAheadFullSpeed))
                : Vector3.zero;
            _lookAhead = Vector3.SmoothDamp(_lookAhead, target, ref _lookAheadVelocity, walkLookAheadSmoothing);

            // The composer's offset is in the player's local space.
            _followComposer.TargetOffset = _baseTargetOffset + player.InverseTransformDirection(_lookAhead);
        }

        private void OnTargetChanged(ITargetable target)
        {
            if (lockOnGroup == null || lockOnCamera == null) return;

            lockOnGroup.Targets.Clear();
            if (target != null)
            {
                lockOnGroup.AddMember(player, 1f, 1f);
                lockOnGroup.AddMember(target.LockPoint, 1f, 1f);
            }

            lockOnCamera.Priority = target != null ? ActivePriority : InactivePriority;
        }
    }
}
