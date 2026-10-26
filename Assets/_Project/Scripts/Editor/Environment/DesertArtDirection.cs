using Conquest.Environment;
using UnityEditor;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// The project-wide Sable-style look: toon materials for the characters, the ink-line
    /// pass on the URP renderers, and a reusable desert atmosphere prefab (sun, sky, haze).
    /// </summary>
    public static class DesertArtDirection
    {
        public const string RenderingFolder = "Assets/_Project/Art/Rendering";
        public const string OutlineMaterialPath = RenderingFolder + "/Outline.mat";
        public const string SkyMaterialPath = RenderingFolder + "/Sky_Desert.mat";
        public const string AtmospherePrefabPath = "Assets/_Project/Prefabs/Environment/Desert_Atmosphere.prefab";

        public static void ApplyProjectLook(string characterMaterialFolder)
        {
            ToonShading.ConvertFolder(characterMaterialFolder);
            Material outline = LoadOrCreate(OutlineMaterialPath, "Conquest/Outline Fullscreen");
            if (outline != null)
            {
                outline.SetColor("_LineColor", new Color(0.18f, 0.1f, 0.12f, 0.9f));
                outline.SetFloat("_Thickness", 1.2f);
                outline.SetFloat("_DepthThreshold", 0.06f);
                outline.SetFloat("_NormalThreshold", 0.5f); // above a 45 degree bevel, so sub-pixel bevels never draw broken lines
                EditorUtility.SetDirty(outline);
            }

            OutlinePassInstaller.Install(outline);
        }

        public static GameObject BuildAtmospherePrefab()
        {
            Material sky = LoadOrCreate(SkyMaterialPath, "Conquest/Sky Gradient");
            if (sky != null)
            {
                // the concept's saturated blue, fading into the same lavender haze as the fog
                sky.SetColor("_ZenithColor", new Color(0.33f, 0.56f, 0.9f));
                sky.SetColor("_HorizonColor", new Color(0.8f, 0.79f, 0.9f));
                sky.SetColor("_GroundColor", new Color(0.7f, 0.52f, 0.5f));
                sky.SetFloat("_Exponent", 0.55f);
                sky.SetFloat("_CloudCover", 0.58f);
                sky.SetFloat("_CloudScale", 2.4f);
                EditorUtility.SetDirty(sky);
            }

            var root = new GameObject("Desert_Atmosphere");
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root.transform, false);
            sunGo.transform.rotation = Quaternion.Euler(50f, -40f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            var atmosphere = root.AddComponent<AtmosphereSettings>();
            EditorAssetUtility.Set(atmosphere, "skybox", sky);
            EditorAssetUtility.Set(atmosphere, "sun", sun);

            EditorAssetUtility.EnsureFolder(System.IO.Path.GetDirectoryName(AtmospherePrefabPath)?.Replace('\\', '/'));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AtmospherePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material LoadOrCreate(string path, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"Conquest: shader '{shaderName}' not found.");
                return null;
            }

            EditorAssetUtility.EnsureFolder(RenderingFolder);
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
