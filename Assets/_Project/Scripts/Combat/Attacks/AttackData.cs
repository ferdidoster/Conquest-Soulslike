using Conquest.Core.Utilities;
using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>
    /// One attack, fully data driven. Timings are normalized (0..1) over <see cref="duration"/>,
    /// so balancing never requires touching code or animation events.
    /// </summary>
    [CreateAssetMenu(menuName = "Conquest/Combat/Attack", fileName = "Attack_")]
    public sealed class AttackData : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("Animator state played on the base layer.")]
        public string animationState = "Attack_1";
        [Min(0f)] public float crossFade = 0.08f;
        [Tooltip("Where in the clip the attack starts (normalized). Above 0 skips part of the wind-up, " +
                 "e.g. for a roll attack that reuses a combo swing.")]
        [Range(0f, 0.9f)] public float startAt;

        [Header("Timing")]
        [Min(0.05f)] public float duration = 0.9f;
        [Tooltip("When the weapon can hit.")]
        public TimeWindow activeWindow = new TimeWindow(0.35f, 0.55f);
        [Tooltip("When pressing attack again queues the next attack of the combo.")]
        public TimeWindow comboWindow = new TimeWindow(0.45f, 0.9f);
        [Tooltip("From this point a dodge can cancel the recovery.")]
        [Range(0f, 1f)] public float dodgeCancelFrom = 0.65f;
        [Tooltip("From this point moving the stick or raising the guard ends the recovery (player only).")]
        [Range(0f, 1f)] public float moveCancelFrom = 0.8f;
        [Tooltip("Seconds after this attack ends (without chaining) before a new swing can start. " +
                 "Attack presses in that time are ignored, so mashing doesn't help.")]
        [Min(0f)] public float cooldown;
        [Tooltip("The attacker may still turn toward the target until this point.")]
        [Range(0f, 1f)] public float trackingUntil = 0.3f;

        [Header("Tempo")]
        [Tooltip("Playback speed over normalized time (1 = the clip's own speed). A slow wind-up, a fast " +
                 "strike and a dragging recovery make a weapon feel heavy. Empty = always 1.")]
        public AnimationCurve tempo = new AnimationCurve();

        [Header("Movement")]
        [Tooltip("Forward speed (m/s) over normalized time. Gives the swing weight.")]
        public AnimationCurve forwardSpeed = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.3f, 3f), new Keyframe(0.5f, 0f), new Keyframe(1f, 0f));

        [Header("Damage")]
        [Min(0f)] public float damage = 20f;
        [Min(0f)] public float guardDamage = 20f;
        [Min(0f)] public float staminaCost = 18f;
        public bool canBeParried = true;
        [Tooltip("Seconds before the hit window opens in which a gun shot already parries (Bloodborne-style).")]
        [Min(0f)] public float parryLead = 0.35f;
        [Tooltip("How heavy the blow feels: 1 = light, 2 = heavy. Scales knockback, hit-stop, shake and blood.")]
        [Min(0.1f)] public float weight = 1f;

        [Header("Charge")]
        [Tooltip("Holding the attack button at chargeAt holds the wind-up and powers up the blow.")]
        public bool chargeable;
        [Range(0f, 1f)] public float chargeAt = 0.4f;
        [Min(0f)] public float maxChargeTime = 1.2f;
        [Tooltip("Damage and guard damage multiplier of a fully charged blow (scales with charge).")]
        [Min(1f)] public float chargedDamageMultiplier = 1.6f;
        [Tooltip("Added to weight by a full charge: more knockback, hit-stop, shake and blood.")]
        [Min(0f)] public float chargedWeightBonus = 0.8f;

        [Header("Combo")]
        [Tooltip("Attack performed if attack is pressed again during the combo window.")]
        public AttackData next;
        [Tooltip("Performed instead of this attack when it is pressed out of a roll or backstep. " +
                 "Only read on the first attack of a chain.")]
        public AttackData rollAttack;
        [Tooltip("Performed instead of this attack when it is pressed in the air. Only read on the first " +
                 "attack of a chain. Empty = the press waits for the landing.")]
        public AttackData jumpAttack;

        /// <summary>Version of the generated defaults; lets the setup tool refresh untouched assets.</summary>
        [HideInInspector] public int defaultsVersion;

        /// <summary>The playback speed at normalized time <paramref name="t"/>.</summary>
        public float TempoAt(float t) => tempo != null && tempo.length > 0 ? Mathf.Max(0.05f, tempo.Evaluate(t)) : 1f;
    }
}
