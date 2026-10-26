using Conquest.Core.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace Conquest.UI
{
    /// <summary>
    /// A row of pips for any <see cref="IChargeSource"/> (the nano-injector's cartridges), one per
    /// charge: lit while available, dark once used. Built at runtime, so a different maximum just works.
    /// A pip that is used shrinks out; refilled pips pop back in.
    /// </summary>
    public sealed class ChargeCounter : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing IChargeSource (NanoInjector).")]
        private MonoBehaviour source;
        [SerializeField] private Vector2 pipSize = new Vector2(12f, 26f);
        [SerializeField] private float spacing = 6f;
        [SerializeField] private Color fullColor = new Color(0.3f, 0.68f, 1f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.05f, 0.08f, 0.12f, 0.75f);
        [SerializeField] private Color frameColor = new Color(0f, 0f, 0f, 0.6f);

        private IChargeSource _source;
        private Image[] _pips = new Image[0];
        private float[] _pop = new float[0];
        private int _shown = -1;

        public int LitPips { get; private set; }
        public int PipCount => _pips.Length;

        public void Bind(IChargeSource charges)
        {
            source = charges as MonoBehaviour;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void Start()
        {
            // The source may set its charges in an Awake that ran after our OnEnable.
            if (_source == null) Subscribe();
            else OnChanged(_source.Charges, _source.MaxCharges);
        }

        private void Subscribe()
        {
            Unsubscribe();
            _source = source as IChargeSource;
            if (_source == null) return;
            _source.ChargesChanged += OnChanged;
            Rebuild(_source.MaxCharges);
            OnChanged(_source.Charges, _source.MaxCharges);
        }

        private void Unsubscribe()
        {
            if (_source != null) _source.ChargesChanged -= OnChanged;
            _source = null;
        }

        private void Rebuild(int count)
        {
            foreach (Image pip in _pips)
                if (pip != null) Destroy(pip.transform.parent.gameObject);

            _pips = new Image[count];
            _pop = new float[count];
            for (int i = 0; i < count; i++)
            {
                var frame = new GameObject($"Pip {i + 1}", typeof(RectTransform), typeof(Image));
                frame.transform.SetParent(transform, false);
                var frameRect = (RectTransform)frame.transform;
                frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0f, 0.5f);
                frameRect.sizeDelta = pipSize + new Vector2(4f, 4f);
                frameRect.anchoredPosition = new Vector2(i * (pipSize.x + 4f + spacing), 0f);
                Image frameImage = frame.GetComponent<Image>();
                frameImage.color = frameColor;
                frameImage.raycastTarget = false;

                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(frame.transform, false);
                var fillRect = (RectTransform)fill.transform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = new Vector2(2f, 2f);
                fillRect.offsetMax = new Vector2(-2f, -2f);
                _pips[i] = fill.GetComponent<Image>();
                _pips[i].raycastTarget = false;
            }
            _shown = -1;
        }

        private void OnChanged(int charges, int max)
        {
            if (max != _pips.Length) Rebuild(max);
            for (int i = 0; i < _pips.Length; i++)
            {
                bool lit = i < charges;
                bool wasLit = _shown < 0 || i < _shown;
                if (_shown >= 0 && lit != wasLit) _pop[i] = 1f;
                _pips[i].color = lit ? fullColor : emptyColor;
            }
            _shown = charges;
            LitPips = Mathf.Clamp(charges, 0, _pips.Length);
        }

        private void Update()
        {
            for (int i = 0; i < _pips.Length; i++)
            {
                if (_pop[i] <= 0f) continue;
                _pop[i] = Mathf.MoveTowards(_pop[i], 0f, Time.unscaledDeltaTime * 4f);
                float scale = 1f + 0.45f * Mathf.Sin(_pop[i] * Mathf.PI);
                _pips[i].transform.parent.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
