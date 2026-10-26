using System.Collections.Generic;
using Conquest.Core;
using UnityEngine;

namespace Conquest.Targeting
{
    /// <summary>
    /// Marks a GameObject as lock-on-able. Keeps a static registry so finding targets never needs
    /// a physics query. Stops being targetable when an <see cref="ILifeState"/> on it dies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Targetable : MonoBehaviour, ITargetable
    {
        private static readonly List<Targetable> Registry = new List<Targetable>();
        public static IReadOnlyList<Targetable> All => Registry;

        [SerializeField, Tooltip("Defaults to this transform.")] private Transform lockPoint;

        private ILifeState _life;

        public Transform LockPoint => lockPoint != null ? lockPoint : transform;
        public bool IsTargetable => isActiveAndEnabled && (_life == null || _life.IsAlive);
        public GameObject Owner => gameObject;

        private void Awake() => _life = GetComponentInParent<ILifeState>();
        private void OnEnable() => Registry.Add(this);
        private void OnDisable() => Registry.Remove(this);

        public void SetLockPoint(Transform point) => lockPoint = point;
    }
}
