using System;
using UnityEngine;

namespace Conquest.Character
{
    [Serializable]
    public sealed class LocomotionSettings
    {
        [Min(0f)] public float walkSpeed = 1.8f;
        [Min(0f)] public float runSpeed = 4.2f;
        [Min(0f)] public float sprintSpeed = 7.2f;
        [Tooltip("Stick magnitude below this walks, above it runs.")]
        [Range(0f, 1f)] public float runThreshold = 0.6f;
        [Range(0f, 1f)] public float guardSpeedMultiplier = 0.55f;
        [Min(0f)] public float sprintStaminaPerSecond = 14f;
        [Tooltip("Degrees per second when turning toward the move direction.")]
        [Min(0f)] public float turnSpeed = 720f;
        [Tooltip("Degrees per second when turning to face a locked-on target.")]
        [Min(0f)] public float lockOnTurnSpeed = 540f;
    }
}
