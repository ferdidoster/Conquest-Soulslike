using Conquest.Core.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace Conquest.UI
{
    /// <summary>
    /// Flashes red at the screen edges when a resource (the player's health) drops. Bigger drops
    /// flash harder. Put it on a full-screen Image; the vignette texture is generated.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class DamageVignette : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing IResourceSource, usually the player's Health.")]
        private MonoBehaviour source;
        [SerializeField] private Color color = new Color(0.55f, 0.02f, 0.04f, 1f);
        [SerializeField, Tooltip("Damage (as a fraction of max health) that gives a full-strength flash.")]
        private float fullFlashAt = 0.3f;
        [SerializeField, Range(0f, 1f)] private float minimumFlash = 0.35f;
        [SerializeField] private float fadeTime = 0.6f;

        private Image _image;
        private ResourcePool _pool;
        private float _last;
        private float _strength;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.raycastTarget = false;
            _image.sprite = CreateVignette(128);
            SetAlpha(0f);
        }

        // The pool is created in the source's Awake, so subscribe in Start.
        private void Start()
        {
            _pool = (source as IResourceSource)?.Pool;
            if (_pool == null) return;
            _last = _pool.Current;
            _pool.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (_pool != null) _pool.Changed -= OnChanged;
        }

        private void OnChanged(float current, float max)
        {
            float drop = _last - current;
            _last = current;
            if (drop <= 0f || max <= 0f) return;
            _strength = Mathf.Max(_strength, Mathf.Lerp(minimumFlash, 1f, drop / max / fullFlashAt));
        }

        private void Update()
        {
            if (_strength <= 0f) return;
            _strength = Mathf.Max(0f, _strength - Time.unscaledDeltaTime / fadeTime);
            SetAlpha(_strength);
        }

        private void SetAlpha(float alpha)
        {
            Color c = color;
            c.a = alpha;
            _image.color = c;
        }

        /// <summary>A soft ring: transparent in the middle, opaque toward the corners.</summary>
        private static Sprite CreateVignette(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.Alpha8, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float edge = Mathf.InverseLerp(0.55f, 1.25f, Mathf.Sqrt(u * u + v * v));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.SmoothStep(0f, 1f, edge) * 255f));
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
