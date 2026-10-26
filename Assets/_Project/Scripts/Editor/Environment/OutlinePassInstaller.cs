using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Adds the ink-line pass to every URP renderer in the project as a built-in Full Screen
    /// Pass Renderer Feature, so it shows up (and can be switched off or tuned) in the renderer's
    /// inspector like any other feature.
    /// </summary>
    public static class OutlinePassInstaller
    {
        public const string FeatureName = "Conquest Outline";

        public static void Install(Material outlineMaterial)
        {
            if (outlineMaterial == null) return;

            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;

                FullScreenPassRendererFeature feature = Find(data);
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                    feature.name = FeatureName;
                    AssetDatabase.AddObjectToAsset(feature, data);
                    data.rendererFeatures.Add(feature);
                    AssetDatabase.SaveAssets();
                    AddToFeatureMap(data, feature);
                    Debug.Log($"Conquest: added the outline pass to {path}.");
                }

                feature.passMaterial = outlineMaterial;
                feature.passIndex = 0;
                feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents;
                feature.requirements = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth |
                                       ScriptableRenderPassInput.Normal;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
        }

        private static FullScreenPassRendererFeature Find(UniversalRendererData data)
        {
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
                if (f is FullScreenPassRendererFeature full && f.name == FeatureName) return full;
            return null;
        }

        /// <summary>The renderer keeps a parallel list of sub-asset ids; keep it in sync.</summary>
        private static void AddToFeatureMap(UniversalRendererData data, ScriptableRendererFeature feature)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId)) return;
            var so = new SerializedObject(data);
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            // Saving may already have let URP repair the map.
            if (map == null || map.arraySize >= data.rendererFeatures.Count) return;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
