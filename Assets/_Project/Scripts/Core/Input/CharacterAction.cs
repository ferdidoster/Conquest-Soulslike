namespace Conquest.Core.Input
{
    /// <summary>Discrete actions a character can be asked to perform.</summary>
    public enum CharacterAction
    {
        LightAttack,
        HeavyAttack,
        Dodge,
        Parry,
        Jump,
        /// <summary>Use a healing charge (the player's nano-injector).</summary>
        Heal,
    }
}
