using System;
using Conquest.Core.Stats;
using UnityEngine;

namespace Conquest.Stats
{
    /// <summary>
    /// The knight's healing item, like an Estus flask: a few glowing cartridges of nanites that he
    /// slams into the port on his gauntlet. Each one restores a share of max health over a moment,
    /// and a checkpoint or respawn refills them all. The character's HealState plays the animation
    /// and calls <see cref="Inject"/> on the slam; this component only counts and heals.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class NanoInjector : MonoBehaviour, IChargeSource
    {
        [SerializeField, Min(1)] private int maxCharges = 4;
        [SerializeField, Range(0f, 1f), Tooltip("Share of max health one cartridge restores.")]
        private float healFraction = 0.45f;
        [SerializeField, Min(0f), Tooltip("Seconds the nanites take to restore it once the cartridge clicks in. " +
                                          "The repair goes on even if the knight is hit after the slam.")]
        private float healTime = 0.9f;

        [Header("Timing (the Heal clip, 48 frames)")]
        [SerializeField, Min(0.1f)] private float duration = 1.6f;
        [SerializeField, Range(0f, 1f), Tooltip("The left hand takes a cartridge from the belt (frame 6).")]
        private float drawAt = 0.125f;
        [SerializeField, Range(0f, 1f), Tooltip("The cartridge is slammed into the port (frame 20). A hit before this keeps the charge.")]
        private float injectAt = 0.42f;
        [SerializeField, Range(0f, 1f), Tooltip("The spent cartridge is tossed away (frame 30).")]
        private float stowAt = 0.625f;

        private Health _health;
        private int _charges;
        private float _healRemaining;
        private float _healRate;

        public int Charges => _charges;
        public int MaxCharges => maxCharges;
        public bool HasCharge => _charges > 0;
        public bool IsRepairing => _healRemaining > 0f;
        public float HealTime => healTime;

        public float Duration => duration;
        public float DrawAt => drawAt;
        public float InjectAt => injectAt;
        public float StowAt => stowAt;

        public event Action<int, int> ChargesChanged;
        /// <summary>A cartridge appears in the hand.</summary>
        public event Action Drawn;
        /// <summary>The cartridge clicked into the port and the nanites are repairing.</summary>
        public event Action Injected;
        /// <summary>The cartridge is gone again (tossed away, or put back after an interruption).</summary>
        public event Action Stowed;
        /// <summary>The heal button was pressed with no cartridges left.</summary>
        public event Action Empty;
        /// <summary>All charges were restored (checkpoint or respawn).</summary>
        public event Action Refilled;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _charges = maxCharges;
        }

        public void Draw() => Drawn?.Invoke();
        public void Stow() => Stowed?.Invoke();
        public void NotifyEmpty() => Empty?.Invoke();

        /// <summary>Spends a cartridge and starts the repair. False when there is none left or the owner is dead.</summary>
        public bool Inject()
        {
            if (!HasCharge || !_health.IsAlive) return false;

            SetCharges(_charges - 1);
            float amount = healFraction * _health.Pool.Max;
            if (healTime <= 0f) _health.Heal(amount);
            else
            {
                _healRemaining += amount;
                _healRate = _healRemaining / healTime;
            }

            Injected?.Invoke();
            return true;
        }

        public void Refill()
        {
            if (_charges == maxCharges) return;
            SetCharges(maxCharges);
            Refilled?.Invoke();
        }

        /// <summary>Stops a repair in progress (on death).</summary>
        public void CancelRepair() => _healRemaining = 0f;

        private void Update()
        {
            if (_healRemaining <= 0f) return;
            if (!_health.IsAlive)
            {
                _healRemaining = 0f;
                return;
            }

            float step = Mathf.Min(_healRemaining, _healRate * Time.deltaTime);
            _healRemaining -= step;
            _health.Heal(step);
        }

        private void SetCharges(int value)
        {
            value = Mathf.Clamp(value, 0, maxCharges);
            if (value == _charges) return;
            _charges = value;
            ChargesChanged?.Invoke(_charges, maxCharges);
        }
    }
}
