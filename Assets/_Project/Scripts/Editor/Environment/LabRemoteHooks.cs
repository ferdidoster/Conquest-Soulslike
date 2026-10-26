using System.IO;
using Conquest.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Marker files in the project root for the underground lab, picked up while the editor is in
    /// the background (next to the ones in <see cref="ConquestRemoteHooks"/>):
    ///   ConquestLabBuild.pending     rebuilds the lab kit prefabs, Level_LabDemo and the Lab_Demo scene
    ///   ConquestLabKit.pending       rebuilds only the kit prefabs and the level prefab
    ///   ConquestLabCapture.pending   renders reference shots of the lab into Captures/Lab
    /// The same captures are available from Conquest > Capture Lab Shots.
    /// </summary>
    [InitializeOnLoad]
    internal static class LabRemoteHooks
    {
        private const string BuildMarker = "ConquestLabBuild.pending";
        private const string KitMarker = "ConquestLabKit.pending";
        private const string CaptureMarker = "ConquestLabCapture.pending";
        private const string CaptureFolder = "Captures/Lab";

        private static double _nextPoll;
        private static float _isoYaw = 45f;

        static LabRemoteHooks() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll) return;
            _nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            // the shared refresh marker goes first, so new scripts are compiled before a build
            if (File.Exists("ConquestRefresh.pending")) return;

            if (File.Exists(BuildMarker))
            {
                File.Delete(BuildMarker);
                LabKitBuilder.BuildScene();
                return;
            }

            if (File.Exists(KitMarker))
            {
                File.Delete(KitMarker);
                LabKitBuilder.Build();
                return;
            }

            if (File.Exists(CaptureMarker))
            {
                string[] extra = File.ReadAllLines(CaptureMarker);
                File.Delete(CaptureMarker);
                CaptureLabShots(extra);
            }
        }

        [MenuItem("Conquest/Capture Lab Shots", priority = 21)]
        public static void CaptureLabShotsMenu() => CaptureLabShots(null);

        /// <summary>
        /// Renders the standard shots. Extra lines in the marker file add custom ones:
        /// "name iso x y z orthoSize" or "name persp px py pz tx ty tz fov" (level space), and
        /// "open Gate_Id" opens a gate (instantly) before rendering, and
        /// "section Hub" shows only that section (as in play mode) for the shots after it, and
        /// "yaw 135" turns the isometric shots after it.
        /// </summary>
        public static void CaptureLabShots(string[] extra)
        {
            if (EditorSceneManager.GetActiveScene().path != LabKitBuilder.ScenePath)
            {
                if (!File.Exists(LabKitBuilder.ScenePath))
                {
                    Debug.LogWarning("Conquest: no lab scene yet. Run Conquest > Build Lab Demo first.");
                    return;
                }

                EditorSceneManager.OpenScene(LabKitBuilder.ScenePath, OpenSceneMode.Single);
            }

            GameObject levelGo = GameObject.Find(LabKitBuilder.LevelName);
            Transform level = levelGo != null ? levelGo.transform : null;
            Directory.CreateDirectory(CaptureFolder);

            bool custom = false;
            _isoYaw = 45f;
            LevelSections sections = levelGo != null ? levelGo.GetComponent<LevelSections>() : null;
            var moved = new System.Collections.Generic.List<(Transform, Vector3)>();
            if (extra != null)
                foreach (string raw in extra)
                {
                    string[] p = raw.Trim().Split(' ');
                    if (p.Length == 2 && p[0] == "section" && sections != null)
                    {
                        if (p[1] == "all") sections.ShowAll();
                        else sections.Show(p[1], Quaternion.Euler(35f, _isoYaw, 0f) * Vector3.forward);
                    }
                    else if (p.Length == 4 && p[0] == "sight" && sections != null)
                    {
                        // as if the player stood at this level point: cut what blocks the iso camera's view of it
                        Vector3 at = level != null ? level.TransformPoint(V(p, 1)) : V(p, 1);
                        Quaternion view = Quaternion.Euler(35f, _isoYaw, 0f);
                        sections.CutLineOfSight(at - view * Vector3.forward * 25f, at + Vector3.up * 1.1f);
                    }
                    else if (p.Length == 2 && p[0] == "yaw")
                    {
                        // turns the isometric shots after it (and the cut-away walls with them)
                        _isoYaw = F(p[1]);
                        if (sections != null && sections.Current != null)
                            sections.RefreshCutaway(Quaternion.Euler(35f, _isoYaw, 0f) * Vector3.forward);
                    }
                    else if (p.Length == 2 && p[0] == "open")
                    {
                        foreach (Gate gate in Object.FindObjectsByType<Gate>())
                            if (gate.Id == p[1] || p[1] == "all")
                                OpenInEditMode(gate, moved); // edit mode has no Awake, so move the leaves by hand
                    }
                    else if (p.Length == 6 && p[1] == "iso")
                    {
                        IsoShot(level, p[0], V(p, 2), F(p[5]));
                        custom = true;
                    }
                    else if (p.Length == 9 && p[1] == "persp")
                    {
                        Shot(level, p[0], V(p, 2), V(p, 5), F(p[8]));
                        custom = true;
                    }
                }

            if (!custom)
            {
                // Level space (Unity): hub at the origin, north (+Z) up the sketch, the lab to the east (+X).
                IsoShot(level, "iso_gameplay", new Vector3(-6f, 1f, 0f), 6.5f);
                IsoShot(level, "iso_hub", new Vector3(0f, 1f, 0f), 11f);
                IsoShot(level, "iso_corridor", new Vector3(18f, 1f, 2f), 9f);
                IsoShot(level, "iso_lab", new Vector3(52f, 1f, 0f), 19f);
                IsoShot(level, "iso_overview", new Vector3(6f, 1f, 0f), 50f);
                IsoShot(level, "iso_sleeping", new Vector3(-26f, 1f, 26f), 13f);
                IsoShot(level, "iso_technical", new Vector3(16f, 1f, 33f), 13f);
                IsoShot(level, "iso_kitchen", new Vector3(-16f, 1f, -32f), 13f);
                IsoShot(level, "iso_bathroom", new Vector3(-38f, 1f, -14f), 11f);
                IsoShot(level, "iso_storage", new Vector3(16f, 1f, -34f), 13f);
                Shot(level, "hero_hub", new Vector3(-7.6f, 1.8f, -2.5f), new Vector3(2f, 5.5f, 1.5f), 62f);
                Shot(level, "hero_lab", new Vector3(30f, 2.2f, -4f), new Vector3(52f, 4.5f, 0f), 58f);
                Shot(level, "hero_corridor", new Vector3(10.5f, 1.8f, -1.5f), new Vector3(27f, 2.4f, 0f), 60f);
            }

            foreach ((Transform leaf, Vector3 shut) in moved) leaf.localPosition = shut;
            if (sections != null) sections.ShowAll();
            Debug.Log($"Conquest: lab shots written to {Path.GetFullPath(CaptureFolder)}.");
        }

        private static void OpenInEditMode(Gate gate, System.Collections.Generic.List<(Transform, Vector3)> moved)
        {
            var so = new SerializedObject(gate);
            float travel = so.FindProperty("travel").floatValue;
            foreach (Transform child in gate.GetComponentsInChildren<Transform>(true))
            {
                Vector3 dir;
                if (child.name.EndsWith("_Leaf_L")) dir = Vector3.left;
                else if (child.name.EndsWith("_Leaf_R")) dir = Vector3.right;
                else if (child.name.EndsWith("_Leaf_Up")) dir = Vector3.up;
                else if (child.name.EndsWith("_Leaf_Down")) dir = Vector3.down;
                else continue;
                moved.Add((child, child.localPosition));
                child.localPosition += child.parent.InverseTransformVector(gate.transform.TransformVector(dir * travel));
            }
        }

        private static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        private static Vector3 V(string[] p, int i) => new Vector3(F(p[i]), F(p[i + 1]), F(p[i + 2]));

        private static void Shot(Transform level, string name, Vector3 localPos, Vector3 localTarget, float fov)
        {
            Vector3 pos = level != null ? level.TransformPoint(localPos) : localPos;
            Vector3 target = level != null ? level.TransformPoint(localTarget) : localTarget;
            Render(name, pos, Quaternion.LookRotation(target - pos), fov, 0f);
        }

        /// <summary>Matches the isometric Cinemachine rig: 35 degree pitch, 45 degree yaw, far back.</summary>
        private static void IsoShot(Transform level, string name, Vector3 localTarget, float orthoSize)
        {
            Vector3 target = level != null ? level.TransformPoint(localTarget) : localTarget;
            Quaternion rotation = Quaternion.Euler(35f, _isoYaw, 0f);
            Render(name, target - rotation * Vector3.forward * 60f, rotation, 0f, orthoSize);
        }

        private static void Render(string name, Vector3 position, Quaternion rotation, float fov, float orthoSize)
        {
            const int width = 1600, height = 900;
            var go = new GameObject("Capture Camera") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetPositionAndRotation(position, rotation);
            var camera = go.AddComponent<Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            if (orthoSize > 0f)
            {
                camera.orthographic = true;
                camera.orthographicSize = orthoSize;
            }
            else camera.fieldOfView = fov;

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine(CaptureFolder, name + ".png"), texture.EncodeToPNG());
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(go);
        }
    }
}
