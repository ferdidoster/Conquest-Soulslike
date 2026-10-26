using System;

namespace Conquest.Core.StateMachine
{
    /// <summary>
    /// Minimal, allocation-free finite state machine. Owners decide when to tick it.
    /// </summary>
    public sealed class StateMachine
    {
        public IState Current { get; private set; }
        public IState Previous { get; private set; }

        /// <summary>Raised after a transition with (previous, next).</summary>
        public event Action<IState, IState> StateChanged;

        public void ChangeState(IState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));

            Current?.Exit();
            Previous = Current;
            Current = next;
            Current.Enter();

            StateChanged?.Invoke(Previous, Current);
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);

        public bool IsIn(IState state) => ReferenceEquals(Current, state);
    }
}
