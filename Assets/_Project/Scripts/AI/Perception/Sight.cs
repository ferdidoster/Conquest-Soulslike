using Conquest.Targeting;
using UnityEngine;

namespace Conquest.AI
{
    /// <summary>
    /// Line-of-sight checks for the AI brains. Anything solid between the eye and the target blocks the
    /// view (walls, closed gate leaves, crates); characters, triggers and the Ignore Raycast layer
    /// (invisible boundary walls) do not.
    /// </summary>
    public static class Sight
    {
        private const int CharacterLayer = 8;
        private const int BlockingLayers = ~((1 << 2) | (1 << 5) | (1 << CharacterLayer)); // Ignore Raycast, UI

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>True when nothing solid lies between <paramref name="eye"/> and the target's lock point.</summary>
        public static bool CanSee(GameObject self, Vector3 eye, ITargetable target)
        {
            if (target == null) return false;
            Vector3 to = target.LockPoint.position - eye;
            float distance = to.magnitude;
            if (distance < 0.01f) return true;

            int count = Physics.RaycastNonAlloc(eye, to / distance, Hits, distance, BlockingLayers,
                QueryTriggerInteraction.Ignore);
            Transform targetRoot = target.Owner != null ? target.Owner.transform.root : null;
            Transform selfRoot = self.transform.root;
            for (int i = 0; i < count; i++)
            {
                Transform hit = Hits[i].transform;
                if (hit.root == selfRoot || hit.root == targetRoot) continue;
                if (Hits[i].collider.GetComponentInParent<CharacterController>() != null) continue;
                return false;
            }

            return true;
        }

        /// <summary>True when <paramref name="point"/> lies within a cone of <paramref name="angle"/> degrees around forward.</summary>
        public static bool InView(Transform self, Vector3 point, float angle)
        {
            if (angle >= 360f) return true;
            Vector3 flat = point - self.position;
            flat.y = 0f;
            return flat.sqrMagnitude < 0.0001f || Vector3.Angle(self.forward, flat) <= angle * 0.5f;
        }
    }
}
