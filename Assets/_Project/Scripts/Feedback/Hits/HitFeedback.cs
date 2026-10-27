using Conquest.CameraSystem;
using Conquest.Combat;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Turns the hits a <see cref="CombatReceiver"/> takes into feel: hit-stop on both fighters,
    /// a shudder of the model, camera shake, slow motion, particles, blood, a flash and sound.
    /// What each kind of hit does lives in a shared <see cref="HitFeedbackProfile"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatReceiver))]
    public sealed class HitFeedback : MonoBehaviour
    {
        [SerializeField] private HitFeedbackProfile profile;
        [SerializeField, Tooltip("The model that shudders during hit-stop (a child, not the root).")]
        private Transform visual;
        [SerializeField] private HitFlash flash;
        [SerializeField] private BloodSpray blood;
        [SerializeField, Range(0f, 2f), Tooltip("Scales camera shake caused by hits on this character.")]
        private float cameraInfluence = 1f;

        private CombatReceiver _receiver;
        private IHitStoppable _self;
        private Vector3 _visualRest;
        private Vector3 _shudderDirection;
        private float _shudderAmount;
        private float _shudderDuration;
        private float _shudderRemaining;

        public HitFeedbackProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        private void Awake()
        {
            _receiver = GetComponent<CombatReceiver>();
            _self = GetComponent<IHitStoppable>();
            if (flash == null) flash = GetComponent<HitFlash>();
            if (blood == null) blood = GetComponent<BloodSpray>();
            if (visual != null) _visualRest = visual.localPosition;
        }

        private void OnEnable() => _receiver.HitReceived += OnHitReceived;
        private void OnDisable() => _receiver.HitReceived -= OnHitReceived;

        private void OnHitReceived(HitResult result, DamageInfo hit) => Play(result, hit);

        /// <summary>
        /// Plays the feedback for a hit on this character. Also used for results that never pass
        /// through the receiver, like a gun parry, which this character lands rather than takes.
        /// </summary>
        public void Play(HitResult result, in DamageInfo hit)
        {
            if (profile == null || !profile.TryGet(result, out HitFeedbackEntry entry)) return;

            float scale = HitFeedbackProfile.WeightScale(hit.Weight);
            Vector3 direction = hit.Direction.sqrMagnitude > 1e-4f ? hit.Direction.normalized : -transform.forward;
            Vector3 point = hit.HitPoint != Vector3.zero ? hit.HitPoint : transform.position + Vector3.up * 1.2f;

            Freeze(entry, hit, scale, direction);
            ShakeCamera(entry, scale, direction);
            if (entry.slowMotionDuration > 0f) TimeDilation.Slow(entry.slowMotionScale, entry.slowMotionDuration);

            if (entry.effect != null)
            {
                Vector3 facing = entry.effectTowardAttacker ? -direction : direction;
                VfxPool.Play(entry.effect, point, Quaternion.LookRotation(facing + Vector3.up * 0.3f),
                    entry.effectScale * scale);
            }

            if (blood != null)
            {
                if (entry.bleed) blood.Spray(point, direction, hit.Swing, entry.bloodAmount * hit.Weight);
                if (entry.bloodPool) blood.PoolBeneath();
            }

            if (flash != null && entry.flashDuration > 0f) flash.Flash(entry.flashColor, entry.flashDuration);
            AudioOneShots.PlayRandom(entry.sounds, point, entry.volume, entry.pitch);
        }

        private void Freeze(HitFeedbackEntry entry, in DamageInfo hit, float scale, Vector3 direction)
        {
            float stop = entry.hitStop * scale;
            _self?.ApplyHitStop(stop);

            if (hit.Source != null && hit.Source.TryGetComponent(out IHitStoppable attacker) &&
                !ReferenceEquals(attacker, _self))
                attacker.ApplyHitStop(Mathf.Min(stop * entry.attackerHitStop, profile.maxAttackerHitStop));

            if (visual == null || stop <= 0f || entry.shudder <= 0f) return;
            _shudderDirection = visual.parent != null ? visual.parent.InverseTransformDirection(direction) : direction;
            _shudderAmount = entry.shudder * scale;
            _shudderDuration = _shudderRemaining = stop;
        }

        private void ShakeCamera(HitFeedbackEntry entry, float scale, Vector3 direction)
        {
            float strength = scale * cameraInfluence;
            if (strength <= 0f) return;
            CameraShake.AddTrauma(entry.trauma * strength);
            CameraShake.Kick(direction * (entry.kick * strength));
            if (entry.zoomPunch > 0f) CameraShake.PunchZoom(entry.zoomPunch * strength);
        }

        private void LateUpdate()
        {
            if (visual == null || _shudderDuration <= 0f) return;

            _shudderRemaining -= Time.deltaTime;
            if (_shudderRemaining <= 0f)
            {
                _shudderDuration = 0f;
                visual.localPosition = _visualRest;
                return;
            }

            // A fast back-and-forth along the blow that dies out with the freeze.
            float fade = _shudderRemaining / _shudderDuration;
            float wave = Mathf.Sin(Time.time * 110f);
            visual.localPosition = _visualRest + _shudderDirection * (wave * _shudderAmount * fade);
        }
    }
}
