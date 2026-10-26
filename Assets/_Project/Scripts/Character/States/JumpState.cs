using Conquest.Animation;
using UnityEngine;

namespace Conquest.Character.States
{
    /// <summary>
    /// A jump: launches the character to <see cref="JumpSettings.height"/>, keeps the run-up's
    /// momentum with a little steering in the air, and hands back to locomotion on landing.
    /// </summary>
    public sealed class JumpState : CharacterState
    {
        private Vector3 _velocity;
        private float _speed;

        public JumpState(ActionCharacter character) : base(character) { }

        protected override void OnEnter()
        {
            JumpSettings jump = Character.Jump;
            Character.Stamina.Spend(jump.staminaCost);

            Vector3 input = Character.Input != null ? Character.Input.MoveDirection : Vector3.zero;
            _velocity = Character.Motor.PlanarVelocity;
            _speed = Mathf.Max(_velocity.magnitude, input.magnitude * jump.standingJumpSpeed);
            if (_velocity.sqrMagnitude < 0.01f) _velocity = input * _speed;

            Character.Motor.Launch(Mathf.Sqrt(2f * Mathf.Abs(Character.Motor.Gravity) * jump.height));
            Character.Animator.Play(AnimatorStates.Jump, 0.05f);
        }

        protected override void OnTick(float dt)
        {
            Vector3 input = Character.Input != null ? Character.Input.MoveDirection : Vector3.zero;
            _velocity = Vector3.MoveTowards(_velocity, input * _speed, Character.Jump.airControl * dt);
            Character.Motor.SetPlanarVelocityImmediate(_velocity);
            Character.Motor.TurnTowards(Character.HasTarget ? Character.DirectionToTarget() : _velocity, dt);

            // Give the controller a moment to leave the ground before checking for the landing.
            if (Elapsed < 0.1f || !Character.Motor.IsGrounded) return;
            if (!Character.TryStartBufferedAction()) Character.EnterLocomotion();
        }
    }
}
