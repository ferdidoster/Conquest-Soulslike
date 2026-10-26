using UnityEngine;

namespace Conquest.Character.States
{
    /// <summary>Any involuntary reaction: flinch, block stun, guard break, parried stagger.</summary>
    public sealed class ReactionState : TimedState
    {
        private Reaction _reaction;
        private Vector3 _pushDirection;
        private float _pushScale;

        public ReactionState(ActionCharacter character) : base(character) { }

        protected override float Duration => _reaction.duration;

        /// <param name="pushScale">Multiplies the reaction's knockback (heavier blows push further).</param>
        public ReactionState With(Reaction reaction, Vector3 pushDirection, float pushScale = 1f)
        {
            _reaction = reaction;
            _pushDirection = pushDirection;
            _pushScale = pushScale;
            return this;
        }

        protected override void OnEnter()
        {
            Character.SetGuarding(_reaction.keepGuard);

            // Face the attacker so the hit animation reads correctly.
            if (_pushDirection.sqrMagnitude > 0.01f) Character.Motor.FaceImmediately(-_pushDirection);
            Character.Animator.Play(_reaction.animationState, 0.05f);
        }

        protected override void OnTick(float dt)
        {
            // Quadratic ease-out: a hard shove that skids to a stop.
            float remaining = 1f - Progress;
            float speed = _reaction.knockback * _pushScale * remaining * remaining;
            Character.Motor.SetPlanarVelocityImmediate(_pushDirection * speed);
            if (IsFinished) Character.EnterLocomotion();
        }

        protected override void OnExit() => Character.SetGuarding(false);
    }
}
