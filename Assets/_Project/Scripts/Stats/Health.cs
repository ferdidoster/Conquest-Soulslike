using System;
using Conquest.Core;
using Conquest.Core.Stats;
using UnityEngine;

namespace Conquest.Stats
{
    /// <summary>Hit points. Knows nothing about combat rules; it only goes up, down, and dies.</summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IResourceSource, ILifeState
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public ResourcePool Pool { get; private set; }
        public bool IsAlive => Pool != null && !Pool.IsEmpty;

        /// <summary>Raised with the amount of damage actually taken.</summary>
        public event Action<float> Damaged;
        public event Action Died;

        private void Awake() => Pool = new ResourcePool(maxHealth);

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            float taken = Pool.Drain(amount);
            Damaged?.Invoke(taken);

            if (Pool.IsEmpty) Died?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsAlive) Pool.Restore(amount);
        }

        public void Revive()
        {
            Pool.Fill();
        }
    }
}
