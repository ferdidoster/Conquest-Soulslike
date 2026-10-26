using UnityEditor;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>Creates and converts materials for the Conquest/Toon Flat shader.</summary>
    public static class ToonShading
    {
        public const string ShaderName = "Conquest/Toon Flat";

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int Grit = Shader.PropertyToID("_GritStrength");
        private static readonly int VertexColor = Shader.PropertyToID("_VertexColorStrength");
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly int Sway = Shader.PropertyToID("_SwayStrength");
        private static readonly int Cull = Shader.PropertyToID("_Cull");

        public static Shader Shader => Shader.Find(ShaderName);

        /// <summary>
        /// Kit material for a palette entry, created or re-synced from the palette on every build so
        /// tools/Blender/arena/palette.py stays the single source of the colours. To customise a
        /// colour for one level, duplicate the material instead of editing it.
        /// </summary>
        public static Material SyncKitMaterial(string folder, PaletteEntry entry)
        {
            Shader shader = Shader;
            if (shader == null)
            {
                Debug.LogError($"Conquest: shader '{ShaderName}' not found; is Assets/_Project/Art/Shaders imported?");
                return null;
            }

            string path = $"{folder}/{entry.name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EditorAssetUtility.EnsureFolder(folder);
                material = new Material(shader) { name = entry.name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.enableInstancing = true;
            material.SetColor(BaseColor, KitJson.Col(entry.color, Color.white));
            material.SetFloat(Grit, entry.grit);
            material.SetFloat(VertexColor, 1f);
            material.SetFloat(Sway, entry.sway);
            material.SetFloat(Cull, entry.doubleSided ? 0f : 2f);
            material.SetColor(Emission, entry.emissionIntensity > 0f
                ? KitJson.Col(entry.emission, Color.black) * entry.emissionIntensity
                : Color.black);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Switches a Lit (or any) material to the toon shader, keeping its colour and texture.</summary>
        public static bool Convert(Material material)
        {
            Shader toon = Shader;
            if (material == null || toon == null || material.shader == toon) return false;

            Color color = material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : material.color;
            Texture texture = material.HasProperty(BaseMap) ? material.GetTexture(BaseMap) : material.mainTexture;

            material.shader = toon;
            material.SetColor(BaseColor, color);
            material.SetTexture(BaseMap, texture);
            material.SetFloat(Grit, 0.06f);
            material.SetFloat(VertexColor, 0f);
            material.SetColor(Emission, Color.black);
            material.enableInstancing = true;
            return true;
        }

        /// <summary>Converts every material asset in a folder (e.g. the tinted enemy materials).</summary>
        public static void ConvertFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (Convert(material)) EditorUtility.SetDirty(material);
            }
        }
    }
}
