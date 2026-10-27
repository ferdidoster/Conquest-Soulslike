using Conquest.Core.StateMachine;

namespace Conquest.Character.States
{
    /// <summary>Base for all character states; gives typed access to the owning character.</summary>
    public abstract class CharacterState : IState
    {
        protected readonly ActionCharacter Character;

        /// <summary>Seconds this state has run, not counting time with <see cref="IsClockPaused"/> set.</summary>
        protected float Elapsed { get; private set; }

        /// <summary>Holds the state's clock, e.g. while an attack is being charged.</summary>
        protected bool IsClockPaused { get; set; }

        /// <summary>How fast the state's clock runs, e.g. to play an attack slower or faster than its clip.</summary>
        protected float ClockScale { get; set; } = 1f;

        protected CharacterState(ActionCharacter character) => Character = character;

        public void Enter()
        {
            Elapsed = 0f;
            IsClockPaused = false;
            ClockScale = 1f;
            OnEnter();
        }

        public void Tick(float deltaTime)
        {
            if (!IsClockPaused) Elapsed += deltaTime * ClockScale;
            OnTick(deltaTime);
        }

        public void Exit() => OnExit();

        /// <summary>Moves the state's clock ahead, e.g. to start an attack part way into its clip.</summary>
        protected void Skip(float seconds) => Elapsed += seconds;

        protected virtual void OnEnter() { }
        protected virtual void OnTick(float deltaTime) { }
        protected virtual void OnExit() { }
    }

    /// <summary>A state with a fixed duration, exposing normalized progress.</summary>
    public abstract class TimedState : CharacterState
    {
        protected TimedState(ActionCharacter character) : base(character) { }

        protected abstract float Duration { get; }
        protected float Progress => Duration > 0f ? UnityEngine.Mathf.Clamp01(Elapsed / Duration) : 1f;
        protected bool IsFinished => Elapsed >= Duration;
    }
}
