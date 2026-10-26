using Conquest.Combat;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>Plays a whoosh when a weapon's hit window opens; heavier blows get a heavier whoosh.</summary>
    [DisallowMultipleComponent]
    public sealed class SwingSound : MonoBehaviour
    {
        [SerializeField] private MeleeHitDetector detector;
        [SerializeField] private AudioClip[] lightSwings = new AudioClip[0];
        [SerializeField] private AudioClip[] heavySwings = new AudioClip[0];
        [SerializeField, Tooltip("Blows at least this heavy use the heavy whoosh.")]
        private float heavyWeight = 1.5f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
        [SerializeField] private Vector2 pitch = new Vector2(0.9f, 1.1f);

        private void Awake()
        {
            if (detector == null) detector = GetComponentInParent<MeleeHitDetector>();
        }

        private void OnEnable()
        {
            if (detector != null) detector.Opened += OnOpened;
        }

        private void OnDisable()
        {
            if (detector != null) detector.Opened -= OnOpened;
        }

        private void OnOpened(DamageInfo swing)
        {
            AudioClip[] clips = swing.Weight >= heavyWeight && heavySwings.Length > 0 ? heavySwings : lightSwings;
            AudioOneShots.PlayRandom(clips, transform.position, volume, pitch);
        }
    }
}
