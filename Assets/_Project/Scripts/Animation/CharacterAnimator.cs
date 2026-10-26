using UnityEngine;

namespace Conquest.Animation
{
    /// <summary>
    /// Typed facade over an <see cref="Animator"/>. Gameplay code talks in terms of
    /// "locomotion direction" and "play this action" and never touches parameter strings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("Damping time for blend tree parameters.")]
        private float locomotionDamping = 0.1f;
        [SerializeField, Tooltip("Seconds to blend the guard layer in or out.")]
        private float guardBlendTime = 0.12f;
        [SerializeField] private float defaultCrossFade = 0.1f;

        [Header("Blending")]
        [SerializeField, Min(0.1f), Tooltip("Multiplies every cross-fade the states ask for. Above 1 = softer transitions.")]
        private float crossFadeScale = 1f;
        [SerializeField, Min(0f), Tooltip("No cross-fade is shorter than this (seconds), so quick actions don't snap.")]
        private float minCrossFade;
        [SerializeField, Tooltip("Eases the guard layer in and out instead of blending it linearly.")]
        private bool easeGuardBlend;
        [SerializeField, Tooltip("Returning to locomotion starts the blend tree from standing, instead of from " +
                                 "whatever speed it had before the action (which makes the legs run on the spot).")]
        private bool resetLocomotionOnReturn;

        private int _guardLayer = -1;
        private RuntimeAnimatorController _baseController;
        private float _guardWeight;
        private float _guardTarget;
        private float _playbackSpeed = 1f;
        private bool _frozen;

        public Animator Animator => animator;

        private void Reset() => animator = GetComponentInChildren<Animator>();

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            _guardLayer = animator.GetLayerIndex(AnimatorStates.GuardLayer);
        }

        private void Update()
        {
            if (_guardLayer < 0) return;
            float step = guardBlendTime > 0f ? Time.deltaTime / guardBlendTime : 1f;
            _guardWeight = Mathf.MoveTowards(_guardWeight, _guardTarget, step);
            animator.SetLayerWeight(_guardLayer, easeGuardBlend ? Mathf.SmoothStep(0f, 1f, _guardWeight) : _guardWeight);
        }

        /// <summary>
        /// Drives the 2D locomotion blend tree. <paramref name="localMove"/> is the movement in the
        /// character's local space (x = strafe, y = forward), scaled so walk = 0.5, run = 1, sprint = 1.5.
        /// </summary>
        public void SetLocomotion(Vector2 localMove, float deltaTime)
        {
            animator.SetFloat(AnimatorParameters.MoveX, localMove.x, locomotionDamping, deltaTime);
            animator.SetFloat(AnimatorParameters.MoveY, localMove.y, locomotionDamping, deltaTime);
            animator.SetFloat(AnimatorParameters.Speed, localMove.magnitude, locomotionDamping, deltaTime);
        }

        public void SetLockedOn(bool lockedOn) => animator.SetBool(AnimatorParameters.LockedOn, lockedOn);
        public void SetGrounded(bool grounded) => animator.SetBool(AnimatorParameters.Grounded, grounded);

        /// <summary>Shows the guard pose on the upper body while the legs keep walking.</summary>
        public void SetGuarding(bool guarding) => _guardTarget = guarding ? 1f : 0f;

        /// <summary>Cross-fades the base layer into a named action state.</summary>
        public void Play(string stateName, float crossFade = -1f)
        {
            float fade = crossFade < 0f ? defaultCrossFade : crossFade;
            fade = Mathf.Max(minCrossFade, fade * crossFadeScale);
            animator.CrossFadeInFixedTime(stateName, fade, 0, 0f);
        }

        /// <summary>Freezes the pose in place (hit-stop) or lets it play on.</summary>
        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;
            animator.speed = frozen ? 0f : _playbackSpeed;
        }

        /// <summary>Plays the animation faster or slower (1 = normal); a freeze still wins.</summary>
        public void SetPlaybackSpeed(float speed)
        {
            _playbackSpeed = speed;
            if (!_frozen) animator.speed = speed;
        }

        /// <summary>
        /// Switches the animation set, e.g. to two-handed clips for a greatsword, by assigning an
        /// override of the base controller. Null returns to the base controller's own clips.
        /// </summary>
        public void SetAnimationSet(RuntimeAnimatorController set)
        {
            if (_baseController == null)
            {
                RuntimeAnimatorController current = animator.runtimeAnimatorController;
                _baseController = current is AnimatorOverrideController o ? o.runtimeAnimatorController : current;
            }

            RuntimeAnimatorController target = set != null ? set : _baseController;
            if (target == null || animator.runtimeAnimatorController == target) return;
            animator.runtimeAnimatorController = target;
            _guardLayer = animator.GetLayerIndex(AnimatorStates.GuardLayer);
        }

        /// <summary>Returns to the locomotion blend tree.</summary>
        public void PlayLocomotion(float crossFade = 0.2f)
        {
            if (resetLocomotionOnReturn)
            {
                animator.SetFloat(AnimatorParameters.MoveX, 0f);
                animator.SetFloat(AnimatorParameters.MoveY, 0f);
                animator.SetFloat(AnimatorParameters.Speed, 0f);
            }
            Play(AnimatorStates.Locomotion, crossFade);
        }
    }
}
