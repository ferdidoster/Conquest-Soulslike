using Conquest.Animation;
using Conquest.Core.Input;
using UnityEngine;

namespace Conquest.Character.States
{
    /// <summary>
    /// A jump: launches the character to <see cref="JumpSettings.height"/>, keeps the run-up's
    /// momentum with a little steering in the air, and hands back to locomotion on landing.
    /// An attack pressed in the air becomes the moveset's jump attack.
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
            if (Elapsed < 0.1f) return;
            if (!Character.Motor.IsGrounded)
            {
                Character.TryStartBufferedAction(IsAttack);
                return;
            }
            if (!Character.TryStartBufferedAction()) Character.EnterLocomotion();
        }

        private static bool IsAttack(CharacterAction action) =>
            action == CharacterAction.LightAttack || action == CharacterAction.HeavyAttack;
    }
}
