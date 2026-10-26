using System;
using Conquest.Core.Utilities;
using UnityEngine;

namespace Conquest.Character
{
    [Serializable]
    public sealed class ParrySettings
    {
        [Min(0.05f)] public float duration = 0.8f;
        [Tooltip("When incoming hits are parried.")]
        public TimeWindow activeWindow = new TimeWindow(0.08f, 0.32f);
        [Min(0f)] public float staminaCost = 12f;

        [Header("Gun parry (characters with a ParryGun)")]
        [Tooltip("Length of the Shoot animation.")]
        [Min(0.05f)] public float shotDuration = 20f / 30f;
        [Tooltip("When the shot leaves the muzzle (normalized; frame 5 of 20 in the Shoot clip).")]
        [Range(0f, 1f)] public float shotAt = 5f / 20f;
        [Tooltip("After the shot, a dodge can cancel the rest of the animation from this point.")]
        [Range(0f, 1f)] public float shotDodgeCancelFrom = 0.4f;
    }
}
