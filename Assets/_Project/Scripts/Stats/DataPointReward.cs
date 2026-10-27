using UnityEngine;

namespace Conquest.Stats
{
    /// <summary>Data points an enemy is worth: paid to the player's <see cref="DataPoints"/> when it dies.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class DataPointReward : MonoBehaviour
    {
        [SerializeField, Min(0)] private int amount = 50;

        private Health _health;

        public int Amount => amount;

        private void Awake() => _health = GetComponent<Health>();
        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied()
        {
            if (DataPoints.Player != null) DataPoints.Player.Add(amount);
        }
    }
}
