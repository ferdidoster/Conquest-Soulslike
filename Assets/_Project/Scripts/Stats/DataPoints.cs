using System;
using Conquest.Core.Stats;
using UnityEngine;

namespace Conquest.Stats
{
    /// <summary>
    /// The player's data points, like souls in Dark Souls: harvested from destroyed enemies (see
    /// <see cref="DataPointReward"/>) and spent later. Dying drops them where the player fell (see the
    /// player's DataPointLoss); this component only counts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DataPoints : MonoBehaviour, ICurrencySource
    {
        [SerializeField, Min(0)] private int startingAmount;

        private int _amount;

        /// <summary>The player's wallet, which enemy rewards go to.</summary>
        public static DataPoints Player { get; private set; }

        public int Amount => _amount;

        public event Action<int, int> AmountChanged;

        private void Awake() => _amount = startingAmount;

        private void OnEnable() => Player = this;

        private void OnDisable()
        {
            if (Player == this) Player = null;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Set(_amount + amount);
        }

        /// <summary>Pays <paramref name="cost"/> if there is enough; false (and nothing spent) otherwise.</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0 || cost > _amount) return false;
            Set(_amount - cost);
            return true;
        }

        /// <summary>Empties the wallet and returns what was in it.</summary>
        public int TakeAll()
        {
            int taken = _amount;
            Set(0);
            return taken;
        }

        private void Set(int amount)
        {
            int change = amount - _amount;
            if (change == 0) return;
            _amount = amount;
            AmountChanged?.Invoke(_amount, change);
        }
    }
}
