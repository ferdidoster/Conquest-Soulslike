using System;
using Conquest.Stats;
using UnityEngine;

namespace Conquest.Character
{
    /// <summary>
    /// The Dark Souls rule for the player's <see cref="DataPoints"/>: dying drops all of them in a
    /// <see cref="DataPointCache"/> where the player fell. Reaching it again gets them back; dying
    /// before that loses them for good, and the new death leaves a new cache.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DataPoints), typeof(Health))]
    public sealed class DataPointLoss : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional look for the dropped cache. Empty: a glowing shard and beam are built at runtime.")]
        private GameObject cachePrefab;
        [SerializeField, Min(0.3f)] private float pickupRadius = 1.4f;
        [SerializeField, Tooltip("Layers the cache settles onto below the death spot.")]
        private LayerMask groundMask = 1;

        private DataPoints _wallet;
        private Health _health;
        private DataPointCache _cache;

        /// <summary>The cache waiting where the player last died, if any.</summary>
        public DataPointCache Cache => _cache;

        /// <summary>Raised with the amount dropped on death.</summary>
        public event Action<int> Dropped;
        /// <summary>Raised with the amount picked back up.</summary>
        public event Action<int> Recovered;

        private void Awake()
        {
            _wallet = GetComponent<DataPoints>();
            _health = GetComponent<Health>();
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied()
        {
            // An unclaimed cache from the previous death is lost.
            if (_cache != null) Destroy(_cache.gameObject);
            _cache = null;

            int amount = _wallet.TakeAll();
            if (amount <= 0) return;

            _cache = DataPointCache.Create(GroundBelow(transform.position), amount, this, cachePrefab, pickupRadius);
            Dropped?.Invoke(amount);
        }

        /// <summary>Called by the cache when the living player reaches it.</summary>
        internal void Recover(DataPointCache cache)
        {
            if (cache != _cache) return;
            _cache = null;
            _wallet.Add(cache.Amount);
            Recovered?.Invoke(cache.Amount);
        }

        internal bool CanRecover => _health.IsAlive;

        private Vector3 GroundBelow(Vector3 position)
        {
            return Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6f, groundMask,
                QueryTriggerInteraction.Ignore)
                ? hit.point
                : position;
        }
    }
}
