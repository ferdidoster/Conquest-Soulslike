using System;
using Conquest.Animation;
using Conquest.Character.States;
using Conquest.Combat;
using Conquest.Core.Input;
using Conquest.Core.StateMachine;
using Conquest.Core.Utilities;
using Conquest.Movement;
using Conquest.Stats;
using Conquest.Targeting;
using UnityEngine;

namespace Conquest.Character
{
    /// <summary>
    /// Composition root for a souls-like melee character. It wires the reusable components
    /// together and owns the state machine. Who drives it is decided by the
    /// <see cref="ICharacterInput"/> (player reader or AI brain), so players and enemies share
    /// exactly the same code.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterMotor), typeof(CombatReceiver), typeof(Stamina))]
    public sealed class ActionCharacter : MonoBehaviour, IParryable, IHitStoppable
    {
        [Header("Drivers")]
        [SerializeField, Tooltip("A component implementing ICharacterInput (player reader or AI).")]
        private MonoBehaviour inputSource;
        [SerializeField, Tooltip("A component implementing ITargetProvider (lock-on or AI aggro). Optional.")]
        private MonoBehaviour targetSource;

        [Header("Components")]
        [SerializeField] private CharacterAnimator characterAnimator;
        [SerializeField] private MeleeHitDetector hitDetector;

        [Header("Moveset")]
        [SerializeField] private AttackData lightAttack;
        [SerializeField] private AttackData heavyAttack;

        [Header("Tuning")]
        [SerializeField] private LocomotionSettings locomotion = new LocomotionSettings();
        [SerializeField] private DodgeSettings dodge = new DodgeSettings();
        [SerializeField] private ParrySettings parry = new ParrySettings();
        [SerializeField] private JumpSettings jump = new JumpSettings();
        [SerializeField] private ReactionSettings reactions = new ReactionSettings();
        [SerializeField, Tooltip("Seconds an early button press is remembered.")]
        private float inputBufferTime = 0.35f;
        [SerializeField, Tooltip("Moving the stick (or raising the guard) breaks out of the end of an attack or " +
                                 "roll, from its moveCancelFrom point. Off for AI, which always holds a direction.")]
        private bool moveCancelsRecovery;

        private readonly StateMachine _stateMachine = new StateMachine();
        private InputBuffer<CharacterAction> _buffer;
        private ICharacterInput _input;
        private WeaponLoadout _loadout;
        private ParryGun _gun;
        private NanoInjector _injector;
        private AttackData _defaultLight, _defaultHeavy;
        private ITargetProvider _targets;
        private Health _health;
        private float _attacksLockedUntil;

        private LocomotionState _locomotionState;
        private AttackState _attackState;
        private DodgeState _dodgeState;
        private ParryState _parryState;
        private ShootState _shootState;
        private JumpState _jumpState;
        private HealState _healState;
        private ReactionState _reactionState;
        private DeadState _deadState;
        private float _hitStopRemaining;
        // Time the input buffer runs on: it stands still during hit-stop, so a press made on impact survives the freeze.
        private float _inputClock;

        public ICharacterInput Input => _input;
        public CharacterMotor Motor { get; private set; }
        public CharacterAnimator Animator => characterAnimator;
        public Stamina Stamina { get; private set; }
        public CombatReceiver Receiver { get; private set; }
        public MeleeHitDetector HitDetector => hitDetector;

        public LocomotionSettings Locomotion => locomotion;
        public DodgeSettings Dodge => dodge;
        public ParrySettings Parry => parry;
        public JumpSettings Jump => jump;
        /// <summary>Optional sidearm: with one, the parry button fires it instead of deflecting.</summary>
        public ParryGun Gun => _gun;
        /// <summary>Optional healing item (the player's nano-injector): the heal action uses it.</summary>
        public NanoInjector Injector => _injector;

        public bool HasTarget => _targets != null && _targets.HasTarget;
        public ITargetable Target => HasTarget ? _targets.CurrentTarget : null;
        public IState CurrentState => _stateMachine.Current;
        public bool IsDead => _stateMachine.IsIn(_deadState);
        public bool IsInHitStop => _hitStopRemaining > 0f;
        public bool IsParryableNow => !IsDead && _stateMachine.IsIn(_attackState) && _attackState.IsParryable;

        /// <summary>True when the player asks to move or guard, which may cut an action's recovery short.</summary>
        public bool WantsToBreakRecovery =>
            moveCancelsRecovery && _input != null && (_input.MoveDirection.sqrMagnitude > 0.04f || _input.IsBlockHeld);

        public event Action<IState> StateChanged;

        private void Awake()
        {
            Motor = GetComponent<CharacterMotor>();
            Stamina = GetComponent<Stamina>();
            Receiver = GetComponent<CombatReceiver>();
            _health = GetComponent<Health>();
            if (characterAnimator == null) characterAnimator = GetComponentInChildren<CharacterAnimator>();
            if (hitDetector == null) hitDetector = GetComponentInChildren<MeleeHitDetector>();
            _loadout = GetComponent<WeaponLoadout>();
            _gun = GetComponent<ParryGun>();
            _injector = GetComponent<NanoInjector>();

            _buffer = new InputBuffer<CharacterAction>(inputBufferTime);
            SetInput(inputSource as ICharacterInput);
            SetTargetProvider(targetSource as ITargetProvider);

            _locomotionState = new LocomotionState(this);
            _attackState = new AttackState(this);
            _dodgeState = new DodgeState(this);
            _parryState = new ParryState(this);
            _shootState = new ShootState(this);
            _jumpState = new JumpState(this);
            _healState = new HealState(this);
            _reactionState = new ReactionState(this);
            _deadState = new DeadState(this);

            _stateMachine.StateChanged += (_, next) => StateChanged?.Invoke(next);

            if (_loadout != null)
            {
                _defaultLight = lightAttack;
                _defaultHeavy = heavyAttack;
                _loadout.Equipped += OnWeaponEquipped;
                if (_loadout.Current != null) OnWeaponEquipped(_loadout.Current);
            }
        }

        private void OnEnable()
        {
            Receiver.HitReceived += OnHitReceived;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            Receiver.HitReceived -= OnHitReceived;
            _health.Died -= OnDied;
        }

        private void OnDestroy()
        {
            SetInput(null);
            if (_loadout != null) _loadout.Equipped -= OnWeaponEquipped;
        }

        private void Start()
        {
            if (_input == null)
                Debug.LogError($"{name}: ActionCharacter needs an input source implementing ICharacterInput.", this);
            _stateMachine.ChangeState(_locomotionState);
        }

        private void Update()
        {
            if (TickHitStop(Time.deltaTime)) return;
            _inputClock += Time.deltaTime;
            Animator.SetGrounded(Motor.IsGrounded);
            _stateMachine.Tick(Time.deltaTime);
        }

        // ---------- Hit-stop ----------

        /// <summary>Freezes animation, movement and state timers for a moment when a blow lands.</summary>
        public void ApplyHitStop(float seconds)
        {
            if (seconds <= 0f) return;
            _hitStopRemaining = Mathf.Max(_hitStopRemaining, seconds);
            Animator.SetFrozen(true);
            Motor.IsPaused = true;
        }

        /// <summary>Returns true while frozen.</summary>
        private bool TickHitStop(float dt)
        {
            if (_hitStopRemaining <= 0f) return false;
            _hitStopRemaining -= dt;
            if (_hitStopRemaining > 0f) return true;

            _hitStopRemaining = 0f;
            Animator.SetFrozen(false);
            Motor.IsPaused = false;
            return false;
        }

        // ---------- Wiring ----------

        public void SetInput(ICharacterInput input)
        {
            if (_input != null) _input.ActionRequested -= OnActionRequested;
            _input = input;
            if (_input != null) _input.ActionRequested += OnActionRequested;
        }

        public void SetTargetProvider(ITargetProvider provider) => _targets = provider;

        public void SetMoveset(AttackData light, AttackData heavy)
        {
            lightAttack = light;
            heavyAttack = heavy;
        }

        /// <summary>Takes on the weapon's animation set and moveset, or the character's own where it has none.</summary>
        private void OnWeaponEquipped(WeaponLoadout.Weapon weapon)
        {
            characterAnimator.SetAnimationSet(weapon.animations);
            SetMoveset(weapon.lightAttack != null ? weapon.lightAttack : _defaultLight,
                weapon.heavyAttack != null ? weapon.heavyAttack : _defaultHeavy);
        }

        private void OnActionRequested(CharacterAction action) => _buffer.Push(action, _inputClock);

        /// <summary>Keeps a pending press from expiring, e.g. one made mid-swing that waits for the combo window.</summary>
        public void HoldBufferedAction() => _buffer.Refresh(_inputClock);

        // ---------- Helpers used by states ----------

        public void EnterLocomotion() => _stateMachine.ChangeState(_locomotionState);

        /// <summary>Blocks starting a new swing for <paramref name="seconds"/> (an attack's cooldown).</summary>
        public void LockAttacks(float seconds)
        {
            if (seconds > 0f) _attacksLockedUntil = Mathf.Max(_attacksLockedUntil, Time.time + seconds);
        }

        private bool IsAttackLocked(CharacterAction action) =>
            Time.time < _attacksLockedUntil &&
            (action == CharacterAction.LightAttack || action == CharacterAction.HeavyAttack);

        public void SetGuarding(bool guarding)
        {
            Receiver.IsGuarding = guarding;
            Stamina.IsGuarding = guarding;
            Animator.SetGuarding(guarding);
        }

        public Vector3 DirectionToTarget() =>
            HasTarget ? (Target.LockPoint.position - transform.position).FlatNormalized() : transform.forward;

        /// <summary>Where an action should face: the target, else the stick, else straight ahead.</summary>
        public Vector3 IntendedFacing()
        {
            if (HasTarget) return DirectionToTarget();
            Vector3 move = _input != null ? _input.MoveDirection.FlatNormalized() : Vector3.zero;
            return move != Vector3.zero ? move : transform.forward;
        }

        public void FaceIntendedDirection() => Motor.FaceImmediately(IntendedFacing());

        /// <summary>
        /// Starts the buffered action if there is one, it passes <paramref name="filter"/>, and
        /// stamina allows it. <paramref name="currentAttack"/> lets attacks continue their combo.
        /// </summary>
        public bool TryStartBufferedAction(Predicate<CharacterAction> filter = null, AttackData currentAttack = null)
        {
            if (!_buffer.TryPeek(_inputClock, out CharacterAction action)) return false;
            if (filter != null && !filter(action)) return false;
            if (currentAttack == null && IsAttackLocked(action))
            {
                HoldBufferedAction(); // a press during the cooldown swings as soon as it ends
                return false;
            }
            if (action == CharacterAction.Heal && (_injector == null || !_injector.HasCharge))
            {
                _buffer.Clear(); // an empty injector only clicks; the press is spent
                if (_injector != null) _injector.NotifyEmpty();
                return false;
            }
            if (!Stamina.CanAct && action != CharacterAction.Heal) return false; // healing costs no stamina

            IState next = Resolve(action, currentAttack);
            if (next == null) return false;

            _buffer.Clear();
            _stateMachine.ChangeState(next);
            return true;
        }

        private IState Resolve(CharacterAction action, AttackData currentAttack)
        {
            switch (action)
            {
                case CharacterAction.LightAttack:
                case CharacterAction.HeavyAttack:
                    AttackData chainStart = action == CharacterAction.LightAttack ? lightAttack : heavyAttack;
                    if (currentAttack == null && chainStart != null)
                    {
                        // Out of a roll or backstep the attack becomes its roll attack; in the air, its jump
                        // attack (without one the press waits for the landing).
                        if (_stateMachine.IsIn(_dodgeState) && chainStart.rollAttack != null)
                            return Attack(chainStart.rollAttack, action);
                        if (_stateMachine.IsIn(_jumpState))
                            return chainStart.jumpAttack != null ? Attack(chainStart.jumpAttack, action) : null;
                    }
                    return Attack(NextInCombo(currentAttack, chainStart), action);
                case CharacterAction.Dodge:
                    return _dodgeState;
                case CharacterAction.Parry:
                    return _gun != null ? _shootState : _parryState;
                case CharacterAction.Jump:
                    return Motor.IsGrounded ? _jumpState : null;
                case CharacterAction.Heal:
                    return Motor.IsGrounded ? _healState : null;
                default:
                    return null;
            }
        }

        /// <summary>Continues the current chain if the same kind of attack is pressed again.</summary>
        private static AttackData NextInCombo(AttackData current, AttackData chainStart)
        {
            if (current == null || chainStart == null) return chainStart;
            return IsSameChain(current, chainStart) && current.next != null ? current.next : chainStart;
        }

        private static bool IsSameChain(AttackData attack, AttackData chainStart)
        {
            // Walk the chain (guarding against loops) to see whether the attack belongs to it.
            AttackData step = chainStart;
            for (int i = 0; i < 16 && step != null; i++, step = step.next)
                if (step == attack) return true;
            return false;
        }

        private IState Attack(AttackData data, CharacterAction button) =>
            data != null ? _attackState.With(data, button) : null;

        // ---------- Reactions ----------

        private void OnHitReceived(HitResult result, DamageInfo hit)
        {
            switch (result)
            {
                case HitResult.Damaged:
                    if (hit.Weight < reactions.flinchWeight) break;
                    React(reactions.ForHit(hit.Weight), hit.Direction, PushScale(hit.Weight));
                    break;
                case HitResult.Blocked:
                    React(reactions.blocked, hit.Direction, PushScale(hit.Weight));
                    break;
                case HitResult.GuardBroken:
                    React(reactions.guardBreak, hit.Direction, PushScale(hit.Weight));
                    break;
                case HitResult.Killed:
                    _deadState.Shove(hit.Direction.FlatNormalized() * (reactions.hit.knockback * PushScale(hit.Weight)));
                    break;
            }
        }

        public void OnParried(GameObject parrier)
        {
            if (IsDead) return;
            Vector3 away = (transform.position - parrier.transform.position).FlatNormalized();
            React(reactions.parried, away);
        }

        private static float PushScale(float weight) => Mathf.Lerp(1f, weight, 0.5f);

        private void React(Reaction reaction, Vector3 pushDirection, float pushScale = 1f)
        {
            if (IsDead) return;
            _buffer.Clear();
            _stateMachine.ChangeState(_reactionState.With(reaction, pushDirection.FlatNormalized(), pushScale));
        }

        /// <summary>Brings the character back at a checkpoint with full health and stamina (see PlayerRespawn).</summary>
        public void Respawn(Vector3 position, Quaternion rotation)
        {
            _buffer.Clear();
            _hitStopRemaining = 0f;
            Animator.SetFrozen(false);
            Motor.IsPaused = false;
            Motor.Teleport(position, rotation);
            _health.Revive();
            Stamina.Refill();
            _stateMachine.ChangeState(_locomotionState);
        }

        private void OnDied()
        {
            _buffer.Clear();
            _stateMachine.ChangeState(_deadState);
        }
    }
}
