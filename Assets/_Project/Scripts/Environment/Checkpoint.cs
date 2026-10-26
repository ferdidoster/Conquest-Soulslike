using System;
using System.Collections.Generic;
using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>
    /// A place to rest, like a bonfire. Walking into its radius restores the player's health and
    /// healing charges and makes it the respawn point (see the player's PlayerRespawn). The beacon
    /// light flares when someone rests here and burns brighter once the checkpoint is active.
    /// </summary>
    public sealed class Checkpoint : MonoBehaviour
    {
        private static readonly List<Checkpoint> Active = new List<Checkpoint>();

        [SerializeField, Min(0.5f)] private float radius = 2.2f;
        [SerializeField, Tooltip("Where the player comes back. Defaults to this transform.")]
        private Transform respawnPoint;
        [SerializeField, Tooltip("Optional beacon light.")] private Light beacon;
        [SerializeField] private float idleIntensity = 1.2f;
        [SerializeField] private float litIntensity = 3f;
        [SerializeField] private float flareIntensity = 9f;
        [SerializeField, Tooltip("Optional effect played when someone rests here.")]
        private ParticleSystem restEffect;

        private float _flare;
        private bool _lit;

        public float Radius => radius;
        public bool IsLit => _lit;
        public Pose RespawnPose
        {
            get
            {
                Transform point = respawnPoint != null ? respawnPoint : transform;
                return new Pose(point.position, point.rotation);
            }
        }

        /// <summary>Raised when someone rests here.</summary>
        public event Action Rested;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        /// <summary>The checkpoint whose radius contains <paramref name="position"/>, if any.</summary>
        public static Checkpoint At(Vector3 position)
        {
            foreach (Checkpoint checkpoint in Active)
            {
                Vector3 offset = position - checkpoint.transform.position;
                if (Mathf.Abs(offset.y) < 2.5f && new Vector2(offset.x, offset.z).magnitude <= checkpoint.radius)
                    return checkpoint;
            }
            return null;
        }

        public void Rest()
        {
            foreach (Checkpoint other in Active) other._lit = other == this;
            _flare = 1f;
            if (restEffect != null)
            {
                restEffect.Clear(true);
                restEffect.Play(true);
            }
            Rested?.Invoke();
        }

        private void Update()
        {
            if (beacon == null) return;
            _flare = Mathf.MoveTowards(_flare, 0f, Time.deltaTime * 0.8f);
            float pulse = 1f + Mathf.Sin(Time.time * 2.2f) * 0.08f;
            beacon.intensity = Mathf.Lerp(_lit ? litIntensity : idleIntensity, flareIntensity, _flare * _flare) * pulse;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
