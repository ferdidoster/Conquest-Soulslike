using System;
using UnityEngine;

namespace Conquest.Core.Input
{
    /// <summary>
    /// The intent of whoever drives a character. A player input reader and an AI brain both
    /// implement this, so the same character controller works for players and enemies.
    /// </summary>
    public interface ICharacterInput
    {
        /// <summary>Desired movement on the ground plane, in world space. Magnitude 0..1.</summary>
        Vector3 MoveDirection { get; }

        bool IsBlockHeld { get; }
        bool IsSprintHeld { get; }

        /// <summary>Whether the button behind an action is still held, e.g. to charge a heavy attack.</summary>
        bool IsHeld(CharacterAction action);

        /// <summary>Raised when a discrete action is requested.</summary>
        event Action<CharacterAction> ActionRequested;
    }
}
