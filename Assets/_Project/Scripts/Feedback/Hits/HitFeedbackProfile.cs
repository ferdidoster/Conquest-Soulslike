using System;
using Conquest.Combat;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// How every kind of hit feels: freeze, shake, slow motion, particles, blood, flash and sound.
    /// One profile can be shared by all characters; tune it while playing.
    /// </summary>
    [CreateAssetMenu(menuName = "Conquest/Feedback/Hit Feedback Profile", fileName = "HitFeedback")]
    public sealed class HitFeedbackProfile : ScriptableObject
    {
        [Tooltip("Values marked 'scaled' grow with the blow's weight: 0.5 + 0.5 * weight.")]
        public HitFeedbackEntry[] entries = Array.Empty<HitFeedbackEntry>();
        [Tooltip("The attacker never freezes longer than this (seconds), however heavy the blow, so landing " +
                 "a hit doesn't stall the next input. The defender still gets the full hit-stop.")]
        [Min(0f)] public float maxAttackerHitStop = 0.07f;

        /// <summary>Version of the generated defaults; lets the setup tool refresh untouched profiles.</summary>
        [HideInInspector] public int defaultsVersion;

        public bool TryGet(HitResult result, out HitFeedbackEntry entry)
        {
            foreach (HitFeedbackEntry candidate in entries)
            {
                if (candidate.result != result) continue;
                entry = candidate;
                return true;
            }

            entry = null;
            return false;
        }

        /// <summary>The multiplier applied to scaled values for a blow of this weight.</summary>
        public static float WeightScale(float weight) => 0.5f + 0.5f * weight;
    }

    [Serializable]
    public sealed class HitFeedbackEntry
    {
        public HitResult result;

        [Header("Freeze (scaled)")]
        [Tooltip("Seconds the defender freezes.")]
        [Min(0f)] public float hitStop = 0.08f;
        [Tooltip("The attacker freezes for hitStop times this.")]
        [Min(0f)] public float attackerHitStop = 1f;
        [Tooltip("How far (m) the defender's model shudders during the freeze.")]
        [Min(0f)] public float shudder = 0.04f;

        [Header("Camera (scaled)")]
        [Range(0f, 1f)] public float trauma = 0.3f;
        [Tooltip("Metres the camera jolts along the blow.")]
        [Min(0f)] public float kick = 0.1f;
        [Range(0f, 0.3f)] public float zoomPunch;

        [Header("Slow motion")]
        [Range(0.05f, 1f)] public float slowMotionScale = 1f;
        [Tooltip("Real-time seconds. 0 = no slow motion.")]
        [Min(0f)] public float slowMotionDuration;

        [Header("Visuals")]
        [Tooltip("Burst played at the hit point (sparks, flash, ...). Optional.")]
        public ParticleSystem effect;
        [Min(0f)] public float effectScale = 1f;
        [Tooltip("Plays the effect pointing back at the attacker (sparks off a shield) instead of along the blow.")]
        public bool effectTowardAttacker;
        [Tooltip("Sprays the defender's blood. Amount is multiplied by the blow's weight.")]
        public bool bleed;
        [Min(0f)] public float bloodAmount = 1f;
        [Tooltip("Leaves a growing pool of blood under the defender (deaths).")]
        public bool bloodPool;
        public Color flashColor = Color.white;
        [Tooltip("Seconds the defender flashes. 0 = no flash.")]
        [Min(0f)] public float flashDuration;

        [Header("Audio")]
        public AudioClip[] sounds = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        public Vector2 pitch = new Vector2(0.92f, 1.08f);
    }
}
