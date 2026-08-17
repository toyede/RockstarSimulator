#if UNITY_EDITOR
using System.Collections.Generic;
using ContextStage;
using UnityEditor;
using UnityEngine;

namespace ContextStageEditor
{
    public static class AugmentFirstSetSetup
    {
        const string PrefabFolder = "Assets/Prefabs/Augments";
        const string ResourceFolder = "Assets/Resources/Augments";
        const string RewardTableFolder = ResourceFolder + "/RewardTables";
        const string CatalogPath = ResourceFolder + "/AugmentCatalog.asset";

        [MenuItem("Tools/Tour/Setup First Augments", false, 20)]
        public static void Setup()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(ResourceFolder);
            EnsureFolder(RewardTableFolder);

            AugmentDefinition fever = CreateDefinitionIfMissing(
                "Augment_FeverDuration",
                "fever_duration",
                "FEVER EXTENSION",
                AugmentEffectType.FeverDurationSeconds,
                new[] { 1f, 2f, 3f },
                value => $"Fever lasts {value:0} second{(Mathf.Approximately(value, 1f) ? string.Empty : "s")} longer.");

            AugmentDefinition performanceTime = CreateDefinitionIfMissing(
                "Augment_PerformanceDuration",
                "performance_duration",
                "ENCORE TIME",
                AugmentEffectType.PerformanceDurationSeconds,
                new[] { 5f, 7f, 10f },
                value => $"The performance time limit is extended by {value:0} seconds.");

            AugmentDefinition initialAudience = CreateDefinitionIfMissing(
                "Augment_InitialAudience",
                "initial_audience",
                "EARLY CROWD",
                AugmentEffectType.InitialAudienceCount,
                new[] { 1f, 2f, 3f },
                value => $"Start each performance with {value:0} additional audience member{(Mathf.Approximately(value, 1f) ? string.Empty : "s")}.");

            CreateOrUpdateCatalog(new[] { fever, performanceTime, initialAudience });

            // 티어 확률은 아직 미확정이므로 첫 검증 버전에서는 모두 같은 가중치로 둔다.
            CreateRewardTableIfMissing("reward_basic", 1f, 1f, 1f, 1);
            CreateRewardTableIfMissing("reward_advanced", 1f, 1f, 1f, 1);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AugmentFirstSetSetup] 증강 3종, 티어 조합 9개, 보상 테이블 구성을 완료했습니다.");
        }

        static AugmentDefinition CreateDefinitionIfMissing(
            string assetName,
            string augmentId,
            string displayName,
            AugmentEffectType effectType,
            IReadOnlyList<float> values,
            System.Func<float, string> createDescription)
        {
            string path = $"{PrefabFolder}/{assetName}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing.GetComponent<AugmentDefinition>();

            var root = new GameObject(assetName);
            try
            {
                AugmentDefinition definition = root.AddComponent<AugmentDefinition>();
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("augmentId").stringValue = augmentId;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("effectType").enumValueIndex = (int)effectType;

                SerializedProperty tiers = serialized.FindProperty("tiers");
                tiers.arraySize = 3;
                for (int i = 0; i < tiers.arraySize; i++)
                {
                    SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
                    tier.FindPropertyRelative("tier").enumValueIndex = i;
                    tier.FindPropertyRelative("enabled").boolValue = true;
                    tier.FindPropertyRelative("value").floatValue = values[i];
                    tier.FindPropertyRelative("description").stringValue =
                        createDescription(values[i]);
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab == null ? null : prefab.GetComponent<AugmentDefinition>();
        }

        static void CreateOrUpdateCatalog(IReadOnlyList<AugmentDefinition> definitions)
        {
            AugmentCatalog catalog = AssetDatabase.LoadAssetAtPath<AugmentCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AugmentCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("definitions");
            entries.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
                entries.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static void CreateRewardTableIfMissing(
            string tableId,
            float bronzeWeight,
            float silverWeight,
            float goldWeight,
            int rerollsPerSlot)
        {
            string path = $"{RewardTableFolder}/{tableId}.asset";
            if (AssetDatabase.LoadAssetAtPath<AugmentRewardTable>(path) != null)
                return;

            AugmentRewardTable table = ScriptableObject.CreateInstance<AugmentRewardTable>();
            var serialized = new SerializedObject(table);
            serialized.FindProperty("tableId").stringValue = tableId;
            serialized.FindProperty("bronzeWeight").floatValue = bronzeWeight;
            serialized.FindProperty("silverWeight").floatValue = silverWeight;
            serialized.FindProperty("goldWeight").floatValue = goldWeight;
            serialized.FindProperty("rerollsPerSlot").intValue = rerollsPerSlot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(table, path);
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
