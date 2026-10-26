using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>
    /// A box (in this object's space) that tells <see cref="LevelSections"/> the player is in a
    /// section. For hand-built levels: put one or more under the level root per section.
    /// </summary>
    public sealed class SectionZone : MonoBehaviour
    {
        [SerializeField] private string section = "Hub";
        [SerializeField] private Vector3 center = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private Vector3 size = new Vector3(16f, 5f, 16f);

        public string Section => section;

        public bool Contains(Vector3 worldPosition)
        {
            Vector3 p = transform.InverseTransformPoint(worldPosition) - center;
            Vector3 h = size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.15f);
            Gizmos.DrawCube(center, size);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
