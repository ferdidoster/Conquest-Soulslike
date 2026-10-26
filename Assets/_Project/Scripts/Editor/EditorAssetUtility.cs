using System.IO;
using UnityEditor;
using UnityEngine;

namespace Conquest.EditorTools
{
    /// <summary>Small helpers shared by the setup tools.</summary>
    public static class EditorAssetUtility
    {
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Loads an asset or creates and saves a new one.</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Sets a serialized (usually private) field, so components keep their encapsulation.</summary>
        public static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property, target);
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string property, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property, target);
            if (p.propertyType == SerializedPropertyType.LayerMask) p.intValue = value;
            else if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = value;
            else p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = Find(so, property, target);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }

            return null;
        }

        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }

            Debug.LogError($"No free user layer for '{layerName}'.");
            return 0;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private static SerializedProperty Find(SerializedObject so, string property, Object target)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null) throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{property}'.");
            return p;
        }
    }
}
