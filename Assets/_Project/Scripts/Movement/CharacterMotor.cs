using Conquest.Core.Utilities;
using UnityEngine;

namespace Conquest.Movement
{
    /// <summary>
    /// Thin wrapper around Unity's CharacterController. Callers set a desired planar velocity
    /// each frame; the motor handles gravity, grounding and turning. It has no idea who is
    /// driving it (player, AI, cutscene).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float groundedStickForce = -2f;
        [SerializeField, Tooltip("Degrees per second.")] private float defaultTurnSpeed = 720f;
        [SerializeField, Tooltip("How quickly the planar velocity reaches its target.")]
        private float acceleration = 30f;

        private CharacterController _controller;
        private Vector3 _planarVelocity;
        private Vector3 _targetPlanarVelocity;
        private float _verticalVelocity;
        private bool _snapVelocity;

        public bool IsGrounded => _controller.isGrounded;
        /// <summary>A paused motor does not move at all, not even fall (used for hit-stop).</summary>
        public bool IsPaused { get; set; }
        public Vector3 Velocity => _planarVelocity + Vector3.up * _verticalVelocity;
        public Vector3 PlanarVelocity => _planarVelocity;
        public float DefaultTurnSpeed => defaultTurnSpeed;
        /// <summary>Downward acceleration in m/s² (negative).</summary>
        public float Gravity => gravity;

        private void Awake() => _controller = GetComponent<CharacterController>();

        /// <summary>Requests a planar velocity, smoothed by the motor's acceleration.</summary>
        public void SetPlanarVelocity(Vector3 velocity)
        {
            _targetPlanarVelocity = velocity.Flat();
            _snapVelocity = false;
        }

        /// <summary>Applies a planar velocity this frame without smoothing (rolls, lunges).</summary>
        public void SetPlanarVelocityImmediate(Vector3 velocity)
        {
            _targetPlanarVelocity = velocity.Flat();
            _snapVelocity = true;
        }

        public void Stop() => SetPlanarVelocityImmediate(Vector3.zero);

        /// <summary>Sends the character upward at <paramref name="upwardSpeed"/> m/s (jumps); gravity brings it down.</summary>
        public void Launch(float upwardSpeed) => _verticalVelocity = upwardSpeed;

        /// <summary>Rotates toward a direction at <paramref name="degreesPerSecond"/> (default turn speed if negative).</summary>
        public void TurnTowards(Vector3 direction, float deltaTime, float degreesPerSecond = -1f)
        {
            Vector3 flat = direction.FlatNormalized();
            if (flat == Vector3.zero) return;

            float speed = degreesPerSecond < 0f ? defaultTurnSpeed : degreesPerSecond;
            Quaternion target = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, speed * deltaTime);
        }

        public void FaceImmediately(Vector3 direction)
        {
            Vector3 flat = direction.FlatNormalized();
            if (flat != Vector3.zero) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }

        private void Update()
        {
            if (IsPaused) return;
            float dt = Time.deltaTime;

            _planarVelocity = _snapVelocity
                ? _targetPlanarVelocity
                : Vector3.MoveTowards(_planarVelocity, _targetPlanarVelocity, acceleration * dt);

            _verticalVelocity = IsGrounded && _verticalVelocity < 0f
                ? groundedStickForce
                : _verticalVelocity + gravity * dt;

            if (_controller.enabled)
                _controller.Move((_planarVelocity + Vector3.up * _verticalVelocity) * dt);
        }

        public void SetCollisionEnabled(bool isEnabled) => _controller.enabled = isEnabled;

        /// <summary>Moves the character instantly (respawn), dropping any velocity it had.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false; // a CharacterController overrides direct position changes
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
            _planarVelocity = _targetPlanarVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }
    }
}
