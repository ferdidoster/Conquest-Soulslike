using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>Makes a fire light breathe with smooth noise around its authored intensity.</summary>
    [RequireComponent(typeof(Light))]
    public sealed class FlameFlicker : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float amount = 0.25f;
        [SerializeField] private float speed = 5f;

        private Light _light;
        private float _baseIntensity;
        private float _seed;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(_seed, Time.time * speed);
            _light.intensity = _baseIntensity * (1f + (noise - 0.5f) * 2f * amount);
        }
    }
}
