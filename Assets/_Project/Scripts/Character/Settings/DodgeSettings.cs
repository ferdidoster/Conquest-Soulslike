using System;
using Conquest.Core.Utilities;
using UnityEngine;

namespace Conquest.Character
{
    [Serializable]
    public sealed class DodgeMove
    {
        [Min(0.05f)] public float duration = 0.8f;
        [Tooltip("Speed in m/s over normalized time.")]
        public AnimationCurve speed = new AnimationCurve(
            new Keyframe(0f, 7f), new Keyframe(0.5f, 6f), new Keyframe(0.75f, 1.5f), new Keyframe(1f, 0f));
        public TimeWindow invulnerability = new TimeWindow(0.05f, 0.5f);
        [Tooltip("Buffered actions may interrupt the recovery from this point.")]
        [Range(0f, 1f)] public float recoveryFrom = 0.65f;
        [Tooltip("From this point moving the stick or raising the guard ends the recovery (player only).")]
        [Range(0f, 1f)] public float moveCancelFrom = 0.75f;
        [Min(0f)] public float staminaCost = 20f;
    }

    [Serializable]
    public sealed class DodgeSettings
    {
        public DodgeMove roll = new DodgeMove();

        public DodgeMove backstep = new DodgeMove
        {
            duration = 0.5f,
            speed = new AnimationCurve(new Keyframe(0f, 6f), new Keyframe(0.5f, 2f), new Keyframe(1f, 0f)),
            invulnerability = new TimeWindow(0.05f, 0.35f),
            recoveryFrom = 0.6f,
            moveCancelFrom = 0.7f,
            staminaCost = 12f,
        };
    }
}
