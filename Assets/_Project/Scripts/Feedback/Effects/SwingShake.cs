using Conquest.CameraSystem;
using Conquest.Combat;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// A small camera jolt as a heavy weapon comes down, hit or miss, so the swing itself has weight.
    /// Meant for the player; enemies' swings shouldn't shake the player's camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SwingShake : MonoBehaviour
    {
        [SerializeField] private MeleeHitDetector detector;
        [SerializeField, Tooltip("Only blows at least this heavy shake the camera.")]
        private float minWeight = 1.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Trauma per unit of weight above minWeight, plus a base.")]
        private float trauma = 0.12f;
        [SerializeField, Tooltip("Metres the camera dips along the swing.")]
        private float kick = 0.08f;

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
            if (swing.Weight < minWeight) return;
            float strength = 1f + (swing.Weight - minWeight);
            CameraShake.AddTrauma(trauma * strength);
            CameraShake.Kick((transform.forward + Vector3.down) * (kick * strength));
        }
    }
}
