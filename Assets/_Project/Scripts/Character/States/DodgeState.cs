using Conquest.Animation;
using UnityEngine;

namespace Conquest.Character.States
{
    /// <summary>
    /// Roll in the input direction, or backstep without input. Locked on, the character keeps
    /// facing the target and plays a directional roll; unlocked, it turns and rolls forward.
    /// </summary>
    public sealed class DodgeState : TimedState
    {
        private DodgeMove _move;
        private Vector3 _direction;

        public DodgeState(ActionCharacter character) : base(character) { }

        protected override float Duration => _move.duration;

        protected override void OnEnter()
        {
            Vector3 input = Character.Input.MoveDirection;
            bool hasInput = input.sqrMagnitude > 0.04f;

            _move = hasInput ? Character.Dodge.roll : Character.Dodge.backstep;
            _direction = hasInput ? input.normalized : -Character.transform.forward;

            Character.Stamina.Spend(_move.staminaCost);
            Character.Animator.Play(hasInput ? ChooseRollState() : AnimatorStates.Backstep, 0.05f);
        }

        protected override void OnTick(float dt)
        {
            float t = Progress;
            Character.Receiver.IsInvulnerable = _move.invulnerability.Contains(t);
            Character.Motor.SetPlanarVelocityImmediate(_direction * _move.speed.Evaluate(t));

            if (t >= _move.recoveryFrom && Character.TryStartBufferedAction()) return;
            if (IsFinished || (t >= _move.moveCancelFrom && Character.WantsToBreakRecovery))
                Character.EnterLocomotion();
        }

        protected override void OnExit() => Character.Receiver.IsInvulnerable = false;

        private string ChooseRollState()
        {
            if (!Character.HasTarget)
            {
                Character.Motor.FaceImmediately(_direction);
                return AnimatorStates.RollForward;
            }

            Vector3 local = Character.transform.InverseTransformDirection(_direction);
            if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
                return local.x > 0f ? AnimatorStates.RollRight : AnimatorStates.RollLeft;
            return local.z >= 0f ? AnimatorStates.RollForward : AnimatorStates.RollBackward;
        }
    }
}
