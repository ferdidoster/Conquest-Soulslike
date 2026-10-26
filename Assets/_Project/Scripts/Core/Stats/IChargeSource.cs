using System;

namespace Conquest.Core.Stats
{
    /// <summary>A limited number of uses that refill later (healing charges, ...), for HUD counters.</summary>
    public interface IChargeSource
    {
        int Charges { get; }
        int MaxCharges { get; }

        /// <summary>Raised with (charges, max) whenever either changes.</summary>
        event Action<int, int> ChargesChanged;
    }
}
