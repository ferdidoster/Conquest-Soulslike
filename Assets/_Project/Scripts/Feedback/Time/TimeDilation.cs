using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Short global slow motion (kills, parries). Overlapping requests keep the slowest scale and
    /// the latest end; time eases back to normal over the last part of the effect.
    /// </summary>
    public static class TimeDilation
    {
        /// <summary>Fraction of the effect spent easing back to full speed.</summary>
        public static float RecoverPortion = 0.4f;

        private static float _scale = 1f;
        private static float _start;
        private static float _end;
        private static float _baseFixedDeltaTime = -1f;
        private static Runner _runner;

        public static bool IsActive => _runner != null && _runner.enabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            // Unscaled time restarts each play session, so old timestamps must not survive it.
            _scale = 1f;
            _start = _end = 0f;
            _runner = null;
            _baseFixedDeltaTime = -1f;
        }

        /// <param name="scale">Time scale while slowed (0.25 = quarter speed).</param>
        /// <param name="realSeconds">Duration in real (unscaled) seconds.</param>
        public static void Slow(float scale, float realSeconds)
        {
            if (realSeconds <= 0f || scale >= 1f) return;
            if (_baseFixedDeltaTime < 0f) _baseFixedDeltaTime = Time.fixedDeltaTime;

            float now = Time.unscaledTime;
            _scale = IsActive ? Mathf.Min(_scale, scale) : scale;
            if (!IsActive) _start = now;
            _end = Mathf.Max(IsActive ? _end : 0f, now + realSeconds);

            if (_runner == null)
            {
                var go = new GameObject("[Time Dilation]") { hideFlags = HideFlags.HideInHierarchy };
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }

            _runner.enabled = true;
            Apply(_scale);
        }

        private static void Apply(float scale)
        {
            Time.timeScale = scale;
            Time.fixedDeltaTime = _baseFixedDeltaTime * scale;
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                float now = Time.unscaledTime;
                if (now >= _end)
                {
                    Apply(1f);
                    enabled = false;
                    return;
                }

                float recoverStart = Mathf.Lerp(_end, _start, RecoverPortion);
                float t = Mathf.InverseLerp(recoverStart, _end, now);
                Apply(Mathf.Lerp(_scale, 1f, t * t));
            }

            private void OnDestroy() => Apply(1f);
        }
    }
}
