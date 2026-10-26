using UnityEngine;

namespace Conquest.CameraSystem
{
    /// <summary>One frame of camera shake, ready to add on top of a camera pose.</summary>
    public readonly struct CameraShakeSample
    {
        public readonly Vector3 Offset;
        public readonly float Roll;
        /// <summary>Fraction to shrink the view by (0.05 = zoom in 5%).</summary>
        public readonly float Zoom;

        public CameraShakeSample(Vector3 offset, float roll, float zoom)
        {
            Offset = offset;
            Roll = roll;
            Zoom = zoom;
        }
    }

    /// <summary>
    /// Shared, camera-agnostic shake. Gameplay adds trauma, kicks and zoom punches; every camera
    /// (the orbit camera, a Cinemachine extension, ...) samples the same signal, so switching
    /// cameras never changes how a hit feels. Uses unscaled time so it keeps going through
    /// hit-stop and slow motion.
    /// </summary>
    public static class CameraShake
    {
        public static float MaxOffset = 0.35f;
        public static float MaxRoll = 2.5f;
        public static float Frequency = 22f;
        /// <summary>Trauma lost per second.</summary>
        public static float TraumaDecay = 1.8f;
        /// <summary>How fast kicks and zoom punches spring back (per second, exponential).</summary>
        public static float KickRecovery = 14f;

        private static float _trauma, _traumaTime;
        private static Vector3 _kick;
        private static float _kickTime;
        private static float _zoom, _zoomTime;

        private static float Now => Time.unscaledTime;

        /// <summary>
        /// Clears the shake when play mode starts. The timestamps matter too: unscaled time restarts
        /// at 0 each play session, so a timestamp left from the last session (statics survive when
        /// domain reload is off) would be in the future, and the decay would blow up to infinity.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            _trauma = _traumaTime = 0f;
            _kick = Vector3.zero;
            _kickTime = 0f;
            _zoom = _zoomTime = 0f;
        }

        /// <summary>Adds noisy shake. Trauma is clamped to 1 and the shake grows with its square.</summary>
        public static void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(Trauma + amount);
            _traumaTime = Now;
        }

        /// <summary>Jolts the camera along a world direction (metres), e.g. the direction of a blow.</summary>
        public static void Kick(Vector3 worldOffset)
        {
            _kick = CurrentKick + worldOffset;
            _kickTime = Now;
        }

        /// <summary>Briefly zooms in by a fraction of the view (0.05 = 5%).</summary>
        public static void PunchZoom(float amount)
        {
            _zoom = Mathf.Max(CurrentZoom, amount);
            _zoomTime = Now;
        }

        public static float Trauma => Mathf.Max(0f, _trauma - TraumaDecay * Since(_traumaTime));
        private static Vector3 CurrentKick => _kick * Mathf.Exp(-KickRecovery * Since(_kickTime));
        private static float CurrentZoom => _zoom * Mathf.Exp(-KickRecovery * 0.6f * Since(_zoomTime));

        /// <summary>Seconds since a timestamp, never negative, so the decays always stay finite.</summary>
        private static float Since(float time) => Mathf.Max(0f, Now - time);

        /// <summary>The shake for a camera looking along <paramref name="rotation"/>.</summary>
        public static CameraShakeSample Sample(Quaternion rotation)
        {
            float t = Now * Frequency;
            float shake = Trauma * Trauma;

            Vector3 noise = Vector3.zero;
            float roll = 0f;
            if (shake > 0f)
            {
                float x = Mathf.PerlinNoise(t, 0.1f) * 2f - 1f;
                float y = Mathf.PerlinNoise(0.7f, t) * 2f - 1f;
                roll = (Mathf.PerlinNoise(t, 5.3f) * 2f - 1f) * MaxRoll * shake;
                noise = rotation * new Vector3(x, y, 0f) * (MaxOffset * shake);
            }

            // Kicks only move the camera across the screen, never toward it.
            Vector3 forward = rotation * Vector3.forward;
            Vector3 kick = Vector3.ProjectOnPlane(CurrentKick, forward);
            return new CameraShakeSample(noise + kick, roll, CurrentZoom);
        }
    }
}
