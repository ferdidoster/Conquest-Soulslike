namespace Conquest.Combat
{
    /// <summary>Receives hits and decides what happens (damage, block, parry, ...).</summary>
    public interface IHittable
    {
        Faction Faction { get; }
        HitResult ReceiveHit(in DamageInfo hit);
    }

    /// <summary>Implemented by attackers that react to being parried (usually a stagger).</summary>
    public interface IParryable
    {
        /// <summary>
        /// True while a ranged parry (a gun shot) would interrupt this attacker: from shortly before its
        /// swing starts cutting until the swing ends.
        /// </summary>
        bool IsParryableNow { get; }

        void OnParried(UnityEngine.GameObject parrier);
    }
}
