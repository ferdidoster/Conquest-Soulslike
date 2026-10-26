using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>Slow bob and sway for things floating in the sky.</summary>
    public sealed class Hover : MonoBehaviour
    {
        [SerializeField] private float height = 0.8f;
        [SerializeField] private float period = 7f;
        [SerializeField, Tooltip("Degrees of roll.")] private float sway = 1.5f;

        private Vector3 _origin;
        private Quaternion _rotation;
        private float _phase;

        private void Awake()
        {
            _origin = transform.localPosition;
            _rotation = transform.localRotation;
            _phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            float t = Time.time * Mathf.PI * 2f / period + _phase;
            transform.localPosition = _origin + Vector3.up * (Mathf.Sin(t) * height);
            transform.localRotation = _rotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * sway);
        }
    }
}
