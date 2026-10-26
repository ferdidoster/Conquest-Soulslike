using System;
using Conquest.Animation;
using UnityEngine;

namespace Conquest.Character
{
    /// <summary>A single involuntary reaction (flinch, block stun, stagger, ...).</summary>
    [Serializable]
    public sealed class Reaction
    {
        public string animationState;
        [Min(0.05f)] public float duration = 0.5f;
        [Tooltip("Initial push-back speed in m/s, decays to zero over the duration.")]
        [Min(0f)] public float knockback = 2f;
        [Tooltip("Keeps the guard up during the reaction (block stun).")]
        public bool keepGuard;

        public Reaction(string state, float duration, float knockback, bool keepGuard = false)
        {
            animationState = state;
            this.duration = duration;
            this.knockback = knockback;
            this.keepGuard = keepGuard;
        }
    }

    [Serializable]
    public sealed class ReactionSettings
    {
        public Reaction hit = new Reaction(AnimatorStates.HitReact, 0.5f, 4.5f);
        [Tooltip("Played instead of the flinch when a blow is at least heavyHitWeight heavy.")]
        public Reaction heavyHit = new Reaction(AnimatorStates.Stagger, 1.1f, 7f);
        [Min(0.1f)] public float heavyHitWeight = 1.8f;
        [Tooltip("Lighter blows (a gun's chip shots) hurt without a flinch, so they don't interrupt.")]
        [Min(0f)] public float flinchWeight = 0.5f;
        public Reaction blocked = new Reaction(AnimatorStates.BlockHit, 0.4f, 3f, keepGuard: true);
        public Reaction guardBreak = new Reaction(AnimatorStates.GuardBreak, 1.1f, 4f);
        [Tooltip("Played on an attacker whose attack was parried.")]
        public Reaction parried = new Reaction(AnimatorStates.Stagger, 1.6f, 2.5f);

        public Reaction ForHit(float weight) => weight >= heavyHitWeight ? heavyHit : hit;
    }
}
