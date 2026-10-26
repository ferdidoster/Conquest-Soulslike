using System.Collections.Generic;
using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>
    /// On the camera: hides every <see cref="FadeOccluder"/> between the camera and its target, so
    /// walls never block the view of the player in interiors (works for the isometric and the
    /// third-person camera alike).
    /// </summary>
    public sealed class OccluderFader : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float targetHeight = 1.1f;
        [SerializeField] private float radius = 0.9f;
        [SerializeField] private LayerMask mask = ~0;
        [Tooltip("Keep pieces hidden this long after they stop blocking, so they don't flicker.")]
        [SerializeField] private float holdSeconds = 0.25f;

        private readonly RaycastHit[] _hits = new RaycastHit[48];
        private readonly Dictionary<FadeOccluder, float> _hidden = new Dictionary<FadeOccluder, float>();
        private readonly List<FadeOccluder> _release = new List<FadeOccluder>();

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 from = transform.position;
            Vector3 to = target.position + Vector3.up * targetHeight;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) return;

            int count = Physics.SphereCastNonAlloc(from, radius, delta / distance, _hits,
                Mathf.Max(0f, distance - radius * 1.5f), mask, QueryTriggerInteraction.Ignore);
            float now = Time.time;
            for (int i = 0; i < count; i++)
            {
                FadeOccluder occluder = _hits[i].collider.GetComponentInParent<FadeOccluder>();
                // gates and doors stay visible, they are how the player finds the way
                if (occluder == null || occluder.GetComponent<Gate>() != null) continue;
                occluder.Hidden = true;
                _hidden[occluder] = now;
            }

            _release.Clear();
            foreach (KeyValuePair<FadeOccluder, float> pair in _hidden)
                if (now - pair.Value > holdSeconds) _release.Add(pair.Key);
            foreach (FadeOccluder occluder in _release)
            {
                if (occluder != null) occluder.Hidden = false;
                _hidden.Remove(occluder);
            }
        }

        private void OnDisable()
        {
            foreach (FadeOccluder occluder in _hidden.Keys)
                if (occluder != null) occluder.Hidden = false;
            _hidden.Clear();
        }
    }
}
