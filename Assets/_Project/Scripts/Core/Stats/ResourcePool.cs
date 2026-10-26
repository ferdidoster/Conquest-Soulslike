using System;

namespace Conquest.Core.Stats
{
    /// <summary>
    /// A bounded numeric resource (health, stamina, mana, poise, ...).
    /// Pure C# so it can be shared by any component and tested outside Unity.
    /// </summary>
    [Serializable]
    public sealed class ResourcePool
    {
        public float Current { get; private set; }
        public float Max { get; private set; }

        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsEmpty => Current <= 0f;
        public bool IsFull => Current >= Max;

        /// <summary>Raised with (current, max) whenever the value changes.</summary>
        public event Action<float, float> Changed;

        public ResourcePool(float max, float? current = null)
        {
            Max = Math.Max(0f, max);
            Current = Clamp(current ?? Max);
        }

        public void SetMax(float max, bool refill = false)
        {
            Max = Math.Max(0f, max);
            Set(refill ? Max : Current);
        }

        /// <summary>Removes up to <paramref name="amount"/> and returns how much was actually removed.</summary>
        public float Drain(float amount)
        {
            if (amount <= 0f) return 0f;
            float before = Current;
            Set(Current - amount);
            return before - Current;
        }

        /// <summary>Removes <paramref name="amount"/> only if enough is available.</summary>
        public bool TryConsume(float amount)
        {
            if (amount > Current) return false;
            Drain(amount);
            return true;
        }

        public void Restore(float amount)
        {
            if (amount > 0f) Set(Current + amount);
        }

        public void Fill() => Set(Max);

        private void Set(float value)
        {
            float clamped = Clamp(value);
            if (clamped.Equals(Current)) return;
            Current = clamped;
            Changed?.Invoke(Current, Max);
        }

        private float Clamp(float value) => Math.Min(Math.Max(value, 0f), Max);
    }
}
