using System.IO;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    internal static class EditorSetupUtility
    {
        /// <summary>
        /// 컴포넌트를 가져오거나 없으면 붙인다.
        ///
        /// <b>`GetComponent&lt;T&gt;() ?? AddComponent&lt;T&gt;()` 를 쓰면 안 된다.</b>
        /// UnityEngine.Object 는 `==` 만 오버라이드하고 `??` 는 순수 참조 비교를 하므로,
        /// 네이티브 객체가 파괴된 "fake null" 래퍼가 그대로 통과해
        /// MissingComponentException 이 터진다. Unity 의 `==` 로 판정해야 안전하다.
        /// </summary>
        public static T EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go == null) return null;

            T component = go.GetComponent<T>();
            if (component != null) return component; // Unity 의 == 오버로드가 fake null 을 잡아준다

            return Undo.AddComponent<T>(go);
        }

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
