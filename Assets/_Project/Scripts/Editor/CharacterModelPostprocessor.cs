using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Import rules for the generated Blender characters: Generic rig, Blender axis fix,
    /// clip names without the "Armature|" prefix, looping for cyclic clips and toon materials.
    /// </summary>
    public sealed class CharacterModelPostprocessor : AssetPostprocessor
    {
        public const string CharactersFolder = "Assets/_Project/Art/Characters/";

        private static readonly HashSet<string> LoopingClips = new HashSet<string>
        {
            "Idle", "Guard_Idle", "Sprint",
            "Walk_F", "Walk_B", "Walk_L", "Walk_R",
            "Run_F", "Run_B", "Run_L", "Run_R",
        };

        private const float GlowIntensity = 2.5f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private bool IsCharacterModel => assetPath.StartsWith(CharactersFolder) && assetPath.EndsWith(".fbx");

        private void OnPreprocessModel()
        {
            if (!IsCharacterModel) return;

            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.resampleCurves = true;
            importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsCharacterModel) return;

            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = ShortName(clip.name);
                clip.loopTime = LoopingClips.Contains(AnimatorControllerBuilder.StateName(clip.name));
            }

            importer.clipAnimations = clips;
        }

        /// <summary>
        /// Imported materials get the flat Sable-style shading, keeping their colours (converted to sRGB).
        /// Materials whose name ends in "Glow" (e.g. the tech-knight's visor and blade edge) also emit their colour.
        /// </summary>
        private void OnPostprocessMaterial(Material material)
        {
            if (!IsCharacterModel) return;
            ToonShading.Convert(material);
            // Blender writes linear colours to the FBX; Unity material colours are authored in sRGB.
            Color color = material.GetColor(BaseColor).gamma;
            material.SetColor(BaseColor, color);
            if (material.name.EndsWith("Glow")) material.SetColor(EmissionColor, color * GlowIntensity);
        }

        /// <summary>"Knight|Attack_1" -> "Attack_1".</summary>
        public static string ShortName(string clipName)
        {
            int bar = clipName.LastIndexOf('|');
            return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
        }
    }
}
