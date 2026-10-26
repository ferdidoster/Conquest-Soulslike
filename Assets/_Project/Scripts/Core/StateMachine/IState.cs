namespace Conquest.Core.StateMachine
{
    /// <summary>
    /// A single state in a <see cref="StateMachine"/>. States are plain C# objects so they
    /// can be unit tested and reused by any owner (player, AI, UI flow, ...).
    /// </summary>
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
