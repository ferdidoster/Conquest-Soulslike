using UnityEngine;

namespace Conquest.Targeting
{
    /// <summary>Something that can be locked on to.</summary>
    public interface ITargetable
    {
        /// <summary>The point the camera and reticle aim at (usually the chest).</summary>
        Transform LockPoint { get; }
        bool IsTargetable { get; }
        GameObject Owner { get; }
    }

    /// <summary>Provides the current focus target of a character (lock-on for players, aggro for AI).</summary>
    public interface ITargetProvider
    {
        ITargetable CurrentTarget { get; }
        bool HasTarget { get; }
    }
}
