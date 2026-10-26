using Conquest.Core.Stats;
using UnityEngine;

namespace Conquest.UI
{
    /// <summary>
    /// Generic bar for any <see cref="IResourceSource"/> (health, stamina, ...). Fills are
    /// RectTransforms scaled by anchors, so no sprites are needed. A trailing fill catches up
    /// after a short delay, souls style.
    /// </summary>
    public sealed class ResourceBar : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing IResourceSource (Health, Stamina).")]
        private MonoBehaviour source;
        [SerializeField] private RectTransform fill;
        [SerializeField, Tooltip("Optional trailing fill that catches up after a delay.")]
        private RectTransform trail;
        [SerializeField] private float trailDelay = 0.5f;
        [SerializeField] private float trailSpeed = 0.6f;

        private ResourcePool _pool;
        private float _value;
        private float _trailValue;
        private float _trailHoldUntil;

        public void Bind(IResourceSource resource, RectTransform fillRect, RectTransform trailRect)
        {
            source = resource as MonoBehaviour;
            fill = fillRect;
            trail = trailRect;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            // Pools are created in Awake, which may run after our OnEnable.
            if (_pool == null) Subscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            _pool = (source as IResourceSource)?.Pool;
            if (_pool == null) return;

            _pool.Changed += OnChanged;
            _value = _trailValue = _pool.Normalized;
            Apply(fill, _value);
            Apply(trail, _trailValue);
        }

        private void Unsubscribe()
        {
            if (_pool != null) _pool.Changed -= OnChanged;
            _pool = null;
        }

        private void OnChanged(float current, float max)
        {
            float normalized = max > 0f ? current / max : 0f;
            if (normalized < _value) _trailHoldUntil = Time.time + trailDelay;
            _value = normalized;
            Apply(fill, _value);
        }

        private void Update()
        {
            if (trail == null) return;

            if (_trailValue < _value) _trailValue = _value;
            else if (Time.time >= _trailHoldUntil)
                _trailValue = Mathf.MoveTowards(_trailValue, _value, trailSpeed * Time.deltaTime);

            Apply(trail, _trailValue);
        }

        private static void Apply(RectTransform rect, float normalized)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(Mathf.Clamp01(normalized), rect.anchorMax.y);
        }
    }
}
