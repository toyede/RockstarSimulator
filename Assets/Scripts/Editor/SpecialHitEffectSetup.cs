using UnityEditor;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 저격 성공 연출 셋업. 요구 타입마다 다른 화면 효과를 붙인다.
    ///
    /// | 요구 | 연출 |
    /// |---|---|
    /// | Chill | 색수차 (ChromaticSplit 프로필) |
    /// | Singalong | 카메라 셰이크 |
    /// | Mosh | 링 디스토션 (내장, 기존 그대로) |
    ///
    /// 셋이 <b>같은 타이밍</b>에 터진다 — `SpecialAudienceCrowdActor` 가 `SpecialHitLanded` 를
    /// 받는 그 지점 하나에서 표를 찾아 재생하기 때문이다.
    ///
    /// 이미 프로필이 연결돼 있으면 덮어쓰지 않는다. 여러 번 실행해도 안전하다.
    /// </summary>
    public static class SpecialHitEffectSetup
    {
        const string ProfileFolder = "Assets/Settings/Effects";
        const string ChillProfilePath = ProfileFolder + "/SpecialHit_Chill_Chromatic.asset";
        const string SpecialAudiencePrefabPath =
            "Assets/Prefabs/Audience/SpecialAudience.prefab";

        [MenuItem("Tools/Special Audience/Setup Special Hit Effects", false, 10)]
        public static void Setup()
        {
            LocalScreenEffectProfile chillProfile = EnsureChillProfile();
            if (chillProfile == null) return;

            if (!WireIntoPrefab(chillProfile)) return;

            AssetDatabase.SaveAssets();
            Selection.activeObject = chillProfile;
            Debug.Log(
                "[SpecialHit] 저격 성공 연출 셋업 완료.\n" +
                "Chill = 색수차 / Singalong = 카메라 셰이크 / Mosh = 링 디스토션.\n" +
                "세기는 SpecialAudience 프리팹의 specialHitEffects 표와 " +
                $"{ChillProfilePath} 에서 조절합니다.",
                chillProfile);
        }

        // ---------------- 색수차 프로필 ----------------

        static LocalScreenEffectProfile EnsureChillProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LocalScreenEffectProfile>(
                ChillProfilePath);
            if (existing != null)
            {
                Debug.Log("[SpecialHit] 색수차 프로필이 이미 있습니다. 값을 덮어쓰지 않았습니다.", existing);
                return existing;
            }

            EnsureFolder(ProfileFolder);

            var profile = ScriptableObject.CreateInstance<LocalScreenEffectProfile>();
            var serialized = new SerializedObject(profile);

            SetEnum(serialized, "effectType", (int)LocalScreenEffectType.ChromaticSplit);
            // 관객 한 명을 중심으로 터지므로 원형이 자연스럽다 (Ring 은 폭발 느낌이라 Mosh 쪽)
            SetEnum(serialized, "shape", (int)DistortionShape.Circle);
            SetFloat(serialized, "feather", 0.28f);
            SetFloat(serialized, "chromaticOffset", 0.02f);
            // Chill 은 조용한 성향이라 짧고 얇게 스치듯 지나간다
            SetFloat(serialized, "duration", 0.45f);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(profile, ChillProfilePath);
            Debug.Log($"[SpecialHit] 색수차 프로필을 만들었습니다: {ChillProfilePath}", profile);
            return profile;
        }

        static void SetEnum(SerializedObject serialized, string field, int value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null) property.enumValueIndex = value;
            else Debug.LogWarning($"[SpecialHit] '{field}' 필드를 찾지 못했습니다.");
        }

        static void SetFloat(SerializedObject serialized, string field, float value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null) property.floatValue = value;
            else Debug.LogWarning($"[SpecialHit] '{field}' 필드를 찾지 못했습니다.");
        }

        // ---------------- 프리팹 연결 ----------------

        static bool WireIntoPrefab(LocalScreenEffectProfile chillProfile)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SpecialAudiencePrefabPath);
            if (root == null)
            {
                Debug.LogError(
                    $"[SpecialHit] 특별 관객 프리팹을 찾지 못했습니다: {SpecialAudiencePrefabPath}");
                return false;
            }

            try
            {
                var actor = root.GetComponentInChildren<SpecialAudienceCrowdActor>(true);
                if (actor == null)
                {
                    Debug.LogError("[SpecialHit] 프리팹에서 SpecialAudienceCrowdActor 를 찾지 못했습니다.");
                    return false;
                }

                var serialized = new SerializedObject(actor);
                SerializedProperty list = serialized.FindProperty("specialHitEffects");
                if (list == null)
                {
                    Debug.LogError("[SpecialHit] 'specialHitEffects' 필드를 찾지 못했습니다.");
                    return false;
                }

                EnsureStageRows(list);
                ApplyRow(list, HeatStage.Chill, chillProfile, cameraShake: false,
                    shakeStrength: 0f, shakeDuration: 0f, builtinFallback: false);
                ApplyRow(list, HeatStage.Singalong, null, cameraShake: true,
                    shakeStrength: 0.35f, shakeDuration: 0.35f, builtinFallback: false);
                ApplyRow(list, HeatStage.Mosh, null, cameraShake: false,
                    shakeStrength: 0f, shakeDuration: 0f, builtinFallback: true);

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, SpecialAudiencePrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>세 요구 타입이 표에 모두 있는지 보장한다. 없는 것만 뒤에 덧붙인다.</summary>
        static void EnsureStageRows(SerializedProperty list)
        {
            var stages = new[] { HeatStage.Chill, HeatStage.Singalong, HeatStage.Mosh };
            for (int s = 0; s < stages.Length; s++)
            {
                if (FindRow(list, stages[s]) != null) continue;

                int index = list.arraySize;
                list.arraySize = index + 1;
                SerializedProperty row = list.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("stage").enumValueIndex = (int)stages[s];
                row.FindPropertyRelative("effectSize").vector2Value = new Vector2(9f, 9f);
                row.FindPropertyRelative("strengthMultiplier").floatValue = 1f;
                row.FindPropertyRelative("durationOverride").floatValue = 0f;
            }
        }

        static SerializedProperty FindRow(SerializedProperty list, HeatStage stage)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty row = list.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("stage").enumValueIndex == (int)stage) return row;
            }
            return null;
        }

        /// <summary>
        /// 한 줄을 채운다. <b>이미 프로필이 연결돼 있으면 건드리지 않는다</b> —
        /// 아트·연출 담당이 다른 프로필로 바꿔 놓았을 수 있다.
        /// </summary>
        static void ApplyRow(
            SerializedProperty list,
            HeatStage stage,
            LocalScreenEffectProfile profile,
            bool cameraShake,
            float shakeStrength,
            float shakeDuration,
            bool builtinFallback)
        {
            SerializedProperty row = FindRow(list, stage);
            if (row == null) return;

            SerializedProperty profileProperty = row.FindPropertyRelative("profile");
            if (profileProperty.objectReferenceValue == null)
                profileProperty.objectReferenceValue = profile;

            row.FindPropertyRelative("cameraShake").boolValue = cameraShake;
            row.FindPropertyRelative("shakeStrength").floatValue = shakeStrength;
            row.FindPropertyRelative("shakeDuration").floatValue = shakeDuration;
            row.FindPropertyRelative("useBuiltinDistortionFallback").boolValue = builtinFallback;

            if (row.FindPropertyRelative("strengthMultiplier").floatValue <= 0f)
                row.FindPropertyRelative("strengthMultiplier").floatValue = 1f;
            if (row.FindPropertyRelative("effectSize").vector2Value == Vector2.zero)
                row.FindPropertyRelative("effectSize").vector2Value = new Vector2(9f, 9f);
        }
    }
}
