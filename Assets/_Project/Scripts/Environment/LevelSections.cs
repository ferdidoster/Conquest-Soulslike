using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Conquest.Environment
{
    /// <summary>
    /// On a level root: splits the level into sections (hub, corridor, rooms ...) so the player can
    /// read where they are. The section the player stands in is shown normally; the rest of the level
    /// stays visible but dimmed (lamps, tubes and gate lights keep glowing, so the way on is still
    /// readable). Walls of the current section that face the camera are cut down to a low base, like
    /// a cut-away drawing, so they never hide the player. Characters in other sections are hidden.
    ///
    /// Where the player is comes from the baked section map (generated levels) or from
    /// <see cref="SectionZone"/> boxes. Which pieces belong where comes from the baked member list,
    /// from direct children named like a section, and from <see cref="SectionMember"/> components.
    /// Walls are the pieces with a <see cref="FadeOccluder"/>; gates and doors are never cut.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSections : MonoBehaviour
    {
        /// <summary>Rendering layer of kit renderers the Toon Flat shader dims by the section map.</summary>
        public const uint SectionLayer = 1u << 8;
        /// <summary>Rendering layer of cut-away walls: everything above the cut height is not drawn.</summary>
        public const uint CutLayer = 1u << 9;
        /// <summary>Rendering layer of whole renderers dimmed (levels without a section map).</summary>
        public const uint DimLayer = 1u << 10;

        [SerializeField] private Transform target;
        [SerializeField] private bool sectionsEnabled = true;

        [Header("Other sections")]
        [Tooltip("Colour multiplier for everything outside the current section.")]
        [SerializeField] private Color dimTint = new Color(0.5f, 0.53f, 0.66f, 1f);
        [Tooltip("How much of their glow lamps and screens keep outside the current section.")]
        [SerializeField, Range(0f, 1f)] private float glowKept = 0.75f;
        [Tooltip("Seconds the light takes to come up in a section the player enters (and to dim in the one left). 0 = at once.")]
        [SerializeField, Min(0f)] private float transitionTime = 0.7f;
        [Tooltip("Hide characters (enemies) that stand in another section.")]
        [SerializeField] private bool hideCharacters = true;

        [Header("Cut-away walls")]
        [SerializeField] private bool cutaway = true;
        [Tooltip("Height (above the level root) cut-away walls are drawn up to.")]
        [SerializeField] private float cutHeight = 0.6f;
        [Tooltip("How directly a wall must face the camera to be cut (1 = only head on, 0 = also side on, below 0 = also the side walls that lean away). Walls facing away further stay up.")]
        [SerializeField, Range(-1f, 1f)] private float cutFacing = -0.35f;
        [Tooltip("Also cut any wall or tall prop (any section) standing between the camera and the player.")]
        [SerializeField] private bool cutLineOfSight = true;
        [SerializeField] private float sightRadius = 0.8f;
        [Tooltip("Seconds a piece stays cut after it stops blocking, so it doesn't flicker.")]
        [SerializeField] private float sightHold = 0.5f;
        [Tooltip("Also cut walls of other sections that stand between the camera and the player's section (the camera side of the player), up to this far away. 0 = off.")]
        [SerializeField] private float cutNearSide = 18f;

        [SerializeField] private string[] sectionNames = Array.Empty<string>();

        [Header("Section map (baked, level space)")]
        [SerializeField] private Vector2 mapOrigin;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private int mapWidth;
        [SerializeField] private int mapHeight;
        [SerializeField, HideInInspector] private string cells = "";

        [Header("Members (baked)")]
        [SerializeField, HideInInspector] private GameObject[] members = Array.Empty<GameObject>();
        [SerializeField, HideInInspector] private int[] memberMasks = Array.Empty<int>();

        public event Action<string> SectionChanged;

        private static readonly int SectionId = Shader.PropertyToID("_ConquestSection");
        private static readonly int PrevId = Shader.PropertyToID("_ConquestSectionPrev");
        private static readonly int BlendId = Shader.PropertyToID("_ConquestSectionBlend");
        private static readonly int MapSizeId = Shader.PropertyToID("_ConquestSectionMapSize");
        private static readonly int HasMapId = Shader.PropertyToID("_ConquestSectionHasMap");
        private static readonly int MapId = Shader.PropertyToID("_ConquestSectionMap");
        private static readonly int MatrixId = Shader.PropertyToID("_ConquestSectionWorldToMap");
        private static readonly int TintId = Shader.PropertyToID("_ConquestSectionTint");
        private static readonly int GlowId = Shader.PropertyToID("_ConquestSectionGlow");
        private static readonly int CutHeightId = Shader.PropertyToID("_ConquestCutHeight");

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<int> _rendererMasks = new List<int>();
        private readonly List<Wall> _walls = new List<Wall>();
        private readonly List<Character> _characters = new List<Character>();
        private SectionZone[] _zones = Array.Empty<SectionZone>();
        private Vector3[] _centres = Array.Empty<Vector3>();
        private Texture2D _mapTexture;
        private int _current = -1;
        private int _previous = -1;
        private float _fade = 1f;          // 0 = still showing _previous, 1 = fully _current
        private Vector3 _viewForward;
        private Vector3 _cutFrom;
        private float _nextCharacterCheck;
        private float _nextCharacterScan;
        private bool _collected;

        private sealed class Wall
        {
            public Transform Root;
            public int Mask;
            public bool IsWall;        // a wall piece (cut by facing); tall props are only cut by line of sight
            public bool FacingCut;
            public float SightUntil = -1f;
            public bool IsCut;
            public Bounds Local;
            public readonly List<Renderer> Renderers = new List<Renderer>();
        }

        private readonly Dictionary<FadeOccluder, Wall> _wallByOccluder = new Dictionary<FadeOccluder, Wall>();
        private readonly Dictionary<Renderer, bool> _rendererCut = new Dictionary<Renderer, bool>();
        private readonly HashSet<Renderer> _wantCut = new HashSet<Renderer>();
        private readonly RaycastHit[] _hits = new RaycastHit[64];

        private sealed class Character
        {
            public Transform Root;
            public Renderer[] Renderers;
            public Canvas[] Canvases;
            public bool Hidden;
        }

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public IReadOnlyList<string> SectionNames => sectionNames;

        /// <summary>Name of the section the player is in, or null when sections are off.</summary>
        public string Current => _current >= 0 && _current < sectionNames.Length ? sectionNames[_current] : null;

        /// <summary>How far the light has come up in the current section, 0..1 (1 = settled).</summary>
        public float TransitionProgress => _fade;

        private bool HasMap => mapWidth > 0 && cells.Length == mapWidth * mapHeight;

        private void Start() => Collect();

        private void LateUpdate()
        {
            if (!sectionsEnabled || target == null) return;
            int section = Locate(target.position);
            if (section >= 0 && section != _current) Show(section);
            if (_fade < 1f) StepFade(Time.deltaTime);

            // the third-person camera turns: re-pick the walls that face it
            Camera view = Camera.main;
            if (view != null && _current >= 0)
            {
                Vector3 forward = Flat(view.transform.forward);
                if (Vector3.Angle(forward, _viewForward) > 3f ||
                    (cutNearSide > 0f && (target.position - _cutFrom).sqrMagnitude > 0.25f))
                    RefreshCutaway(forward);
                if (cutLineOfSight) CutLineOfSight(view.transform.position, target.position + Vector3.up * 1.1f);
            }

            if (!hideCharacters || Time.time < _nextCharacterCheck) return;
            _nextCharacterCheck = Time.time + 0.2f;
            if (Time.time >= _nextCharacterScan)
            {
                // picks up enemies spawned later on
                _nextCharacterScan = Time.time + 2f;
                FindCharacters();
            }

            UpdateCharacters();
        }

        private void OnDisable() => ShowAll();

        private void OnDestroy()
        {
            if (_mapTexture == null) return;
            if (Application.isPlaying) Destroy(_mapTexture);
            else DestroyImmediate(_mapTexture);
        }

        // ------------------------------------------------------------------ locating

        /// <summary>Index of the section at a world position, or -1 outside every section.</summary>
        public int Locate(Vector3 worldPosition)
        {
            if (HasMap)
            {
                int index = MapAt(worldPosition);
                if (index >= 0) return index;
            }

            // zones: the current section wins where zones overlap, so doorways don't flicker
            int found = -1;
            foreach (SectionZone zone in _zones)
            {
                if (zone == null || !zone.Contains(worldPosition)) continue;
                int index = IndexOf(zone.Section);
                if (index == _current) return index;
                if (found < 0) found = index;
            }

            return found;
        }

        private int MapAt(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            int i = Mathf.FloorToInt((local.x - mapOrigin.x) / cellSize);
            int j = Mathf.FloorToInt((local.z - mapOrigin.y) / cellSize);
            if (i < 0 || j < 0 || i >= mapWidth || j >= mapHeight) return -1;
            char c = cells[j * mapWidth + i];
            return c == '.' ? -1 : c - 'A';
        }

        public int IndexOf(string section) => Array.IndexOf(sectionNames, section);

        // ------------------------------------------------------------------ showing

        /// <summary>Makes a section (by name) the current one. Works in edit mode too.</summary>
        public void Show(string section, Vector3? viewForward = null)
        {
            int index = IndexOf(section);
            if (index >= 0) Show(index, viewForward);
        }

        public void Show(int section, Vector3? viewForward = null)
        {
            Collect();
            // in play, fade over from the section just left; in edit mode (and the first time) switch at once
            bool fade = Application.isPlaying && transitionTime > 0f && _current >= 0 && _current != section;
            if (fade && section == _previous && _fade < 1f)
                _fade = 1f - _fade;        // stepped back mid-fade: run the same fade the other way
            else
                _fade = fade ? 0f : 1f;
            _previous = fade ? _current : section;
            _current = section;
            int bit = 1 << section;

            // levels without a map are dimmed per renderer; with a map the shader does it per pixel
            if (!HasMap)
                for (int i = 0; i < _renderers.Count; i++)
                    SetLayer(_renderers[i], DimLayer, _rendererMasks[i] != 0 && (_rendererMasks[i] & bit) == 0);

            SetGlobals();
            Camera view = Camera.main;
            RefreshCutaway(viewForward ?? (view != null ? Flat(view.transform.forward) : _viewForward));
            if (hideCharacters) UpdateCharacters();
            SectionChanged?.Invoke(Current);
        }

        /// <summary>Back to the plain level: nothing dimmed or cut.</summary>
        public void ShowAll()
        {
            if (!_collected) return;
            _current = -1;
            _previous = -1;
            _fade = 1f;
            foreach (Renderer r in _renderers) SetLayer(r, DimLayer, false);
            foreach (Wall wall in _walls)
            {
                wall.FacingCut = false;
                wall.SightUntil = -1f;
            }

            ApplyCuts();
            foreach (Character c in _characters) SetHidden(c, false);
            Shader.SetGlobalFloat(SectionId, 0f);
        }

        private void SetGlobals()
        {
            if (HasMap && _mapTexture == null)
            {
                var bytes = new byte[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                    bytes[i] = cells[i] == '.' ? (byte)0 : (byte)(cells[i] - 'A' + 1);
                _mapTexture = new Texture2D(mapWidth, mapHeight, TextureFormat.R8, false, true)
                {
                    name = "Section Map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _mapTexture.SetPixelData(bytes, 0);
                _mapTexture.Apply(false, true);
            }

            if (HasMap)
            {
                // world -> map uv: x along the level's X, y along its Z
                Matrix4x4 toLocal = transform.worldToLocalMatrix;
                Matrix4x4 toMap = Matrix4x4.identity;
                toMap.SetRow(0, (toLocal.GetRow(0) - new Vector4(0f, 0f, 0f, mapOrigin.x)) / (mapWidth * cellSize));
                toMap.SetRow(1, (toLocal.GetRow(2) - new Vector4(0f, 0f, 0f, mapOrigin.y)) / (mapHeight * cellSize));
                Shader.SetGlobalTexture(MapId, _mapTexture);
                Shader.SetGlobalMatrix(MatrixId, toMap);
                Shader.SetGlobalVector(MapSizeId, new Vector4(mapWidth, mapHeight, 1f / mapWidth, 1f / mapHeight));
            }

            Shader.SetGlobalFloat(HasMapId, HasMap ? 1f : 0f);
            Shader.SetGlobalColor(TintId, dimTint);
            Shader.SetGlobalFloat(GlowId, glowKept);
            Shader.SetGlobalFloat(CutHeightId, transform.position.y + cutHeight);
            Shader.SetGlobalFloat(SectionId, _current + 1f);
            SetFadeGlobals();
        }

        private void StepFade(float deltaTime)
        {
            _fade = transitionTime > 0f ? Mathf.Min(1f, _fade + deltaTime / transitionTime) : 1f;
            SetFadeGlobals();
        }

        private void SetFadeGlobals()
        {
            Shader.SetGlobalFloat(PrevId, _previous + 1f);
            Shader.SetGlobalFloat(BlendId, Mathf.SmoothStep(0f, 1f, _fade));
        }

        // ------------------------------------------------------------------ cut-away walls

        /// <summary>Cuts the walls of the current section that face a camera looking along viewForward.</summary>
        public void RefreshCutaway(Vector3 viewForward)
        {
            _viewForward = Flat(viewForward);
            Vector3 toCamera = -_viewForward;
            Vector3 side = Vector3.Cross(Vector3.up, toCamera);
            _cutFrom = target != null ? target.position : Vector3.zero;
            foreach (Wall wall in _walls)
            {
                bool cut = false;
                if (cutaway && wall.IsWall && _current >= 0 && wall.Root != null && toCamera != Vector3.zero)
                {
                    Vector3 outward = Outward(wall);
                    cut = outward != Vector3.zero && Vector3.Dot(outward, toCamera) > cutFacing;
                    if (!cut && cutNearSide > 0f && target != null)
                    {
                        // walls on the camera side of the player, e.g. of the next rooms over the camera looks across
                        Vector3 d = wall.Root.position - target.position;
                        float along = Vector3.Dot(d, toCamera);
                        cut = along > 1.5f && along < cutNearSide && Mathf.Abs(Vector3.Dot(d, side)) < cutNearSide;
                    }
                }

                wall.FacingCut = cut;
            }

            ApplyCuts();
        }

        /// <summary>Cuts whatever wall or tall prop stands between the camera and the player right now.</summary>
        public void CutLineOfSight(Vector3 cameraPosition, Vector3 to)
        {
            Collect();
            if (!Application.isPlaying) Physics.SyncTransforms();
            Vector3 delta = to - cameraPosition;
            float distance = delta.magnitude;
            if (distance < 0.01f) return;
            int count = Physics.SphereCastNonAlloc(cameraPosition, sightRadius, delta / distance, _hits,
                Mathf.Max(0f, distance - sightRadius), ~0, QueryTriggerInteraction.Ignore);
            float now = Time.time;
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                FadeOccluder occluder = _hits[i].collider.GetComponentInParent<FadeOccluder>();
                if (occluder == null || !_wallByOccluder.TryGetValue(occluder, out Wall wall)) continue;
                if (wall.SightUntil < now) changed = true;
                wall.SightUntil = now + sightHold;
            }

            foreach (Wall wall in _walls)
                if (wall.IsCut && !wall.FacingCut && wall.SightUntil < now)
                    changed = true;
            if (changed) ApplyCuts();
        }

        /// <summary>A renderer is cut while any piece it belongs to (a wall, or the wall it hangs on) is cut.</summary>
        private void ApplyCuts()
        {
            float now = Time.time;
            _wantCut.Clear();
            foreach (Wall wall in _walls)
            {
                wall.IsCut = cutaway && _current >= 0 && (wall.FacingCut || wall.SightUntil >= now);
                if (wall.IsCut)
                    foreach (Renderer r in wall.Renderers) _wantCut.Add(r);
            }

            foreach (Wall wall in _walls)
                foreach (Renderer r in wall.Renderers)
                {
                    if (r == null) continue;
                    bool cut = _wantCut.Contains(r);
                    if (_rendererCut.TryGetValue(r, out bool was) && was == cut) continue;
                    _rendererCut[r] = cut;
                    SetLayer(r, CutLayer, cut);
                }
        }

        /// <summary>One line per wall or tall prop near a point: cut state and the sections on either side.</summary>
        public string Describe(Vector3 near, float radius)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"    view forward used {transform.InverseTransformDirection(_viewForward)}, camera {(Camera.main != null ? transform.InverseTransformDirection(Flat(Camera.main.transform.forward)).ToString() : "-")}, level yaw {transform.eulerAngles.y:0}");
            foreach (Wall wall in _walls)
            {
                if (wall.Root == null || (wall.Root.position - near).sqrMagnitude > radius * radius) continue;
                Vector3 p = wall.Root.position, f = Flat(wall.Root.forward);
                int front = HasMap ? MapAt(p + f * 0.9f) : -1, back = HasMap ? MapAt(p - f * 0.9f) : -1;
                sb.AppendLine($"    {(wall.IsCut ? "cut " : "UP  ")} {(wall.IsWall ? "wall" : "prop")} {wall.Root.name}" +
                              $" at {transform.InverseTransformPoint(p)} fwd {transform.InverseTransformDirection(f)}" +
                              $" front {Name(front)} back {Name(back)} facing {wall.FacingCut}" +
                              $" drawnCut {wall.Renderers.Count(r => r != null && r.transform.IsChildOf(wall.Root) && (r.renderingLayerMask & CutLayer) != 0)}/{wall.Renderers.Count(r => r != null && r.transform.IsChildOf(wall.Root))}");
                if (!wall.IsWall)
                    foreach (Wall owner in _walls)
                        if (owner != wall && owner.Renderers.Count > 0 && wall.Renderers.Count > 0 &&
                            owner.Renderers.Contains(wall.Renderers[0]))
                            sb.AppendLine($"        hangs on {owner.Root.name} at {transform.InverseTransformPoint(owner.Root.position)} bounds {owner.Local.min} {owner.Local.max} cut {owner.IsCut}");
                if (!wall.IsCut) continue;
                var extra = new HashSet<string>();
                foreach (Renderer r in wall.Renderers)
                    if (r != null && !r.transform.IsChildOf(wall.Root))
                    {
                        Transform t = r.transform;
                        while (t.parent != null && t.parent.parent != null && t.parent.parent != transform &&
                               t.parent.parent.parent != transform) t = t.parent;
                        extra.Add($"{t.name} at {transform.InverseTransformPoint(t.position)}");
                    }
                if (extra.Count > 0) sb.AppendLine("        with " + string.Join(", ", extra));
            }

            return sb.ToString();
        }

        private string Name(int i) => i >= 0 && i < sectionNames.Length ? sectionNames[i] : "-";

        /// <summary>Direction from the current section through the wall to the other side, or zero.</summary>
        private Vector3 Outward(Wall wall)
        {
            Vector3 p = wall.Root.position;
            Vector3 f = Flat(wall.Root.forward);
            if (HasMap)
            {
                int front = MapAt(p + f * 0.9f), back = MapAt(p - f * 0.9f);
                if (back == _current && front != _current) return f;
                if (front == _current && back != _current) return -f;
                return Vector3.zero;
            }

            if ((wall.Mask & (1 << _current)) == 0 || _current >= _centres.Length) return Vector3.zero;
            return Vector3.Dot(p - _centres[_current], f) >= 0f ? f : -f;
        }

        private static void SetLayer(Renderer r, uint layer, bool on)
        {
            if (r == null) return;
            r.renderingLayerMask = on ? r.renderingLayerMask | layer : r.renderingLayerMask & ~layer;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.zero;
        }

        // ------------------------------------------------------------------ characters

        private void FindCharacters()
        {
            if (!sectionsEnabled || !hideCharacters) return;
            foreach (CharacterController controller in FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
                if (target == null || !controller.transform.IsChildOf(target.root))
                    Track(controller.transform);
        }

        /// <summary>Hide or show a character (enemy, NPC) by the section it stands in.</summary>
        public void Track(Transform character)
        {
            if (character == null || _characters.Exists(c => c.Root == character)) return;
            _characters.Add(new Character
            {
                Root = character,
                Renderers = character.GetComponentsInChildren<Renderer>(true),
                Canvases = character.GetComponentsInChildren<Canvas>(true),
            });
        }

        private void UpdateCharacters()
        {
            for (int i = _characters.Count - 1; i >= 0; i--)
            {
                Character c = _characters[i];
                if (c.Root == null)
                {
                    _characters.RemoveAt(i);
                    continue;
                }

                int at = Locate(c.Root.position);
                SetHidden(c, hideCharacters && _current >= 0 && at >= 0 && at != _current);
            }
        }

        private static void SetHidden(Character c, bool hidden)
        {
            if (c.Hidden == hidden) return;
            c.Hidden = hidden;
            foreach (Renderer r in c.Renderers)
                if (r != null) r.forceRenderingOff = hidden;
            foreach (Canvas canvas in c.Canvases)
                if (canvas != null) canvas.enabled = !hidden;
        }

        // ------------------------------------------------------------------ setup

        private void Collect()
        {
            if (_collected) return;
            _collected = true;
            var masks = new Dictionary<GameObject, int>();

            for (int i = 0; i < members.Length && i < memberMasks.Length; i++)
                if (members[i] != null) masks[members[i]] = memberMasks[i];

            // hand-built levels: direct children named like a section, plus SectionMember extras
            if (members.Length == 0)
                foreach (Transform child in transform)
                {
                    int index = IndexOf(child.name);
                    if (index < 0) continue;
                    foreach (Transform piece in child)
                        masks[piece.gameObject] = (masks.TryGetValue(piece.gameObject, out int m) ? m : 0) | 1 << index;
                }

            foreach (SectionMember member in GetComponentsInChildren<SectionMember>(true))
            {
                int m = masks.TryGetValue(member.gameObject, out int existing) ? existing : 0;
                foreach (string section in member.Sections)
                {
                    int index = IndexOf(section);
                    if (index >= 0) m |= 1 << index;
                }

                masks[member.gameObject] = m;
            }

            // a renderer reached from several members (nested groups) belongs to all of their sections
            var rendererMasks = new Dictionary<Renderer, int>();
            var sums = new Vector3[sectionNames.Length];
            var counts = new int[sectionNames.Length];
            foreach (KeyValuePair<GameObject, int> pair in masks)
            {
                foreach (Renderer r in pair.Key.GetComponentsInChildren<Renderer>(true))
                    rendererMasks[r] = (rendererMasks.TryGetValue(r, out int m) ? m : 0) | pair.Value;
                for (int s = 0; s < sectionNames.Length; s++)
                    if ((pair.Value & (1 << s)) != 0)
                    {
                        sums[s] += pair.Key.transform.position;
                        counts[s]++;
                    }
            }

            foreach (KeyValuePair<Renderer, int> pair in rendererMasks)
            {
                _renderers.Add(pair.Key);
                _rendererMasks.Add(pair.Value);
            }

            _centres = new Vector3[sectionNames.Length];
            for (int s = 0; s < sectionNames.Length; s++)
                _centres[s] = counts[s] > 0 ? sums[s] / counts[s] : transform.position;

            CollectWalls(masks);
            _zones = GetComponentsInChildren<SectionZone>(true);
            FindCharacters();
        }

        /// <summary>Walls (FadeOccluder pieces) plus what hangs on them: lamps, pipes, screens, lockers.</summary>
        private void CollectWalls(Dictionary<GameObject, int> masks)
        {
            var bounds = new List<Bounds>();
            foreach (FadeOccluder occluder in GetComponentsInChildren<FadeOccluder>(true))
            {
                if (occluder.GetComponent<Gate>() != null) continue;
                var wall = new Wall
                {
                    Root = occluder.transform,
                    Mask = masks.TryGetValue(occluder.gameObject, out int m) ? m : 0,
                    IsWall = occluder.IsWall,
                };
                wall.Renderers.AddRange(occluder.GetComponentsInChildren<Renderer>(true));
                _walls.Add(wall);
                _wallByOccluder[occluder] = wall;
                bounds.Add(LocalBounds(occluder.transform));
                wall.Local = bounds[bounds.Count - 1];
            }

            // things hanging on or standing against a wall are cut with it (tall props too, which
            // are also cut on their own when they block the view)
            foreach (GameObject piece in masks.Keys)
            {
                if (piece == null || piece.GetComponent<Gate>() != null) continue;
                FadeOccluder own = piece.GetComponent<FadeOccluder>();
                if (own != null && _wallByOccluder.TryGetValue(own, out Wall self) && self.IsWall) continue;
                if (piece.name.StartsWith("Door_") || piece.name.StartsWith("Gate_") || piece.name.StartsWith("Floor_")) continue;
                Vector3 p = piece.transform.position;
                for (int w = 0; w < _walls.Count; w++)
                {
                    if (!_walls[w].IsWall) continue;
                    Vector3 local = _walls[w].Root.InverseTransformPoint(p);
                    Bounds b = bounds[w];
                    if (local.x < b.min.x - 0.1f || local.x > b.max.x + 0.1f) continue;
                    if (local.z < b.min.z - 0.9f || local.z > b.max.z + 0.9f) continue;
                    _walls[w].Renderers.AddRange(piece.GetComponentsInChildren<Renderer>(true));
                    break;
                }
            }
        }

        private static Bounds LocalBounds(Transform root)
        {
            // colliders first: in play mode static batching swaps the renderers' meshes for one
            // combined mesh of the whole level, so their bounds say nothing about this piece
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger) continue;
                if (collider is BoxCollider box)
                    Encapsulate(ref b, ref any, root, collider.transform, new Bounds(box.center, box.size));
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
                    Encapsulate(ref b, ref any, root, collider.transform, mesh.sharedMesh.bounds);
            }

            if (any) return b;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (renderer != null && renderer.isPartOfStaticBatch) continue;
                Encapsulate(ref b, ref any, root, filter.transform, filter.sharedMesh.bounds);
            }

            return b;
        }

        private static void Encapsulate(ref Bounds b, ref bool any, Transform root, Transform part, Bounds local)
        {
            Matrix4x4 m = root.worldToLocalMatrix * part.localToWorldMatrix;
            for (int k = 0; k < 8; k++)
            {
                var corner = new Vector3((k & 1) == 0 ? local.min.x : local.max.x, (k & 2) == 0 ? local.min.y : local.max.y,
                    (k & 4) == 0 ? local.min.z : local.max.z);
                Vector3 c = m.MultiplyPoint3x4(corner);
                if (!any) b = new Bounds(c, Vector3.zero);
                else b.Encapsulate(c);
                any = true;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor builders: bakes the section map and the members.</summary>
        public void Bake(string[] names, Vector2 origin, float cell, int width, int height, string map,
            GameObject[] pieces, int[] masks)
        {
            sectionNames = names;
            mapOrigin = origin;
            cellSize = cell;
            mapWidth = width;
            mapHeight = height;
            cells = map ?? "";
            members = pieces;
            memberMasks = masks;
        }
#endif
    }
}
