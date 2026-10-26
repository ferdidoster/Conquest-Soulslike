using UnityEditor;

namespace Conquest.EditorTools
{
    /// <summary>
    /// Import rules for the generated environment kit: Blender axis fix, flat normals from the
    /// file, no rig or animation, vertex colours kept (they carry face tint and cloth sway).
    /// Materials are swapped for the shared kit materials by <see cref="DesertKitBuilder"/>.
    /// </summary>
    public sealed class EnvironmentModelPostprocessor : AssetPostprocessor
    {
        public const string EnvironmentFolder = "Assets/_Project/Art/Environment/";

        private bool IsEnvironmentModel =>
            assetPath.StartsWith(EnvironmentFolder) && assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsEnvironmentModel) return;

            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.weldVertices = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
