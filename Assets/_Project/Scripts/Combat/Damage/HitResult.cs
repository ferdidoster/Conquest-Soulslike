namespace Conquest.Combat
{
    public enum HitResult
    {
        Ignored,
        Evaded,
        Damaged,
        Blocked,
        GuardBroken,
        Parried,
        Killed,
    }

    public static class HitResultExtensions
    {
        public static bool Connected(this HitResult result) =>
            result != HitResult.Ignored && result != HitResult.Evaded;
    }
}
