using Conquest.Animation;
using Conquest.Stats;

namespace Conquest.Character.States
{
    /// <summary>
    /// The nano-injector: the knight pulls a cartridge from his belt, slams it into the port on his
    /// right gauntlet and the nanites repair him. Committed like a Souls flask: no moving, turning or
    /// cancelling until the clip ends. A hit still interrupts it; one before the slam keeps the charge.
    /// </summary>
    public sealed class HealState : TimedState
    {
        private bool _drawn, _injected, _stowed;

        public HealState(ActionCharacter character) : base(character) { }

        private NanoInjector Injector => Character.Injector;

        protected override float Duration => Injector.Duration;

        protected override void OnEnter()
        {
            _drawn = _injected = _stowed = false;
            Character.SetGuarding(false);
            Character.Motor.Stop();
            Character.Animator.Play(AnimatorStates.Heal, 0.1f);
        }

        protected override void OnTick(float dt)
        {
            Character.Motor.SetPlanarVelocityImmediate(UnityEngine.Vector3.zero);
            float t = Progress;

            if (!_drawn && t >= Injector.DrawAt)
            {
                _drawn = true;
                Injector.Draw();
            }
            if (!_injected && t >= Injector.InjectAt)
            {
                _injected = true;
                Injector.Inject();
            }
            if (!_stowed && t >= Injector.StowAt)
            {
                _stowed = true;
                Injector.Stow();
            }

            if (IsFinished) Character.EnterLocomotion();
        }

        protected override void OnExit()
        {
            if (_drawn && !_stowed) Injector.Stow(); // interrupted with the cartridge in hand
        }
    }
}
