using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Conquest.Environment
{
    /// <summary>
    /// A door or gate with sliding leaves. Event gates (the dotted lines on the level sketch) stay
    /// closed until a script opens them; auto doors open while a character stands in their sensor.
    ///
    ///   Gate.Open("Gate_Lab");                 // by id, from anywhere
    ///   GetComponent&lt;Gate&gt;().Open();            // or on a reference
    ///   gate.Opened.AddListener(...);          // react when it finishes opening
    ///
    /// Leaves are child objects named *_Leaf_L / *_Leaf_R (slide sideways), *_Leaf_Up or
    /// *_Leaf_Down; their colliders move with them, so an open gate is passable. The status light
    /// material and the gate's lights switch from red to green while it is open (or unlocked).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Gate : MonoBehaviour
    {
        private static readonly Dictionary<string, Gate> Registry = new Dictionary<string, Gate>();

        [Tooltip("Name used by Gate.Open(id) / Gate.Find(id). Leave empty for anonymous doors.")]
        [SerializeField] private string gateId;
        [SerializeField] private bool startsOpen;
        [Tooltip("Open while a character is inside the sensor trigger (sliding doors).")]
        [SerializeField] private bool autoOpen;
        [Tooltip("A locked gate ignores its sensor and shows red.")]
        [SerializeField] private bool locked;

        [Header("Motion")]
        [SerializeField] private float travel = 2f;
        [SerializeField] private float openSeconds = 1.2f;
        [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float autoCloseDelay = 0.6f;

        [Header("Status")]
        [SerializeField] private Material closedStatus;
        [SerializeField] private Material openStatus;
        [SerializeField] private Color closedLight = new Color(1f, 0.23f, 0.19f);
        [SerializeField] private Color openLight = new Color(0.3f, 1f, 0.6f);
        [SerializeField] private Light[] statusLights = new Light[0];

        [Header("Sound (optional)")]
        [SerializeField] private AudioClip moveSound;

        [Header("Events")]
        [SerializeField] private UnityEvent opened = new UnityEvent();
        [SerializeField] private UnityEvent closed = new UnityEvent();

        private readonly List<(Transform leaf, Vector3 shut, Vector3 open)> _leaves =
            new List<(Transform, Vector3, Vector3)>();
        private Renderer[] _renderers;
        private float _t;           // 0 = shut, 1 = open
        private bool _target;
        private int _occupants;
        private float _emptySince;
        private AudioSource _audio;

        public string Id => gateId;
        public bool IsOpen => _target;
        public bool IsFullyOpen => _t >= 1f;
        public bool IsLocked => locked;
        public bool AutoOpens => autoOpen;
        public UnityEvent Opened => opened;
        public UnityEvent Closed => closed;

        /// <summary>Opens the gate with this id. Returns false when no such gate is loaded.</summary>
        public static bool Open(string id) => Set(id, true);

        /// <summary>Closes the gate with this id. Returns false when no such gate is loaded.</summary>
        public static bool Close(string id) => Set(id, false);

        public static Gate Find(string id) =>
            !string.IsNullOrEmpty(id) && Registry.TryGetValue(id, out Gate gate) ? gate : null;

        public static IEnumerable<Gate> All => Registry.Values;

        private static bool Set(string id, bool open)
        {
            Gate gate = Find(id);
            if (gate == null)
            {
                Debug.LogWarning($"Gate '{id}' not found.");
                return false;
            }

            gate.SetOpen(open);
            return true;
        }

        private void Awake()
        {
            CacheLeaves();
            _renderers = GetComponentsInChildren<Renderer>();
            _target = startsOpen;
            _t = startsOpen ? 1f : 0f;
            Apply();
            UpdateStatus();
            if (moveSound != null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1f;
                _audio.maxDistance = 30f;
            }
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(gateId)) return;
            if (Registry.TryGetValue(gateId, out Gate other) && other != this && other != null)
                Debug.LogWarning($"Two gates share the id '{gateId}' ({other.name}, {name}).", this);
            Registry[gateId] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(gateId) && Registry.TryGetValue(gateId, out Gate g) && g == this)
                Registry.Remove(gateId);
        }

        [ContextMenu("Open")]
        public void Open() => SetOpen(true);

        [ContextMenu("Close")]
        public void Close() => SetOpen(false);

        public void Toggle() => SetOpen(!_target);

        public void SetOpen(bool open)
        {
            if (_target == open) return;
            _target = open;
            if (_audio != null) _audio.PlayOneShot(moveSound);
            UpdateStatus();
        }

        /// <summary>Snaps to open or shut without animating (e.g. when loading a save).</summary>
        public void SetOpenInstant(bool open)
        {
            _target = open;
            _t = open ? 1f : 0f;
            Apply();
            UpdateStatus();
        }

        public void SetLocked(bool value)
        {
            locked = value;
            if (locked && autoOpen) SetOpen(false);
            UpdateStatus();
        }

        /// <summary>Called by <see cref="GateSensor"/>.</summary>
        internal void SensorChanged(int delta)
        {
            _occupants = Mathf.Max(0, _occupants + delta);
            if (_occupants == 0) _emptySince = Time.time;
        }

        private void Update()
        {
            if (autoOpen && !locked)
            {
                if (_occupants > 0) SetOpen(true);
                else if (_target && Time.time - _emptySince > autoCloseDelay) SetOpen(false);
            }

            float goal = _target ? 1f : 0f;
            if (Mathf.Approximately(_t, goal)) return;
            _t = Mathf.MoveTowards(_t, goal, Time.deltaTime / Mathf.Max(0.05f, openSeconds));
            Apply();
            if (Mathf.Approximately(_t, goal)) (_target ? opened : closed).Invoke();
        }

        private void Apply()
        {
            float k = ease.Evaluate(_t);
            foreach ((Transform leaf, Vector3 shut, Vector3 open) in _leaves)
                if (leaf != null) leaf.localPosition = Vector3.LerpUnclamped(shut, open, k);
        }

        private void CacheLeaves()
        {
            _leaves.Clear();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                Vector3 dir;
                if (child.name.EndsWith("_Leaf_L")) dir = Vector3.left;
                else if (child.name.EndsWith("_Leaf_R")) dir = Vector3.right;
                else if (child.name.EndsWith("_Leaf_Up")) dir = Vector3.up;
                else if (child.name.EndsWith("_Leaf_Down")) dir = Vector3.down;
                else continue;
                Vector3 shut = child.localPosition;
                // travel is in the gate's own space; convert to the leaf parent's space
                Vector3 offset = child.parent.InverseTransformVector(transform.TransformVector(dir * travel));
                _leaves.Add((child, shut, shut + offset));
            }
        }

        private void UpdateStatus()
        {
            bool green = _target || (autoOpen && !locked);
            if (closedStatus != null && openStatus != null && _renderers != null)
            {
                Material from = green ? closedStatus : openStatus;
                Material to = green ? openStatus : closedStatus;
                foreach (Renderer r in _renderers)
                {
                    Material[] mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == from)
                        {
                            mats[i] = to;
                            changed = true;
                        }
                    if (changed) r.sharedMaterials = mats;
                }
            }

            foreach (Light l in statusLights)
                if (l != null) l.color = green ? openLight : closedLight;
        }
    }
}
