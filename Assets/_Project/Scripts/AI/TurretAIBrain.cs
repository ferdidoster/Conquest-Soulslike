using System;
using Conquest.Character;
using Conquest.Character.States;
using Conquest.Combat;
using Conquest.Core.Input;
using Conquest.Core.Utilities;
using Conquest.Targeting;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Conquest.AI
{
    /// <summary>
    /// A stationary gunner. It never walks: from range it charges and fires its <see cref="LaserCannon"/>,
    /// and when a target gets close it deploys its blade and fights in melee until the target backs off.
    /// It drives an <c>ActionCharacter</c> like any other brain; locked onto its target, the character
    /// keeps turning to face it. It only aggroes on targets it can see, and only shoots while it sees them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TurretAIBrain : MonoBehaviour, ICharacterInput, ITargetProvider
    {
        [SerializeField] private LaserCannon cannon;
        [SerializeField] private ActionCharacter character;
        [SerializeField, Tooltip("The melee blade, shown only in melee mode.")]
        private GameObject blade;

        [Header("Awareness")]
        [SerializeField] private float aggroRange = 18f;
        [SerializeField] private float loseAggroRange = 24f;
        [SerializeField, Tooltip("Seconds out of sight before it stops tracking a target.")]
        private float forgetAfter = 2f;
        [SerializeField] private float eyeHeight = 1.6f;

        [Header("Ranged")]
        [SerializeField] private Vector2 shotCooldown = new Vector2(1.6f, 2.8f);

        [Header("Melee")]
        [SerializeField, Tooltip("Closer than this, the blade comes out.")]
        private float meleeRange = 3f;
        [SerializeField, Tooltip("Farther than this, it goes back to shooting.")]
        private float rangedRange = 4.2f;
        [SerializeField] private Vector2 attackCooldown = new Vector2(1.0f, 2.2f);
        [SerializeField, Range(0f, 1f)] private float heavyAttackChance = 0.35f;

        private IHittable _self;
        private bool _melee;
        private float _nextShotAt;
        private float _nextAttackAt;
        private bool _seesTarget;
        private float _lastSeenAt;

        public Vector3 MoveDirection => Vector3.zero;
        public bool IsBlockHeld => false;
        public bool IsSprintHeld => false;
        public bool IsHeld(CharacterAction action) => false;
        public ITargetable CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;

        public event Action<CharacterAction> ActionRequested;

        private void Awake()
        {
            _self = GetComponent<IHittable>();
            if (cannon == null) cannon = GetComponent<LaserCannon>();
            if (character == null) character = GetComponent<ActionCharacter>();
            SetMelee(false);
        }

        private void Update()
        {
            UpdateTarget();
            bool free = character == null || character.CurrentState is LocomotionState;
            if (character != null && character.IsDead) { cannon.Cancel(); return; }

            // Interrupted (staggered, parried...) while charging: the shot is lost.
            if (!free && cannon.IsBusy) cannon.Cancel();
            if (!HasTarget) { cannon.Cancel(); return; }

            float distance = (CurrentTarget.LockPoint.position - transform.position).Flat().magnitude;
            if (!_melee && distance < meleeRange) SetMelee(true);
            else if (_melee && distance > rangedRange && free) SetMelee(false);

            if (!free) return;
            if (!_seesTarget) { cannon.Cancel(); return; }
            if (_melee)
            {
                if (Time.time < _nextAttackAt) return;
                _nextAttackAt = Time.time + Random.Range(attackCooldown.x, attackCooldown.y);
                ActionRequested?.Invoke(Random.value < heavyAttackChance ? CharacterAction.HeavyAttack
                                                                         : CharacterAction.LightAttack);
            }
            else if (!cannon.IsBusy && Time.time >= _nextShotAt)
            {
                cannon.BeginShot(Aim);
                _nextShotAt = Time.time + Random.Range(shotCooldown.x, shotCooldown.y);
            }
        }

        private Vector3 Aim() => HasTarget ? CurrentTarget.LockPoint.position - transform.position : transform.forward;

        private void SetMelee(bool melee)
        {
            _melee = melee;
            if (melee)
            {
                cannon.Cancel();
                // A beat to deploy the blade before the first swing.
                _nextAttackAt = Time.time + 0.4f;
            }
            else
            {
                _nextShotAt = Mathf.Max(_nextShotAt, Time.time + 0.6f);
            }
            if (blade != null) blade.SetActive(melee);
        }

        private void UpdateTarget()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            if (HasTarget && (!CurrentTarget.IsTargetable ||
                              Vector3.Distance(transform.position, CurrentTarget.LockPoint.position) > loseAggroRange))
                CurrentTarget = null;
            if (HasTarget)
            {
                // it never walks, so a target behind a wall is only kept for a moment
                _seesTarget = Sight.CanSee(gameObject, eye, CurrentTarget);
                if (_seesTarget) _lastSeenAt = Time.time;
                else if (Time.time - _lastSeenAt > forgetAfter) CurrentTarget = null;
            }

            if (!HasTarget)
            {
                CurrentTarget = FindHostile(eye);
                _seesTarget = HasTarget;
                _lastSeenAt = Time.time;
            }
        }

        private ITargetable FindHostile(Vector3 eye)
        {
            ITargetable best = null;
            float bestDistance = aggroRange;
            foreach (Targetable candidate in Targetable.All)
            {
                if (!candidate.IsTargetable || candidate.Owner == gameObject) continue;
                if (!candidate.Owner.TryGetComponent(out IHittable hittable)) continue;
                if (_self != null && hittable.Faction == _self.Faction && hittable.Faction != Faction.Neutral) continue;
                float d = Vector3.Distance(transform.position, candidate.LockPoint.position);
                if (d < bestDistance && Sight.CanSee(gameObject, eye, candidate))
                {
                    bestDistance = d;
                    best = candidate;
                }
            }
            return best;
        }
    }
}
