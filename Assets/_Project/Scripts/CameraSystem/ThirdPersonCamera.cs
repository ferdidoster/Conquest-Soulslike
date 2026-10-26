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

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = 1;
        [SerializeField] private float collisionRadius = 0.25f;

        private ILookInput _look;
        private ITargetProvider _targets;
        private Vector3 _pivot;
        private Vector3 _pivotVelocity;
        private float _yaw;
        private float _pitch = 15f;

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
        }

        private void LateUpdate()
        {
            if (followTarget == null) return;

            float dt = Time.deltaTime;
            Vector3 desiredPivot = followTarget.position + pivotOffset;
            _pivot = Vector3.SmoothDamp(_pivot, desiredPivot, ref _pivotVelocity, followSmoothTime);

            if (_targets != null && _targets.HasTarget) FrameTarget(_targets.CurrentTarget, dt);
            else Orbit();

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
            _yaw += delta.x;
            _pitch = Mathf.Clamp(_pitch - delta.y, minPitch, maxPitch);
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
