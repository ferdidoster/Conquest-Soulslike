using Conquest.Core.Stats;
using UnityEngine;

namespace Conquest.Stats
{
    /// <summary>
    /// Souls-style stamina: actions may start as long as stamina is above zero (they can
    /// push it to zero), and regeneration pauses briefly after every use.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Stamina : MonoBehaviour, IResourceSource
    {
        [SerializeField, Min(1f)] private float maxStamina = 100f;
        [SerializeField, Min(0f)] private float regenPerSecond = 35f;
        [SerializeField, Min(0f)] private float regenDelay = 0.6f;
        [Tooltip("Regeneration multiplier while the owner reports it is guarding.")]
        [SerializeField, Range(0f, 1f)] private float guardingRegenMultiplier = 0.3f;

        private float _regenBlockedUntil;

        public ResourcePool Pool { get; private set; }

        /// <summary>Set by the owner, e.g. while holding block.</summary>
        public bool IsGuarding { get; set; }

        public bool CanAct => !Pool.IsEmpty;

        private void Awake() => Pool = new ResourcePool(maxStamina);

        private void Update()
        {
            if (Time.time < _regenBlockedUntil || Pool.IsFull) return;

            float rate = IsGuarding ? regenPerSecond * guardingRegenMultiplier : regenPerSecond;
            Pool.Restore(rate * Time.deltaTime);
        }

        /// <summary>Starts an action if any stamina remains, draining the full cost.</summary>
        public bool TrySpend(float cost)
        {
            if (!CanAct) return false;
            Spend(cost);
            return true;
        }

        /// <summary>Back to full, e.g. on respawn.</summary>
        public void Refill() => Pool.Fill();

        /// <summary>Drains unconditionally (sprinting, blocked hits).</summary>
        public void Spend(float amount)
        {
            if (amount <= 0f) return;
            Pool.Drain(amount);
            _regenBlockedUntil = Time.time + regenDelay;
        }
    }
}
