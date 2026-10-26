using System;
using Conquest.Combat;
using Conquest.Core.Input;
using Conquest.Core.Utilities;
using Conquest.Targeting;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Conquest.AI
{
    /// <summary>
    /// A simple sparring partner. It produces the same intent a player would (move, block,
    /// attack), so it drives an <c>ActionCharacter</c> exactly like the player input does.
    /// It notices hostiles only when it can see them (or when they hit it), chases them along the
    /// level's NavMesh, hunts the last spot it saw them for a while, then walks back home.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeAIBrain : MonoBehaviour, ICharacterInput, ITargetProvider
    {
        private const float SenseInterval = 0.15f;

        [Header("Awareness")]
        [SerializeField] private float aggroRange = 12f;
        [SerializeField] private float loseAggroRange = 20f;
        [SerializeField, Range(30f, 360f), Tooltip("Field of view for noticing hostiles. Once aggroed it tracks all around.")]
        private float viewAngle = 160f;
        [SerializeField, Tooltip("Closer than this a hostile is noticed even behind its back (still not through walls).")]
        private float senseRange = 3f;
        [SerializeField, Tooltip("Seconds out of sight before it gives up the chase.")]
        private float forgetAfter = 6f;
        [SerializeField] private float eyeHeight = 1.6f;

        [Header("Navigation")]
        [SerializeField, Tooltip("Walk back to where it started after losing its target.")]
        private bool returnHome = true;

        [Header("Spacing")]
        [SerializeField] private float engageRange = 2.4f;
        [SerializeField, Range(0f, 1f)] private float strafeSpeed = 0.35f;

        [Header("Aggression")]
        [SerializeField] private Vector2 attackCooldown = new Vector2(1.2f, 2.8f);
        [SerializeField, Range(1, 3), Tooltip("Most swings in one attack. 1 = single hits, no combos.")]
        private int maxComboLength = 1;
        [SerializeField] private float comboPressInterval = 0.55f;
        [SerializeField, Range(0f, 1f)] private float heavyAttackChance = 0.2f;

        [Header("Defence")]
        [SerializeField, Range(0f, 1f), Tooltip("Chance per decision to raise the guard instead of attacking.")]
        private float blockChance;
        [SerializeField] private Vector2 blockDuration = new Vector2(0.8f, 1.8f);

        private readonly NavPath _path = new NavPath();
        private IHittable _self;
        private CombatReceiver _receiver;
        private ITargetable _target;
        private bool _seesTarget;
        private Vector3 _lastSeen;
        private float _lastSeenAt;
        private float _nextSenseAt;
        private Vector3 _home;
        private Quaternion _homeRotation;
        private float _nextDecisionAt;
        private float _blockUntil;
        private int _pendingComboPresses;
        private float _nextComboPressAt;
        private float _strafeSign = 1f;
        private float _nextStrafeFlipAt;

        public Vector3 MoveDirection { get; private set; }
        public bool IsBlockHeld => Time.time < _blockUntil;
        public bool IsSprintHeld => false;
        public bool IsHeld(CharacterAction action) => false;
        /// <summary>The target the character faces: only one it can see (or is about to hit).</summary>
        public ITargetable CurrentTarget { get; private set; }
        public bool HasTarget => CurrentTarget != null;
        /// <summary>Hunting someone, seen or not.</summary>
        public bool IsAggroed => _target != null;
        public bool SeesTarget => _seesTarget;

        public event Action<CharacterAction> ActionRequested;

        private void Awake()
        {
            _self = GetComponent<IHittable>();
            _receiver = GetComponent<CombatReceiver>();
        }

        private void OnEnable()
        {
            if (_receiver != null) _receiver.HitReceived += OnHit;
        }

        private void OnDisable()
        {
            if (_receiver != null) _receiver.HitReceived -= OnHit;
        }

        private void Start()
        {
            _home = transform.position;
            _homeRotation = transform.rotation;
            LevelNavMesh.Ensure();
        }

        private void Update()
        {
            Sense();

            if (_target == null)
            {
                CurrentTarget = null;
                _blockUntil = 0f;
                _pendingComboPresses = 0;
                // walking pace on the way home, then back on watch the way it started
                MoveDirection = returnHome ? _path.Steer(transform.position, _home, 0.6f) * 0.5f : Vector3.zero;
                if (returnHome && MoveDirection == Vector3.zero)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, _homeRotation, 240f * Time.deltaTime);
                return;
            }

            Vector3 targetPosition = _target.LockPoint.position;
            Vector3 toTarget = (targetPosition - transform.position).Flat();
            float distance = toTarget.magnitude;
            bool engaged = _seesTarget && distance <= engageRange;
            CurrentTarget = _seesTarget ? _target : null;

            if (engaged)
            {
                Vector3 direction = distance > 0.01f ? toTarget / distance : transform.forward;
                MoveDirection = Strafe(direction) * strafeSpeed;
            }
            else
            {
                // straight at it when in view, else to where it was last seen
                MoveDirection = _path.Steer(transform.position, _seesTarget ? targetPosition : _lastSeen,
                    _seesTarget ? engageRange * 0.8f : 0.5f);
            }

            ContinueCombo();
            if (_seesTarget && distance <= engageRange * 1.2f && Time.time >= _nextDecisionAt) Decide();
        }

        private void Sense()
        {
            if (Time.time < _nextSenseAt) return;
            _nextSenseAt = Time.time + SenseInterval;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;

            if (_target != null)
            {
                if (!_target.IsTargetable ||
                    Vector3.Distance(transform.position, _target.LockPoint.position) > loseAggroRange)
                {
                    Forget();
                }
                else
                {
                    _seesTarget = Sight.CanSee(gameObject, eye, _target);
                    if (_seesTarget) Saw(_target);
                    else if (Time.time - _lastSeenAt > forgetAfter) Forget();
                }
            }

            if (_target == null)
            {
                ITargetable found = FindHostile(eye);
                if (found != null) Saw(found);
            }
        }

        private void Saw(ITargetable target)
        {
            if (_target != target) _path.Clear();
            _target = target;
            _seesTarget = true;
            _lastSeen = target.LockPoint.position;
            _lastSeenAt = Time.time;
        }

        private void Forget()
        {
            _target = null;
            _seesTarget = false;
            CurrentTarget = null;
            _path.Clear();
        }

        /// <summary>A hit from behind or out of sight still gives the attacker away.</summary>
        private void OnHit(HitResult result, DamageInfo hit)
        {
            if (hit.Source == null) return;
            Targetable attacker = hit.Source.GetComponentInParent<Targetable>();
            if (attacker == null || !attacker.IsTargetable || attacker.Owner == gameObject || !IsHostile(attacker.Owner))
                return;
            if (_target == null || _target == (ITargetable)attacker)
            {
                Saw(attacker);
                _seesTarget = Sight.CanSee(gameObject, transform.position + Vector3.up * eyeHeight, attacker);
            }
        }

        private ITargetable FindHostile(Vector3 eye)
        {
            ITargetable best = null;
            float bestDistance = aggroRange;

            foreach (Targetable candidate in Targetable.All)
            {
                if (!candidate.IsTargetable || candidate.Owner == gameObject) continue;
                if (!IsHostile(candidate.Owner)) continue;

                Vector3 point = candidate.LockPoint.position;
                float d = Vector3.Distance(transform.position, point);
                if (d >= bestDistance) continue;
                if (d > senseRange && !Sight.InView(transform, point, viewAngle)) continue;
                if (!Sight.CanSee(gameObject, eye, candidate)) continue;

                bestDistance = d;
                best = candidate;
            }

            return best;
        }

        private bool IsHostile(GameObject other)
        {
            if (!other.TryGetComponent(out IHittable hittable)) return false;
            return _self == null || hittable.Faction != _self.Faction || hittable.Faction == Faction.Neutral;
        }

        private Vector3 Strafe(Vector3 toTarget)
        {
            if (Time.time >= _nextStrafeFlipAt)
            {
                _strafeSign = Random.value < 0.5f ? -1f : 1f;
                _nextStrafeFlipAt = Time.time + Random.Range(1.5f, 3.5f);
            }

            return Vector3.Cross(Vector3.up, toTarget) * _strafeSign;
        }

        private void Decide()
        {
            _nextDecisionAt = Time.time + Random.Range(attackCooldown.x, attackCooldown.y);

            if (Random.value < blockChance)
            {
                _blockUntil = Time.time + Random.Range(blockDuration.x, blockDuration.y);
                return;
            }

            _blockUntil = 0f;
            if (Random.value < heavyAttackChance)
            {
                ActionRequested?.Invoke(CharacterAction.HeavyAttack);
                return;
            }

            ActionRequested?.Invoke(CharacterAction.LightAttack);
            _pendingComboPresses = Random.Range(0, maxComboLength);
            _nextComboPressAt = Time.time + comboPressInterval;
        }

        private void ContinueCombo()
        {
            if (_pendingComboPresses <= 0 || Time.time < _nextComboPressAt) return;
            _pendingComboPresses--;
            _nextComboPressAt = Time.time + comboPressInterval;
            ActionRequested?.Invoke(CharacterAction.LightAttack);
        }
    }
}
