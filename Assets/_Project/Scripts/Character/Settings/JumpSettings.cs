using System;
using UnityEngine;

namespace Conquest.Character
{
    [Serializable]
    public sealed class JumpSettings
    {
        [Tooltip("Peak height of a jump, in metres.")]
        [Min(0.1f)] public float height = 1.1f;
        [Min(0f)] public float staminaCost = 10f;
        [Tooltip("How quickly the stick can change the jump's direction in the air (m/s²).")]
        [Min(0f)] public float airControl = 6f;
        [Tooltip("Jumping from a stand still covers at least this much ground speed toward the stick.")]
        [Min(0f)] public float standingJumpSpeed = 2.5f;
    }
}
