using Conquest.Combat;
using Conquest.Core.Input;
using UnityEngine;

namespace Conquest.Character.States
{
    /// <summary>
    /// Plays one <see cref="AttackData"/> and chains into the next attack of the combo.
    /// A chargeable attack holds its wind-up while its button stays held, and hits harder for it.
    /// </summary>
    public sealed class AttackState : TimedState
    {
        private AttackData _attack;
        private CharacterAction _button;
        private bool _windowOpened;
        private bool _chargeDone;
        private float _chargeTime;

        public AttackState(ActionCharacter character) : base(character) { }

        protected override float Duration => _attack.duration;

        public AttackState With(AttackData attack, CharacterAction button)
        {
            _attack = attack;
            _button = button;
            return this;
        }

        /// <summary>
        /// From <see cref="AttackData.parryLead"/> seconds before the hit window until it closes, a gun
        /// shot parries this attack.
        /// </summary>
        public bool IsParryable
        {
            get
            {
                if (_attack == null || !_attack.canBeParried) return false;
                float lead = _attack.duration > 0f ? _attack.parryLead / _attack.duration : 0f;
                float t = Progress;
                return t >= _attack.activeWindow.start - lead && t <= _attack.activeWindow.end;
            }
        }

        /// <summary>0 = uncharged, 1 = fully charged.</summary>
        private float Charge => _attack.maxChargeTime > 0f ? Mathf.Clamp01(_chargeTime / _attack.maxChargeTime) : 0f;

        protected override void OnEnter()
        {
            _windowOpened = false;
            _chargeDone = !_attack.chargeable;
            _chargeTime = 0f;
            Character.Stamina.Spend(_attack.staminaCost);
            Character.FaceIntendedDirection();
            float offset = _attack.startAt * _attack.duration;
            Skip(offset);
            Character.Animator.Play(_attack.animationState, _attack.crossFade, offset);
        }

        protected override void OnTick(float dt)
        {
            float t = Progress;
            if (TickCharge(t, dt)) return;

            float tempo = _attack.TempoAt(t);
            ClockScale = tempo;
            Character.Animator.SetPlaybackSpeed(tempo);

            if (t <= _attack.trackingUntil)
                Character.Motor.TurnTowards(Character.IntendedFacing(), dt, Character.Locomotion.lockOnTurnSpeed);

            // forwardSpeed is in clip time, so scale it with the tempo to keep each step's distance.
            Character.Motor.SetPlanarVelocityImmediate(Character.transform.forward * (_attack.forwardSpeed.Evaluate(t) * tempo));
            UpdateHitWindow(t);
            // From the strike on, a press waits for its window instead of expiring before it opens.
            if (t >= _attack.activeWindow.start) Character.HoldBufferedAction();

            if (_attack.comboWindow.Contains(t) && TryChain()) return;
            if (t >= _attack.dodgeCancelFrom && Character.TryStartBufferedAction(a => a == CharacterAction.Dodge)) return;
            if (t >= _attack.moveCancelFrom && Character.WantsToBreakRecovery)
            {
                Character.EnterLocomotion();
                return;
            }
            // With a cooldown, a press left in the buffer can't start a fresh swing straight away.
            if (IsFinished && !Character.TryStartBufferedAction(a => _attack.cooldown <= 0f || !IsAttack(a)))
                Character.EnterLocomotion();
        }

        protected override void OnExit()
        {
            if (IsClockPaused) Character.Animator.SetFrozen(false);
            Character.Animator.SetPlaybackSpeed(1f);
            Character.LockAttacks(_attack.cooldown);
            Character.HitDetector?.Close();
        }

        /// <summary>Holds the wind-up pose while the button is held. Returns true while charging.</summary>
        private bool TickCharge(float t, float dt)
        {
            if (_chargeDone || t < _attack.chargeAt) return false;

            bool holding = Character.Input != null && Character.Input.IsHeld(_button) &&
                           _chargeTime < _attack.maxChargeTime;
            if (holding)
            {
                if (!IsClockPaused)
                {
                    IsClockPaused = true;
                    Character.Animator.SetFrozen(true);
                }
                _chargeTime += dt;
                // Rolling out of a charge is allowed; OnExit lets the animator run again.
                if (Character.TryStartBufferedAction(a => a == CharacterAction.Dodge)) return true;
                Character.Motor.SetPlanarVelocityImmediate(Vector3.zero);
                Character.Motor.TurnTowards(Character.IntendedFacing(), dt, Character.Locomotion.lockOnTurnSpeed);
                return true;
            }

            _chargeDone = true;
            if (IsClockPaused)
            {
                IsClockPaused = false;
                Character.Animator.SetFrozen(false);
            }
            return false;
        }

        private void UpdateHitWindow(float t)
        {
            if (Character.HitDetector == null) return;

            if (!_windowOpened && _attack.activeWindow.Contains(t))
            {
                _windowOpened = true;
                float power = Mathf.Lerp(1f, _attack.chargedDamageMultiplier, Charge);
                Character.HitDetector.Open(new DamageInfo(_attack.damage * power, _attack.guardDamage * power,
                    Character.gameObject, Vector3.zero, Character.transform.forward, _attack.canBeParried,
                    _attack.weight + _attack.chargedWeightBonus * Charge));
            }
            else if (_windowOpened && _attack.activeWindow.HasEnded(t))
            {
                Character.HitDetector.Close();
            }
        }

        private bool TryChain() => Character.TryStartBufferedAction(IsAttack, _attack);

        private static bool IsAttack(CharacterAction action) =>
            action == CharacterAction.LightAttack || action == CharacterAction.HeavyAttack;
    }
}
