using Conquest.Core.Utilities;
using UnityEngine;

namespace Conquest.Character.States
{
    /// <summary>
    /// Free movement. Unlocked: the character turns toward where it moves and plays forward
    /// animations. Locked on: it keeps facing the target and strafes using the 2D blend tree.
    /// Guarding is a modifier of this state, so the character can walk with the shield up.
    /// </summary>
    public sealed class LocomotionState : CharacterState
    {
        // Set when stamina runs out mid-sprint: no sprinting until the sprint input is let go,
        // so an empty bar doesn't stutter between running and sprinting as it regenerates.
        private bool _winded;

        public LocomotionState(ActionCharacter character) : base(character) { }

        protected override void OnEnter() => Character.Animator.PlayLocomotion();

        protected override void OnTick(float dt)
        {
            LocomotionSettings settings = Character.Locomotion;
            Vector3 input = Character.Input.MoveDirection;
            float magnitude = Mathf.Clamp01(input.magnitude);
            Vector3 direction = magnitude > 0.01f ? input / magnitude : Vector3.zero;

            bool guarding = Character.Input.IsBlockHeld;
            Character.SetGuarding(guarding);

            bool sprintHeld = Character.Input.IsSprintHeld;
            if (!sprintHeld) _winded = false;
            bool sprinting = !guarding && !_winded && magnitude > 0.1f && sprintHeld && Character.Stamina.CanAct;
            if (sprinting)
            {
                Character.Stamina.Spend(settings.sprintStaminaPerSecond * dt);
                _winded = !Character.Stamina.CanAct;
            }

            float speed = ResolveSpeed(settings, magnitude, sprinting);
            if (guarding) speed *= settings.guardSpeedMultiplier;

            Character.Motor.SetPlanarVelocity(direction * speed);

            bool strafing = Character.HasTarget && !sprinting;
            float animMagnitude = ToAnimationMagnitude(settings, speed);
            Vector2 localMove;

            if (strafing)
            {
                Character.Motor.TurnTowards(Character.DirectionToTarget(), dt, settings.lockOnTurnSpeed);
                Vector3 local = Character.transform.InverseTransformDirection(direction) * animMagnitude;
                localMove = new Vector2(local.x, local.z);
            }
            else
            {
                Character.Motor.TurnTowards(direction, dt, settings.turnSpeed);
                localMove = new Vector2(0f, animMagnitude);
            }

            Character.Animator.SetLockedOn(strafing);
            Character.Animator.SetLocomotion(localMove, dt);

            Character.TryStartBufferedAction();
        }

        protected override void OnExit() => Character.SetGuarding(false);

        private static float ResolveSpeed(LocomotionSettings s, float magnitude, bool sprinting)
        {
            if (magnitude < 0.01f) return 0f;
            if (sprinting) return s.sprintSpeed;
            return magnitude < s.runThreshold
                ? s.walkSpeed * Mathf.InverseLerp(0f, s.runThreshold, magnitude)
                : Mathf.Lerp(s.walkSpeed, s.runSpeed, Mathf.InverseLerp(s.runThreshold, 1f, magnitude));
        }

        /// <summary>Maps real speed onto the blend tree scale: walk = 0.5, run = 1, sprint = 1.5.</summary>
        private static float ToAnimationMagnitude(LocomotionSettings s, float speed)
        {
            if (speed <= s.walkSpeed) return 0.5f * Mathf.InverseLerp(0f, s.walkSpeed, speed);
            if (speed <= s.runSpeed) return 0.5f + 0.5f * Mathf.InverseLerp(s.walkSpeed, s.runSpeed, speed);
            return 1f + 0.5f * Mathf.InverseLerp(s.runSpeed, s.sprintSpeed, speed);
        }
    }
}
