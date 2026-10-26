using UnityEngine;

namespace Conquest.Core.Utilities
{
    public static class VectorExtensions
    {
        /// <summary>Removes the vertical component.</summary>
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Flattened and normalized, or zero if the vector is (almost) vertical.</summary>
        public static Vector3 FlatNormalized(this Vector3 v)
        {
            Vector3 flat = v.Flat();
            return flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.zero;
        }

        /// <summary>
        /// Converts a 2D stick vector to a world direction on the ground plane, relative to a view
        /// (normally the camera). Keeps the stick magnitude.
        /// </summary>
        public static Vector3 RelativeTo(this Vector2 input, Transform view)
        {
            Vector3 forward = view.forward.FlatNormalized();
            Vector3 right = view.right.FlatNormalized();
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
