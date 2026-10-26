using System.Collections;
using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// A character's blood: a burst of droplets at the wound, splats where the droplets land and
    /// a pool under the body when it dies. Swap the prefab and material for oil, sand, ichor, ...
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BloodSpray : MonoBehaviour
    {
        [Header("Burst")]
        [SerializeField] private ParticleSystem burst;

        [Header("Splats")]
        [SerializeField] private Material splatMaterial;
        [SerializeField, Tooltip("Surfaces blood can land on. Leave characters out.")]
        private LayerMask groundMask = 1;
        [SerializeField, Tooltip("Splats per hit of weight 1.")]
        private int splatsPerHit = 5;
        [SerializeField] private Vector2 splatSize = new Vector2(0.25f, 0.7f);
        [SerializeField, Tooltip("How far (m) droplets fly from the wound.")]
        private float sprayDistance = 2.2f;

        [Header("Pool (on death)")]
        [SerializeField, Tooltip("Where the pool forms. Defaults to this transform.")]
        private Transform poolAnchor;
        [SerializeField] private float poolSize = 2f;
        [SerializeField, Tooltip("Seconds before the pool starts to spread (let the body land first).")]
        private float poolDelay = 0.9f;
        [SerializeField] private float poolSpreadTime = 2.5f;

        public LayerMask GroundMask
        {
            get => groundMask;
            set => groundMask = value;
        }

        /// <summary>Sprays blood from <paramref name="point"/> along the blow and the swing of the weapon.</summary>
        public void Spray(Vector3 point, Vector3 direction, Vector3 swing, float amount)
        {
            if (amount <= 0f) return;

            Vector3 flow = direction.normalized + swing.normalized * 0.8f;
            flow.y = 0f;
            flow = flow.sqrMagnitude > 1e-4f ? flow.normalized : transform.forward;

            if (burst != null)
                VfxPool.Play(burst, point, Quaternion.LookRotation(flow + Vector3.up * 0.35f), Mathf.Sqrt(amount));

            float reach = sprayDistance * Mathf.Sqrt(amount);
            Vector3 side = Vector3.Cross(Vector3.up, flow);
            int count = Mathf.Max(1, Mathf.RoundToInt(splatsPerHit * amount));

            // One splat drips straight down at the wound, the rest land along the spray.
            Splat(point, Random.Range(splatSize.x, splatSize.y) * 1.2f, 0.12f);
            for (int i = 0; i < count; i++)
            {
                float distance = Random.Range(0.25f, 1f) * reach;
                Vector3 landing = point + flow * distance + side * (Random.Range(-0.35f, 0.35f) * distance);
                // Far droplets are small; close ones are big.
                float size = Mathf.Lerp(splatSize.y, splatSize.x, distance / reach) * Random.Range(0.7f, 1.2f);
                Splat(landing, size, 0.1f + distance * 0.09f);
            }
        }

        /// <summary>Spreads a pool of blood under the character.</summary>
        public void PoolBeneath() => StartCoroutine(PoolAfterDelay());

        private IEnumerator PoolAfterDelay()
        {
            // Wait until the body has landed (and slid), then spread from where it lies.
            yield return new WaitForSeconds(poolDelay);
            Transform anchor = poolAnchor != null ? poolAnchor : transform;
            Splat(anchor.position + Vector3.up * 0.5f, poolSize, 0f, poolSpreadTime);
        }

        private void Splat(Vector3 from, float size, float delay, float growTime = 0.12f)
        {
            if (splatMaterial == null) return;
            Vector3 origin = new Vector3(from.x, Mathf.Max(from.y, transform.position.y) + 0.4f, from.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, 4f, groundMask,
                    QueryTriggerInteraction.Ignore))
                SplatDecals.Spawn(splatMaterial, ground.point, ground.normal, size, delay, growTime);
        }
    }
}
