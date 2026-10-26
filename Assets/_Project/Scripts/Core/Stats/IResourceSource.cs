namespace Conquest.Core.Stats
{
    /// <summary>Anything that exposes a <see cref="ResourcePool"/> (used by UI bars).</summary>
    public interface IResourceSource
    {
        ResourcePool Pool { get; }
    }
}
