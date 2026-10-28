using Conquest.Targeting;
using UnityEngine;

namespace Conquest.CameraSystem
{
    /// <summary>
    /// Orbit camera that frames the target when locked on. Put this on the camera; it needs a
    /// follow target, a look input and (optionally) a target provider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform followTarget;
        [SerializeField, Tooltip("A component implementing ILookInput (e.g. PlayerInputReader).")]
        private MonoBehaviour lookSource;
        [SerializeField, Tooltip("A component implementing ITargetProvider (e.g. LockOnController). Optional.")]
        private MonoBehaviour targetSource;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private float distance = 4.5f;
        [SerializeField] private float followSmoothTime = 0.05f;

        [Header("Orbit")]
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Lock-on")]
        [SerializeField, Tooltip("Degrees per second the camera turns to frame the target.")]
        private float lockOnTurnSpeed = 10f;
        [SerializeField, Tooltip("Pitch while locked on, before looking down at close targets.")]
        private float lockOnPitch = 12f;

        [Header("Auto-follow")]
        [SerializeField, Tooltip("When not locked on, swing the camera round behind the way the player walks.")]
        private bool autoFollow = true;
        [SerializeField, Tooltip("Degrees per second the camera turns while running sideways at full speed.")]
        private float autoFollowTurnSpeed = 90f;
        [SerializeField, Tooltip("Player speed (m/s) at which the camera turns at full rate.")]
        private float autoFollowFullSpeed = 4f;
        [SerializeField, Tooltip("Below this speed (m/s) the camera stays put.")]
        private float autoFollowMinSpeed = 0.5f;
        [SerializeField, Tooltip("Seconds after moving the camera by hand before auto-follow takes over again.")]
        private float autoFollowDelay = 0.5f;
        [SerializeField, Tooltip("Look input (degrees per frame) below this counts as a resting mouse or stick, not manual control.")]
        private float manualLookThreshold = 0.5f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = 1;
        [SerializeField] private float collisionRadius = 0.25f;

        private ILookInput _look;
        private ITargetProvider _targets;
        private Vector3 _pivot;
        private Vector3 _pivotVelocity;
        private float _yaw;
        private float _pitch = 15f;
        private Vector3 _lastFollowPosition;
        private float _lastManualLookTime = float.NegativeInfinity;

        private void Awake()
        {
            if (followTarget != null) Bind(followTarget, lookSource as ILookInput, targetSource as ITargetProvider);
        }

        public void Bind(Transform follow, ILookInput look, ITargetProvider targets)
        {
            followTarget = follow;
            _look = look;
            _targets = targets;
            _yaw = follow.eulerAngles.y;
            _pivot = follow.position + pivotOffset;
            _lastFollowPosition = follow.position;
        }

        private void LateUpdate()
        {
            if (followTarget == null) return;

            float dt = Time.deltaTime;
            Vector3 desiredPivot = followTarget.position + pivotOffset;
            _pivot = Vector3.SmoothDamp(_pivot, desiredPivot, ref _pivotVelocity, followSmoothTime);

            Vector3 moved = followTarget.position - _lastFollowPosition;
            _lastFollowPosition = followTarget.position;

            if (_targets != null && _targets.HasTarget) FrameTarget(_targets.CurrentTarget, dt);
            else
            {
                Orbit();
                AutoFollow(moved, dt);
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            CameraShakeSample shake = CameraShake.Sample(rotation);
            float actualDistance = ResolveCollision(_pivot, back, distance * (1f - shake.Zoom));

            transform.SetPositionAndRotation(_pivot + back * actualDistance + shake.Offset,
                rotation * Quaternion.Euler(0f, 0f, shake.Roll));
        }

        private void Orbit()
        {
            if (_look == null) return;
            Vector2 delta = _look.LookDelta;
            if (delta.magnitude > manualLookThreshold) _lastManualLookTime = Time.time;
            _yaw += delta.x;
            _pitch = Mathf.Clamp(_pitch - delta.y, minPitch, maxPitch);
        }

        // Souls-style auto-follow: the camera turns hardest when the player runs sideways and not at
        // all when they run straight at it, so it swings round behind them instead of spinning.
        private void AutoFollow(Vector3 moved, float dt)
        {
            if (!autoFollow || dt <= 0f || Time.time - _lastManualLookTime < autoFollowDelay) return;

            Vector3 flat = new Vector3(moved.x, 0f, moved.z);
            float speed = flat.magnitude / dt;
            if (speed < autoFollowMinSpeed) return;

            float angle = Mathf.DeltaAngle(_yaw, Quaternion.LookRotation(flat).eulerAngles.y);
            float sideways = Mathf.Sin(angle * Mathf.Deg2Rad);
            float step = autoFollowTurnSpeed * sideways * Mathf.Clamp01(speed / autoFollowFullSpeed) * dt;
            _yaw += Mathf.Clamp(step, -Mathf.Abs(angle), Mathf.Abs(angle));
        }

        private void FrameTarget(ITargetable target, float dt)
        {
            Vector3 toTarget = target.LockPoint.position - _pivot;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude < 0.01f) return;

            float targetYaw = Quaternion.LookRotation(flat).eulerAngles.y;
            // Look down a bit more at targets that are close or below us.
            float lookDown = Mathf.Atan2(-toTarget.y, flat.magnitude) * Mathf.Rad2Deg;
            float targetPitch = Mathf.Clamp(lockOnPitch + lookDown * 0.5f, minPitch, maxPitch);

            float t = 1f - Mathf.Exp(-lockOnTurnSpeed * dt);
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, t);
            _pitch = Mathf.LerpAngle(_pitch, targetPitch, t);
        }

        private float ResolveCollision(Vector3 pivot, Vector3 direction, float maxDistance)
        {
            return Physics.SphereCast(pivot, collisionRadius, direction, out RaycastHit hit, maxDistance,
                collisionMask, QueryTriggerInteraction.Ignore)
                ? Mathf.Max(hit.distance, 0.3f)
                : maxDistance;
        }
    }
}
