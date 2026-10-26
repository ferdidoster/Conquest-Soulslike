namespace Conquest.Combat
{
    /// <summary>
    /// Something that can freeze for a moment when a blow lands (hit-stop). The pause sells the
    /// weight of the impact; calls overlap rather than add up.
    /// </summary>
    public interface IHitStoppable
    {
        void ApplyHitStop(float seconds);
    }
}
