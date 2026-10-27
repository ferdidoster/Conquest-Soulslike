using System;

namespace Conquest.Core.Utilities
{
    /// <summary>
    /// Remembers the most recent request for a short time so that inputs pressed slightly
    /// too early (e.g. during an attack's recovery) still fire as soon as they are allowed.
    /// </summary>
    public sealed class InputBuffer<T> where T : struct
    {
        private readonly float _lifetime;
        private T? _value;
        private float _timestamp;

        public InputBuffer(float lifetimeSeconds) => _lifetime = lifetimeSeconds;

        public bool HasValue(float now) => _value.HasValue && now - _timestamp <= _lifetime;

        public void Push(T value, float now)
        {
            _value = value;
            _timestamp = now;
        }

        public bool TryPeek(float now, out T value)
        {
            value = _value.GetValueOrDefault();
            return HasValue(now);
        }

        /// <summary>Returns and clears the buffered value if it has not expired.</summary>
        public bool TryConsume(float now, out T value)
        {
            bool valid = TryPeek(now, out value);
            Clear();
            return valid;
        }

        /// <summary>Consumes the buffered value only if <paramref name="filter"/> accepts it.</summary>
        public bool TryConsume(float now, Predicate<T> filter, out T value)
        {
            if (!TryPeek(now, out value) || !filter(value)) return false;
            Clear();
            return true;
        }

        /// <summary>Restarts the lifetime of the buffered value, so a press can wait for its window.</summary>
        public void Refresh(float now)
        {
            if (HasValue(now)) _timestamp = now;
        }

        public void Clear() => _value = null;
    }
}
