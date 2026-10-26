using Conquest.Animation;

namespace Conquest.Character.States
{
    /// <summary>A short deflect. Hits landing inside the active window stagger the attacker.</summary>
    public sealed class ParryState : TimedState
    {
        public ParryState(ActionCharacter character) : base(character) { }

        protected override float Duration => Character.Parry.duration;

        protected override void OnEnter()
        {
            Character.Stamina.Spend(Character.Parry.staminaCost);
            Character.FaceIntendedDirection();
            Character.Motor.Stop();
            Character.Animator.Play(AnimatorStates.Parry, 0.05f);
        }

        protected override void OnTick(float dt)
        {
            Character.Motor.SetPlanarVelocity(UnityEngine.Vector3.zero);
            Character.Receiver.IsParrying = Character.Parry.activeWindow.Contains(Progress);
            if (IsFinished) Character.EnterLocomotion();
        }

        protected override void OnExit() => Character.Receiver.IsParrying = false;
    }
}
