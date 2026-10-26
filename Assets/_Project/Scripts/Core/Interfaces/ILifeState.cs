namespace Conquest.Core
{
    /// <summary>Lets systems ask "is this alive?" without depending on a Health implementation.</summary>
    public interface ILifeState
    {
        bool IsAlive { get; }
    }
}
