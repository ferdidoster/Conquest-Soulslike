using Conquest.Animation;
using Conquest.Core.Input;
using UnityEngine;

namespace Conquest.Character.States
{
    /// <summary>
    /// The gun parry: draws the pistol, fires once at <see cref="ParrySettings.shotAt"/> and holsters
    /// it again. Whether the shot parries is up to the <see cref="Combat.ParryGun"/> and the target's
    /// timing, so this state only aims, fires and lets a dodge cancel the recovery.
    /// </summary>
    public sealed class ShootState : TimedState
    {
        private bool _fired;

        public ShootState(ActionCharacter character) : base(character) { }

        protected override float Duration => Character.Parry.shotDuration;

        protected override void OnEnter()
        {
            _fired = false;
            Character.Stamina.Spend(Character.Parry.staminaCost);
            Character.FaceIntendedDirection();
            Character.Motor.Stop();
            Character.Gun.Draw();
            Character.Animator.Play(AnimatorStates.Shoot, 0.05f);
        }

        protected override void OnTick(float dt)
        {
            Character.Motor.SetPlanarVelocity(Vector3.zero);
            float t = Progress;

            if (!_fired)
            {
                // Track the target until the trigger is pulled.
                Character.FaceIntendedDirection();
                if (t < Character.Parry.shotAt) return;
                _fired = true;
                Character.Gun.Fire(Character.IntendedFacing());
            }

            if (t >= Character.Parry.shotDodgeCancelFrom &&
                Character.TryStartBufferedAction(a => a == CharacterAction.Dodge)) return;
            if (IsFinished) Character.EnterLocomotion();
        }

        protected override void OnExit() => Character.Gun.Holster();
    }
}
