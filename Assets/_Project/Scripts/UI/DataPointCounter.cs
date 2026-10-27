using System.Linq;
using Conquest.Core.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace Conquest.UI
{
    /// <summary>
    /// The data point total for any <see cref="ICurrencySource"/>, Dark Souls style: a number in the corner
    /// that counts up to what was just earned, with a "+N" above it that fades after a moment. Losing points
    /// (dying) drains the number quickly instead. The texts are built at runtime.
    /// </summary>
    public sealed class DataPointCounter : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing ICurrencySource (DataPoints). Empty: the first one found.")]
        private MonoBehaviour source;
        [SerializeField] private string label = "DATA";
        [SerializeField] private int fontSize = 34;
        [SerializeField] private Color color = new Color(1f, 0.86f, 0.55f, 1f);
        [SerializeField] private Color gainColor = new Color(1f, 0.72f, 0.25f, 1f);
        [SerializeField, Min(0f), Tooltip("Seconds the +N stays before it is counted in.")]
        private float gainHold = 1.2f;

        private ICurrencySource _source;
        private Text _amount;
        private Text _gain;
        private float _shown;
        private int _pendingGain;
        private float _gainTimer;

        public int ShownAmount => Mathf.RoundToInt(_shown);

        public void Bind(ICurrencySource currency)
        {
            source = currency as MonoBehaviour;
            if (isActiveAndEnabled) Subscribe();
        }

        private void Awake() => Build();
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            if (_source == null) Subscribe();
            if (_source == null)
            {
                source = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(b => b is ICurrencySource);
                Subscribe();
            }
            if (_source != null) _shown = _source.Amount;
            Refresh();
        }

        private void Subscribe()
        {
            Unsubscribe();
            _source = source as ICurrencySource;
            if (_source == null) return;
            _source.AmountChanged += OnChanged;
            _shown = _source.Amount;
        }

        private void Unsubscribe()
        {
            if (_source != null) _source.AmountChanged -= OnChanged;
            _source = null;
        }

        private void OnChanged(int amount, int change)
        {
            if (change > 0)
            {
                _pendingGain += change;
                _gainTimer = gainHold;
            }
            else
            {
                _pendingGain = 0;
                _gainTimer = 0f;
            }
        }

        private void Update()
        {
            if (_source == null) return;

            if (_gainTimer > 0f) _gainTimer -= Time.unscaledDeltaTime;
            else _pendingGain = 0;

            // Hold the total while the +N is showing, then count it in.
            float target = _source.Amount - (_gainTimer > 0f ? _pendingGain : 0);
            float rate = Mathf.Max(Mathf.Abs(target - _shown) * 4f, 60f);
            _shown = Mathf.MoveTowards(_shown, target, rate * Time.unscaledDeltaTime);
            Refresh();
        }

        private void Refresh()
        {
            if (_amount == null) return;
            _amount.text = $"<size={Mathf.RoundToInt(fontSize * 0.5f)}>{label}</size>  {Mathf.RoundToInt(_shown)}";
            bool showGain = _gainTimer > 0f && _pendingGain > 0;
            _gain.text = showGain ? $"+{_pendingGain}" : "";
            Color c = gainColor;
            c.a = showGain ? Mathf.Clamp01(_gainTimer / 0.25f) : 0f;
            _gain.color = c;
        }

        private void Build()
        {
            if (_amount != null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _amount = MakeText("Amount", font, fontSize, color, Vector2.zero);
            _gain = MakeText("Gain", font, Mathf.RoundToInt(fontSize * 0.75f), gainColor, new Vector2(0f, fontSize + 6f));
        }

        private Text MakeText(string name, Font font, int size, Color tint, Vector2 offset)
        {
            Transform old = transform.Find(name);
            if (old != null) Destroy(old.gameObject);

            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(400f, size + 10f);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = tint;
            text.alignment = TextAnchor.LowerRight;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            go.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }
    }
}
