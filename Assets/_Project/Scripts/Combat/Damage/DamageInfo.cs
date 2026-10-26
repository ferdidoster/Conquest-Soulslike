using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>Everything a defender needs to know about an incoming hit.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        /// <summary>Stamina the defender loses if the hit is blocked.</summary>
        public readonly float GuardDamage;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        /// <summary>Direction the hit travels in (from attacker toward defender).</summary>
        public readonly Vector3 Direction;
        public readonly bool CanBeParried;
        /// <summary>How heavy the blow is: 1 = light attack, 2 = heavy. Scales knockback and hit feedback.</summary>
        public readonly float Weight;
        /// <summary>Direction the weapon was moving at the moment of impact (zero if unknown).</summary>
        public readonly Vector3 Swing;

        public DamageInfo(float amount, float guardDamage, GameObject source, Vector3 hitPoint, Vector3 direction,
            bool canBeParried = true, float weight = 1f, Vector3 swing = default)
        {
            Amount = amount;
            GuardDamage = guardDamage;
            Source = source;
            HitPoint = hitPoint;
            Direction = direction;
            CanBeParried = canBeParried;
            Weight = weight;
            Swing = swing;
        }

        public DamageInfo WithImpact(Vector3 hitPoint, Vector3 direction, Vector3 swing) =>
            new DamageInfo(Amount, GuardDamage, Source, hitPoint, direction, CanBeParried, Weight, swing);

        public DamageInfo WithAmount(float amount) =>
            new DamageInfo(amount, GuardDamage, Source, HitPoint, Direction, CanBeParried, Weight, Swing);
    }
}
