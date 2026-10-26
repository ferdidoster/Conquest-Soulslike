using System;
using Conquest.Environment;
using Conquest.Stats;
using UnityEngine;

namespace Conquest.Character
{
    /// <summary>
    /// Checkpoints and respawning for the player. Walking into a <see cref="Checkpoint"/> rests there:
    /// full health, a refilled <see cref="NanoInjector"/> and a new respawn point. After dying the
    /// player comes back at the last checkpoint (or where the level started) with both refilled.
    /// Enemies keep their state; only the player is reset.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActionCharacter))]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Seconds from death to coming back.")]
        private float respawnDelay = 4f;

        private ActionCharacter _character;
        private Health _health;
        private NanoInjector _injector;
        private Pose _start;
        private Checkpoint _current;
        private Checkpoint _inside;
        private float _respawnAt = -1f;

        public Checkpoint Current => _current;
        public bool IsWaitingToRespawn => _respawnAt >= 0f;

        public event Action Respawned;

        private void Awake()
        {
            _character = GetComponent<ActionCharacter>();
            _health = GetComponent<Health>();
            _injector = GetComponent<NanoInjector>();
            _start = new Pose(transform.position, transform.rotation);
        }

        private void OnEnable() => _health.Died += OnDied;
        private void OnDisable() => _health.Died -= OnDied;

        private void OnDied()
        {
            _respawnAt = Time.time + respawnDelay;
            if (_injector != null) _injector.CancelRepair();
        }

        private void Update()
        {
            if (_respawnAt >= 0f)
            {
                if (Time.time >= _respawnAt) Respawn();
                return;
            }

            Checkpoint here = _health.IsAlive ? Checkpoint.At(transform.position) : null;
            if (here != null && here != _inside) Rest(here);
            _inside = here;
        }

        /// <summary>Rests at a checkpoint: full health and charges, and it becomes the respawn point.</summary>
        public void Rest(Checkpoint checkpoint)
        {
            _current = checkpoint;
            _health.Heal(_health.Pool.Max);
            if (_injector != null) _injector.Refill();
            checkpoint.Rest();
        }

        public void Respawn()
        {
            _respawnAt = -1f;
            Pose pose = _current != null ? _current.RespawnPose : _start;
            _character.Respawn(pose.position, pose.rotation);
            if (_injector != null) _injector.Refill();
            _inside = Checkpoint.At(pose.position); // coming back already counts as resting there
            Respawned?.Invoke();
        }
    }
}
