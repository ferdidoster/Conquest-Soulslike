using System.Collections.Generic;
using UnityEngine;

namespace Conquest.Targeting
{
    /// <summary>
    /// Stateless scoring for picking lock-on targets. Separated from the controller so AI,
    /// spells or homing projectiles can reuse the same rules.
    /// </summary>
    public static class TargetSelector
    {
        public struct Query
        {
            public Vector3 Origin;
            public Vector3 ViewForward;
            public float MaxDistance;
            public float MaxViewAngle;
            public LayerMask OcclusionMask;
            public GameObject Ignore;
        }

        /// <summary>Best target in front of the view, preferring the one closest to the screen centre.</summary>
        public static ITargetable FindBest<T>(IReadOnlyList<T> candidates, in Query query) where T : ITargetable
        {
            ITargetable best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (!IsValid(candidate, query, out Vector3 toTarget, out float distance)) continue;

                // Measured on the ground plane so steep (e.g. isometric) views still find targets.
                float angle = Vector3.Angle(Flat(query.ViewForward), Flat(toTarget));
                if (angle > query.MaxViewAngle) continue;

                // Angle matters most; distance breaks ties.
                float score = angle + distance / query.MaxDistance * 20f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// The nearest target to the left (<paramref name="direction"/> &lt; 0) or right of the
        /// current one, measured as the signed yaw angle around the origin.
        /// </summary>
        public static ITargetable FindAdjacent<T>(IReadOnlyList<T> candidates, ITargetable current, float direction, in Query query)
            where T : ITargetable
        {
            if (current == null) return FindBest(candidates, query);

            Vector3 toCurrent = (current.LockPoint.position - query.Origin);
            toCurrent.y = 0f;

            ITargetable best = null;
            float bestAngle = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (ReferenceEquals(candidate, current)) continue;
                if (!IsValid(candidate, query, out Vector3 toTarget, out _)) continue;

                toTarget.y = 0f;
                float signed = Vector3.SignedAngle(toCurrent, toTarget, Vector3.up);
                if (Mathf.Sign(signed) != Mathf.Sign(direction)) continue;

                float abs = Mathf.Abs(signed);
                if (abs < bestAngle)
                {
                    bestAngle = abs;
                    best = candidate;
                }
            }

            return best;
        }

        public static bool HasLineOfSight(Vector3 origin, ITargetable target, LayerMask occlusionMask)
        {
            Vector3 end = target.LockPoint.position;
            return !Physics.Linecast(origin, end, out RaycastHit hit, occlusionMask, QueryTriggerInteraction.Ignore)
                   || hit.transform.IsChildOf(target.Owner.transform);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static bool IsValid<T>(T candidate, in Query query, out Vector3 toTarget, out float distance)
            where T : ITargetable
        {
            toTarget = default;
            distance = 0f;

            if (candidate == null || !candidate.IsTargetable || candidate.Owner == query.Ignore) return false;

            toTarget = candidate.LockPoint.position - query.Origin;
            distance = toTarget.magnitude;
            if (distance > query.MaxDistance) return false;

            return HasLineOfSight(query.Origin, candidate, query.OcclusionMask);
        }
    }
}
