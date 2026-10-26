using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>
    /// Trigger volume of an auto door: tells its <see cref="Gate"/> when characters (anything with
    /// a CharacterController or Rigidbody) walk in or out.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class GateSensor : MonoBehaviour
    {
        [SerializeField] private Gate gate;

        private void Awake()
        {
            if (gate == null) gate = GetComponentInParent<Gate>();
            GetComponent<Collider>().isTrigger = true;
        }

        private static bool IsCharacter(Collider other) =>
            other.GetComponent<CharacterController>() != null || other.attachedRigidbody != null;

        private void OnTriggerEnter(Collider other)
        {
            if (gate != null && IsCharacter(other)) gate.SensorChanged(+1);
        }

        private void OnTriggerExit(Collider other)
        {
            if (gate != null && IsCharacter(other)) gate.SensorChanged(-1);
        }
    }
}
