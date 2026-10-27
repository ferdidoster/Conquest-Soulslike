using System;

namespace Conquest.Core.Stats
{
    /// <summary>A running total of something the player collects and spends (data points, ...), for HUD counters.</summary>
    public interface ICurrencySource
    {
        int Amount { get; }

        /// <summary>Raised with (amount, change) whenever the amount changes.</summary>
        event Action<int, int> AmountChanged;
    }
}
