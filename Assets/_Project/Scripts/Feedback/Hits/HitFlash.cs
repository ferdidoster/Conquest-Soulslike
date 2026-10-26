using UnityEngine;

namespace Conquest.Feedback
{
    /// <summary>
    /// Flashes a model toward a colour for a moment (the classic white hit flash). Works with any
    /// shader that has a base colour property, through property blocks, so materials stay shared.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField, Tooltip("Leave empty to use every mesh renderer below this object.")]
        private Renderer[] renderers = new Renderer[0];
        [SerializeField] private string colorProperty = "_BaseColor";

        private MaterialPropertyBlock _block;
        private Color[][] _baseColors;
        private int _colorId;
        private Color _color;
        private float _duration;
        private float _remaining;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _colorId = Shader.PropertyToID(colorProperty);

            if (renderers == null || renderers.Length == 0)
            {
                var found = new System.Collections.Generic.List<Renderer>();
                foreach (Renderer r in GetComponentsInChildren<Renderer>())
                    if (r is MeshRenderer || r is SkinnedMeshRenderer) found.Add(r);
                renderers = found.ToArray();
            }

            _baseColors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                _baseColors[i] = new Color[materials.Length];
                for (int m = 0; m < materials.Length; m++)
                    _baseColors[i][m] = materials[m] != null && materials[m].HasProperty(_colorId)
                        ? materials[m].GetColor(_colorId)
                        : Color.white;
            }
        }

        public void Flash(Color color, float duration)
        {
            _color = color;
            _duration = _remaining = Mathf.Max(0.01f, duration);
        }

        private void LateUpdate()
        {
            if (_duration <= 0f) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _duration = 0f;
                Apply(0f);
                return;
            }

            // Hold full strength for the first half, then fade out fast.
            float t = Mathf.Clamp01(_remaining / _duration * 2f);
            Apply(t * t);
        }

        private void Apply(float amount)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null) continue;
                for (int m = 0; m < _baseColors[i].Length; m++)
                {
                    _block.Clear();
                    if (amount > 0f) _block.SetColor(_colorId, Color.Lerp(_baseColors[i][m], _color, amount));
                    target.SetPropertyBlock(_block, m);
                }
            }
        }
    }
}
