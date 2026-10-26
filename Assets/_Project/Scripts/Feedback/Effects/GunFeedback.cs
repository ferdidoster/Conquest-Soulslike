using Conquest.Combat;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Shows and sounds a <see cref="ParryGun"/>'s shots: a muzzle flash, a tracer that flicks out
    /// to whatever the shot hit, and the bang. A shot that parries also plays this character's
    /// Parried hit feedback, so a gun parry lands with the same slow motion and ring as a blade parry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParryGun))]
    public sealed class GunFeedback : MonoBehaviour
    {
        [SerializeField] private ParryGun gun;
        [SerializeField, Tooltip("Optional. Plays the Parried feedback when a shot parries.")]
        private HitFeedback hitFeedback;

        [Header("Muzzle")]
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private float flashScale = 1f;

        [Header("Tracer")]
        [SerializeField, Tooltip("An unlit additive or flat trail material.")]
        private Material tracerMaterial;
        [SerializeField] private Color tracerColor = new Color(1f, 0.85f, 0.55f, 1f);
        [SerializeField] private float tracerWidth = 0.035f;
        [SerializeField, Tooltip("Seconds the tracer takes to fade.")]
        private float tracerTime = 0.09f;

        [Header("Sound")]
        [SerializeField] private AudioClip[] shotSounds;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private Vector2 pitch = new Vector2(0.95f, 1.05f);

        private LineRenderer _tracer;
        private float _tracerAge = float.MaxValue;

        private void Awake()
        {
            if (gun == null) gun = GetComponent<ParryGun>();
            if (hitFeedback == null) hitFeedback = GetComponent<HitFeedback>();

            if (tracerMaterial == null) return;
            // The tracer is in world space, so it lives outside the character's hierarchy.
            var go = new GameObject($"{name} Tracer");
            _tracer = go.AddComponent<LineRenderer>();
            _tracer.sharedMaterial = tracerMaterial;
            _tracer.useWorldSpace = true;
            _tracer.positionCount = 2;
            _tracer.numCapVertices = 2;
            _tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _tracer.receiveShadows = false;
            _tracer.enabled = false;
        }

        private void OnEnable() => gun.Fired += OnFired;
        private void OnDisable() => gun.Fired -= OnFired;

        private void OnDestroy()
        {
            if (_tracer != null) Destroy(_tracer.gameObject);
        }

        private void OnFired(ParryGun.Shot shot)
        {
            Quaternion aim = Quaternion.LookRotation(shot.Direction);
            if (muzzleFlash != null) VfxPool.Play(muzzleFlash, shot.Muzzle, aim, flashScale);
            AudioOneShots.PlayRandom(shotSounds, shot.Muzzle, volume, pitch);

            if (_tracer != null)
            {
                _tracer.SetPosition(0, shot.Muzzle);
                _tracer.SetPosition(1, shot.End);
                _tracerAge = 0f;
                _tracer.enabled = true;
            }

            if (shot.Parried && hitFeedback != null)
                hitFeedback.Play(HitResult.Parried, new DamageInfo(0f, 0f, shot.Target, shot.End, -shot.Direction,
                    canBeParried: false));
        }

        private void LateUpdate()
        {
            if (_tracer == null || !_tracer.enabled) return;

            _tracerAge += Time.unscaledDeltaTime;
            float fade = 1f - _tracerAge / tracerTime;
            if (fade <= 0f)
            {
                _tracer.enabled = false;
                return;
            }

            Color color = tracerColor;
            color.a *= fade;
            _tracer.startColor = _tracer.endColor = color;
            _tracer.startWidth = tracerWidth * fade;
            _tracer.endWidth = tracerWidth * 0.5f * fade;
        }
    }
}
