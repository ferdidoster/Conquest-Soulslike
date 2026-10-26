using UnityEngine;

namespace Conquest.Environment
{
    public enum SpawnKind
    {
        Player,
        Enemy,
    }

    /// <summary>
    /// Marks where a level wants a character to start; its forward is the facing direction.
    /// Drop these into your own levels and the sandbox builder spawns the knights on them.
    /// </summary>
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private SpawnKind kind;

        public SpawnKind Kind => kind;
        public Pose Pose => new Pose(transform.position, transform.rotation);

        private void OnDrawGizmos()
        {
            Gizmos.color = kind == SpawnKind.Player ? new Color(0.3f, 0.8f, 1f) : new Color(1f, 0.35f, 0.3f);
            Vector3 chest = transform.position + Vector3.up * 0.9f;
            Gizmos.DrawWireSphere(chest, 0.45f);
            Gizmos.DrawLine(chest, chest + transform.forward * 1.2f);
        }
    }
}
