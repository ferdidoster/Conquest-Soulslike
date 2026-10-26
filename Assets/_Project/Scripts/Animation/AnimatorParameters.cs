using UnityEngine;

namespace Conquest.Animation
{
    /// <summary>
    /// The contract between code and the Animator Controller. The editor setup tool builds a
    /// controller with exactly these names, so keep both in sync from this single file.
    /// </summary>
    public static class AnimatorParameters
    {
        public const string MoveXName = "MoveX";
        public const string MoveYName = "MoveY";
        public const string SpeedName = "Speed";
        public const string LockedOnName = "LockedOn";
        public const string GroundedName = "Grounded";

        public static readonly int MoveX = Animator.StringToHash(MoveXName);
        public static readonly int MoveY = Animator.StringToHash(MoveYName);
        public static readonly int Speed = Animator.StringToHash(SpeedName);
        public static readonly int LockedOn = Animator.StringToHash(LockedOnName);
        public static readonly int Grounded = Animator.StringToHash(GroundedName);
    }

    /// <summary>Layer and state names used by the generated Animator Controller.</summary>
    public static class AnimatorStates
    {
        public const string BaseLayer = "Base Layer";
        public const string GuardLayer = "Guard";

        public const string Locomotion = "Locomotion";
        public const string GuardIdle = "Guard_Idle";
        public const string GuardEmpty = "Empty";

        public const string RollForward = "Roll_F";
        public const string RollBackward = "Roll_B";
        public const string RollLeft = "Roll_L";
        public const string RollRight = "Roll_R";
        public const string Backstep = "Backstep";

        public const string Parry = "Parry";
        public const string Shoot = "Shoot";
        public const string Jump = "Jump";
        public const string Heal = "Heal";
        public const string BlockHit = "Block_Hit";
        public const string GuardBreak = "Guard_Break";
        public const string HitReact = "Hit_React";
        public const string Stagger = "Stagger";
        public const string Death = "Death";
    }
}
