using System.IO;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    internal static class EditorSetupUtility
    {
        public static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static bool SetObjectField(Object target, string fieldName, Object value)
        {
            if (target == null) return false;

            var serialized = new SerializedObject(target);
            bool assigned = SetObjectReference(serialized, fieldName, value);
            if (!assigned) return false;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        public static bool SetObjectReference(
            SerializedObject serialized,
            string fieldName,
            Object value)
        {
            if (serialized == null) return false;

            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogError(
                    $"[Setup] Missing serialized field '{fieldName}' on " +
                    $"{serialized.targetObject.GetType().Name}.",
                    serialized.targetObject);
                return false;
            }

            property.objectReferenceValue = value;
            return true;
        }
    }
}
