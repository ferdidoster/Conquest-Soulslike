using System.Collections.Generic;
using Conquest.Environment;
using UnityEngine;
using UnityEngine.AI;

namespace Conquest.AI
{
    /// <summary>
    /// Carves a gate's leaves out of the NavMesh while it is shut, so paths go around closed gates
    /// and through them once an event opens them. Auto doors (unlocked sliding doors) never block:
    /// they open for anyone who walks up. Added at runtime by <see cref="LevelNavMesh"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GateNavBlocker : MonoBehaviour
    {
        private readonly List<NavMeshObstacle> _obstacles = new List<NavMeshObstacle>();
        private Gate _gate;
        private bool _blocking;

        /// <summary>True for colliders on a gate's moving leaves (left out of the baked NavMesh).</summary>
        public static bool IsLeaf(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (IsLeafName(t.name)) return true;
                if (t.GetComponent<Gate>() != null) return false;
            }

            return false;
        }

        private static bool IsLeafName(string n) =>
            n.EndsWith("_Leaf_L") || n.EndsWith("_Leaf_R") || n.EndsWith("_Leaf_Up") || n.EndsWith("_Leaf_Down");

        private void Awake()
        {
            _gate = GetComponent<Gate>();
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger || !IsLeaf(c.transform)) continue;
                var obstacle = c.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                if (c is BoxCollider box)
                {
                    obstacle.center = box.center;
                    obstacle.size = box.size;
                }
                else
                {
                    Bounds local = LocalBounds(c);
                    obstacle.center = local.center;
                    obstacle.size = local.size;
                }

                _obstacles.Add(obstacle);
            }

            _blocking = !Blocks();
            Apply();
        }

        // a gate starts opening before its leaves are clear, so it counts as shut until fully open
        private bool Blocks() => _gate != null && !_gate.IsFullyOpen && !(_gate.AutoOpens && !_gate.IsLocked);

        private void Update() => Apply();

        private void Apply()
        {
            bool blocks = Blocks();
            if (blocks == _blocking) return;
            _blocking = blocks;
            foreach (NavMeshObstacle o in _obstacles)
                if (o != null) o.enabled = blocks;
        }

        private static Bounds LocalBounds(Collider c)
        {
            Bounds world = c.bounds;
            Transform t = c.transform;
            var local = new Bounds(t.InverseTransformPoint(world.center), Vector3.zero);
            Vector3 e = world.extents;
            for (int i = 0; i < 8; i++)
                local.Encapsulate(t.InverseTransformPoint(world.center +
                                                          new Vector3((i & 1) == 0 ? -e.x : e.x,
                                                              (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z)));
            return local;
        }
    }
}
