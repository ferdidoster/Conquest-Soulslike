using System;
using UnityEngine;

namespace Conquest.Combat
{
    [Serializable]
    public sealed class DefenseSettings
    {
        [Tooltip("Hits within this angle in front of the defender can be blocked or parried.")]
        [Range(0f, 360f)] public float guardAngle = 140f;
        [Tooltip("Fraction of damage that still goes through a block.")]
        [Range(0f, 1f)] public float blockDamageThrough = 0f;
        [Tooltip("Multiplier on the attack's guard damage (lower = sturdier shield).")]
        [Min(0f)] public float guardStaminaMultiplier = 1f;
    }
}
