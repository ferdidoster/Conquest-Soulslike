using System;
using Conquest.Stats;
using UnityEngine;

namespace Conquest.Combat
{
    /// <summary>
    /// Resolves incoming hits against the defender's current stance. The owning controller
    /// only flips the stance flags (invulnerable, guarding, parrying); all rules live here.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class CombatReceiver : MonoBehaviour, IHittable
    {
        [SerializeField] private Faction faction = Faction.Neutral;
        [SerializeField] private DefenseSettings defense = new DefenseSettings();

        private Health _health;
        private Stamina _stamina;

        public Faction Faction => faction;

        /// <summary>I-frames (dodge rolls, cutscenes, ...).</summary>
        public bool IsInvulnerable { get; set; }
        public bool IsGuarding { get; set; }
        /// <summary>True only during the active window of a parry.</summary>
        public bool IsParrying { get; set; }

        /// <summary>Raised for every hit that reached this receiver.</summary>
        public event Action<HitResult, DamageInfo> HitReceived;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _stamina = GetComponent<Stamina>();
        }

        public HitResult ReceiveHit(in DamageInfo hit)
        {
            HitResult result = Resolve(hit);
            if (result != HitResult.Ignored) HitReceived?.Invoke(result, hit);
            return result;
        }

        private HitResult Resolve(in DamageInfo hit)
        {
            if (!_health.IsAlive) return HitResult.Ignored;
            if (IsInvulnerable) return HitResult.Evaded;

            bool fromFront = IsFromFront(hit);

            if (IsParrying && fromFront && hit.CanBeParried)
            {
                if (hit.Source != null && hit.Source.TryGetComponent(out IParryable parryable))
                    parryable.OnParried(gameObject);
                return HitResult.Parried;
            }

            if (IsGuarding && fromFront && _stamina != null)
            {
                _stamina.Spend(hit.GuardDamage * defense.guardStaminaMultiplier);
                if (_stamina.Pool.IsEmpty)
                {
                    _health.TakeDamage(hit.Amount * 0.5f);
                    return _health.IsAlive ? HitResult.GuardBroken : HitResult.Killed;
                }

                _health.TakeDamage(hit.Amount * defense.blockDamageThrough);
                return _health.IsAlive ? HitResult.Blocked : HitResult.Killed;
            }

            _health.TakeDamage(hit.Amount);
            return _health.IsAlive ? HitResult.Damaged : HitResult.Killed;
        }

        private bool IsFromFront(in DamageInfo hit)
        {
            Vector3 toAttacker = -hit.Direction;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 1e-4f) return true;
            return Vector3.Angle(transform.forward, toAttacker) <= defense.guardAngle * 0.5f;
        }
    }
}
