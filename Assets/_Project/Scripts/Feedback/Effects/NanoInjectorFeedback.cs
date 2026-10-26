using Conquest.Stats;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Shows and sounds the <see cref="NanoInjector"/>: the glowing cartridge in the left hand, a blue
    /// burst, light and body flash where it slams into the gauntlet port, nanite motes rising over the
    /// body while they repair, a dry click when the injector is empty and a chime when it is refilled.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NanoInjector))]
    public sealed class NanoInjectorFeedback : MonoBehaviour
    {
        [SerializeField] private NanoInjector injector;
        [SerializeField, Tooltip("Optional. Tints the whole model blue on the slam.")]
        private HitFlash flash;

        [Header("Model")]
        [SerializeField, Tooltip("The cartridge mesh in the left hand (Injector_Hand), shown while it is held.")]
        private GameObject cartridge;
        [SerializeField, Tooltip("The port on the right gauntlet (Injector_Port socket).")]
        private Transform port;
        [SerializeField, Tooltip("The cartridge's needle end (Injector_Tip socket).")]
        private Transform tip;

        [Header("Effects")]
        [SerializeField] private Color naniteColor = new Color(0.25f, 0.62f, 1f);
        [SerializeField, Tooltip("One-shot burst at the port on the slam.")]
        private ParticleSystem slamBurst;
        [SerializeField, Tooltip("One-shot puff at the hand when the spent cartridge is tossed.")]
        private ParticleSystem tossPuff;
        [SerializeField, Tooltip("Looping motes around the body while the nanites repair (a child of this character).")]
        private ParticleSystem repairMotes;
        [SerializeField, Tooltip("The body flash on the slam: paler than the nanites so the model still reads.")]
        private Color flashColor = new Color(0.6f, 0.82f, 1f);
        [SerializeField] private float flashDuration = 0.2f;
        [SerializeField] private float lightRange = 3.5f;
        [SerializeField] private float lightIntensity = 6f;

        [Header("Sound")]
        [SerializeField] private AudioClip drawSound;
        [SerializeField] private AudioClip slamSound;
        [SerializeField] private AudioClip repairSound;
        [SerializeField] private AudioClip emptySound;
        [SerializeField] private AudioClip refillSound;
        [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

        private Light _light;
        private float _lightAmount;
        private float _repairEndsAt = -1f;

        private Vector3 PortPosition => port != null ? port.position : transform.position + Vector3.up * 1.2f;
        private Vector3 TipPosition => tip != null ? tip.position : PortPosition;

        private void Awake()
        {
            if (injector == null) injector = GetComponent<NanoInjector>();
            if (flash == null) flash = GetComponent<HitFlash>();
            if (cartridge != null) cartridge.SetActive(false);

            var go = new GameObject("Nanite Light");
            go.transform.SetParent(port != null ? port : transform, false);
            if (port == null) go.transform.localPosition = Vector3.up * 1.2f;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = naniteColor;
            _light.range = lightRange;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
        }

        private void OnEnable()
        {
            injector.Drawn += OnDrawn;
            injector.Injected += OnInjected;
            injector.Stowed += OnStowed;
            injector.Empty += OnEmpty;
            injector.Refilled += OnRefilled;
        }

        private void OnDisable()
        {
            injector.Drawn -= OnDrawn;
            injector.Injected -= OnInjected;
            injector.Stowed -= OnStowed;
            injector.Empty -= OnEmpty;
            injector.Refilled -= OnRefilled;
        }

        private void OnDrawn()
        {
            if (cartridge != null) cartridge.SetActive(true);
            AudioOneShots.Play(drawSound, TipPosition, volume * 0.7f, Random.Range(0.96f, 1.04f));
        }

        private void OnInjected()
        {
            Vector3 at = PortPosition;
            // The port faces up when the cartridge goes in, so the sparks burst upward.
            if (slamBurst != null) VfxPool.Play(slamBurst, at, Quaternion.LookRotation(Vector3.up, transform.forward));
            if (flash != null) flash.Flash(flashColor, flashDuration);
            AudioOneShots.Play(slamSound, at, volume, Random.Range(0.97f, 1.03f));
            AudioOneShots.Play(repairSound, at, volume * 0.8f);

            _lightAmount = 1f;
            _repairEndsAt = Time.time + Mathf.Max(0.2f, injector.HealTime);
            if (repairMotes != null) repairMotes.Play(true);
        }

        private void OnStowed()
        {
            bool wasHeld = cartridge == null || cartridge.activeSelf;
            if (cartridge != null) cartridge.SetActive(false);
            if (wasHeld && tossPuff != null) VfxPool.Play(tossPuff, TipPosition, Quaternion.identity);
        }

        private void OnEmpty() => AudioOneShots.Play(emptySound, PortPosition, volume, Random.Range(0.95f, 1.05f));

        private void OnRefilled() => AudioOneShots.Play(refillSound, PortPosition, volume * 0.8f);

        private void LateUpdate()
        {
            bool repairing = _repairEndsAt >= 0f && Time.time < _repairEndsAt;
            if (!repairing && _repairEndsAt >= 0f)
            {
                _repairEndsAt = -1f;
                if (repairMotes != null) repairMotes.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            // A hard flare on the slam, then a soft glow while the nanites work.
            float target = repairing ? 0.25f : 0f;
            _lightAmount = Mathf.MoveTowards(_lightAmount, target, Time.deltaTime * 3f);
            _light.enabled = _lightAmount > 0.001f;
            if (_light.enabled)
                _light.intensity = lightIntensity * _lightAmount * (repairing ? 1f + Mathf.Sin(Time.time * 18f) * 0.15f : 1f);
        }
    }
}
