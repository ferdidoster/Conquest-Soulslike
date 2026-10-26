using Conquest.Core.Stats;
using UnityEngine;

namespace Conquest.UI
{
    /// <summary>
    /// Keeps a world-space bar (usually a <see cref="ResourceBar"/> showing health) floating over a
    /// character's head: it faces the camera, keeps a steady size on screen (perspective or orthographic), fades out with distance
    /// and fades away a moment after the resource runs out. Put it on the bar's world-space Canvas,
    /// a child of the character.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class OverheadBar : MonoBehaviour
    {
        [SerializeField, Tooltip("A component implementing IResourceSource, usually the character's Health.")]
        private MonoBehaviour source;
        [SerializeField, Tooltip("Optional. Defaults to the main camera.")]
        private Camera viewCamera;

        [Header("Size")]
        [SerializeField, Tooltip("Width of the bar on screen, as a fraction of the screen height.")]
        private float screenWidth = 0.09f;
        [SerializeField, Tooltip("Never wider than this in the world (metres), so far-off enemies get smaller bars.")]
        private float maxWorldWidth = 1.6f;

        [Header("Visibility")]
        [SerializeField, Tooltip("Metres from the camera at which the bar starts to fade, and is gone " +
                                 "(perspective cameras only; the game's camera sits about 25 m away).")]
        private Vector2 fadeDistance = new Vector2(50f, 60f);
        [SerializeField, Tooltip("Seconds the bar stays after the resource runs out.")]
        private float hideAfterEmpty = 1.2f;
        [SerializeField] private float fadeSpeed = 4f;

        private CanvasGroup _group;
        private ResourcePool _pool;
        private float _emptySince = -1f;

        private void Awake()
        {
            // The prefab keeps the canvas off so the bar doesn't clutter the Scene view outside Play mode.
            GetComponent<Canvas>().enabled = true;
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        // Pools are created in the source's Awake, so look the pool up in Start.
        private void Start() => _pool = (source as IResourceSource)?.Pool;

        private void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null) return;

            Transform view = viewCamera.transform;
            Vector3 toBar = transform.position - view.position;
            float distance = toBar.magnitude;

            // Face the camera like a sprite: parallel to the screen, never skewed.
            transform.rotation = view.rotation;
            float depth = Vector3.Dot(toBar, view.forward);
            float viewHeight = viewCamera.orthographic
                ? 2f * viewCamera.orthographicSize
                : 2f * Mathf.Max(depth, 0.1f) * Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float worldWidth = Mathf.Min(screenWidth * viewHeight, maxWorldWidth);
            float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
            float canvasWidth = Mathf.Max(1f, ((RectTransform)transform).rect.width);
            transform.localScale = Vector3.one * (worldWidth / canvasWidth / Mathf.Max(1e-4f, parentScale));

            float target = viewCamera.orthographic ? 1f : 1f - Mathf.InverseLerp(fadeDistance.x, fadeDistance.y, distance);
            if (depth <= 0f) target = 0f;
            if (_pool != null && _pool.IsEmpty)
            {
                if (_emptySince < 0f) _emptySince = Time.time;
                if (Time.time - _emptySince >= hideAfterEmpty) target = 0f;
            }
            else _emptySince = -1f;

            _group.alpha = Mathf.MoveTowards(_group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
        }
    }
}
