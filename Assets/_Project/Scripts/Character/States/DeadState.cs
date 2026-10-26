using Conquest.Animation;
using UnityEngine;

namespace Conquest.Character.States
{
    public sealed class DeadState : CharacterState
    {
        private const float ShoveTime = 0.45f;

        private Vector3 _shove;

        public DeadState(ActionCharacter character) : base(character) { }

        /// <summary>Pushes the body back along the killing blow while it falls.</summary>
        public void Shove(Vector3 velocity) => _shove = velocity;

        protected override void OnEnter()
        {
            _shove = Vector3.zero;
            Character.SetGuarding(false);
            Character.HitDetector?.Close();
            Character.Motor.Stop();
            Character.Animator.Play(AnimatorStates.Death, 0.1f);
        }

        protected override void OnTick(float dt)
        {
            float remaining = Mathf.Clamp01(1f - Elapsed / ShoveTime);
            Character.Motor.SetPlanarVelocityImmediate(_shove * (remaining * remaining));
        }
    }
}
