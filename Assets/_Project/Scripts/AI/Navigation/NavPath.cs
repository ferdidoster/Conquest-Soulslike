using Conquest.Core.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace Conquest.AI
{
    /// <summary>
    /// Steers a character along a NavMesh path without a NavMeshAgent: the brain asks for a direction
    /// each frame and feeds it to the character like stick input, so the CharacterController and the
    /// state machine stay in charge of the actual motion. Without a NavMesh it falls back to walking
    /// straight at the goal.
    /// </summary>
    public sealed class NavPath
    {
        private const float RepathInterval = 0.35f;
        private const float CornerReached = 0.45f;

        private NavMeshPath _path; // created on first use: Unity refuses it in field initializers
        private readonly Vector3[] _corners = new Vector3[48];
        private int _count;
        private int _next;
        private Vector3 _goal;
        private float _nextRepathAt;
        private bool _onMesh;

        /// <summary>False when the last search could only get close to the goal (a closed gate, a ledge).</summary>
        public bool Reachable { get; private set; } = true;

        /// <summary>The flat direction to walk in to reach <paramref name="goal"/>; zero once there.</summary>
        public Vector3 Steer(Vector3 from, Vector3 goal, float stopDistance = 0.3f)
        {
            if (Time.time >= _nextRepathAt || (goal - _goal).sqrMagnitude > 1f) Repath(from, goal);

            if (!_onMesh)
            {
                Vector3 direct = (goal - from).Flat();
                return direct.magnitude > stopDistance ? direct.normalized : Vector3.zero;
            }

            while (_next < _count && (_corners[_next] - from).Flat().sqrMagnitude < CornerReached * CornerReached)
                _next++;
            if (_next >= _count) return Vector3.zero;

            Vector3 to = (_corners[_next] - from).Flat();
            bool last = _next == _count - 1;
            if (last && to.magnitude <= stopDistance) return Vector3.zero;
            return to.normalized;
        }

        public void Clear()
        {
            _count = 0;
            _next = 0;
            _nextRepathAt = 0f;
        }

        private void Repath(Vector3 from, Vector3 goal)
        {
            _goal = goal;
            _nextRepathAt = Time.time + RepathInterval;
            _onMesh = false;
            Reachable = true;

            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas)) return;
            if (!NavMesh.SamplePosition(goal, out NavMeshHit end, 3f, NavMesh.AllAreas)) return;
            if (_path == null) _path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, _path) ||
                _path.status == NavMeshPathStatus.PathInvalid)
                return;

            _onMesh = true;
            Reachable = _path.status == NavMeshPathStatus.PathComplete;
            _count = _path.GetCornersNonAlloc(_corners);
            _next = _count > 1 ? 1 : 0; // corner 0 is where we stand
        }
    }
}
