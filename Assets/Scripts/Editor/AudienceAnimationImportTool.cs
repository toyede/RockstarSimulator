using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// Assets/Sprites/Crowd/Animated 밑의 "프레임 파일 하나에 동물 여러 마리(6x2 그리드)" 시트를
    /// 동물별 SpriteAnimationClip 으로 재조립해 AudienceMember 프리팹에 연결한다.
    ///
    /// 프레임 파일은 이미 Sprite Mode = Multiple 로 6x2 슬라이스되어 있어야 하며,
    /// 슬라이스된 하위 스프라이트 이름은 "..._0" ~ "..._11" (동물 인덱스) 로 끝나야 한다.
    /// </summary>
    public static class AudienceAnimationImportTool
    {
        /// <summary>
        /// 폴더 → (성향, 참여도 단계) 매핑표. 아트가 새 조합을 주면 여기에 한 줄만 추가하면 된다.
        /// AudienceMemberActor 쪽 코드는 그대로다 (CrowdSetupMenu.MoodSheets 와 같은 방식).
        /// </summary>
        static readonly (string folder, CrowdPreference preference, AudienceEngagementStage stage, string clipName)[] Sources =
        {
            ("Assets/Sprites/Crowd/Animated/Singalong/Singalong_Calm", CrowdPreference.Singalong, AudienceEngagementStage.Calm, "Singalong_Calm"),
            ("Assets/Sprites/Crowd/Animated/Singalong/Singalong_Middle", CrowdPreference.Singalong, AudienceEngagementStage.Middle, "Singalong_Middle"),
            ("Assets/Sprites/Crowd/Animated/Singalong/Singalong_Hype", CrowdPreference.Singalong, AudienceEngagementStage.Excited, "Singalong_Excited"),
        };

        [MenuItem("Tools/Audience/Import Animated Species Sheets")]
        public static void ImportAll()
        {
            GameObject prefabAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(AudienceFoundationSetup.MemberPrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError(
                    $"[AudienceAnim] {AudienceFoundationSetup.MemberPrefabPath} 를 찾을 수 없습니다. " +
                    "Tools/Audience/Setup Audience Foundation Assets 를 먼저 실행하세요.");
                return;
            }

            var actor = prefabAsset.GetComponent<AudienceMemberActor>();
            if (actor == null)
            {
                Debug.LogError("[AudienceAnim] AudienceMember 프리팹에 AudienceMemberActor 가 없습니다.");
                return;
            }

            var so = new SerializedObject(actor);
            SerializedProperty listProp = so.FindProperty("animatedVariants");
            if (listProp == null)
            {
                Debug.LogError(
                    "[AudienceAnim] AudienceMemberActor의 animatedVariants 필드를 찾지 못했습니다.");
                return;
            }

            int totalClips = 0;

            foreach (var source in Sources)
            {
                SpriteAnimationClip[] clips = BuildSpeciesClips(source.folder, source.clipName);
                if (clips == null) continue;

                WriteGroup(listProp, source.preference, source.stage, clips);
                totalClips += clips.Length;
                Debug.Log(
                    $"[AudienceAnim] '{source.folder}' → {source.preference}/{source.stage} : " +
                    $"동물 {clips.Length}종 연결.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[AudienceAnim] 총 {totalClips}개 동물별 애니메이션 세트를 AudienceMember 프리팹에 연결했습니다. " +
                "fps/loop 은 인스펙터에서 각 클립별로 조정 가능합니다.");
        }

        /// <summary>폴더 안의 프레임 파일들을 프레임 순서대로 읽어, 동물 인덱스별 프레임 시퀀스로 재조립한다.</summary>
        static SpriteAnimationClip[] BuildSpeciesClips(string folderPath, string clipNamePrefix)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[AudienceAnim] 폴더가 없습니다: {folderPath}");
                return null;
            }

            List<string> framePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => TrailingNumber(Path.GetFileNameWithoutExtension(path)))
                .ToList();

            if (framePaths.Count == 0)
            {
                Debug.LogWarning($"[AudienceAnim] '{folderPath}' 에 프레임 이미지가 없습니다.");
                return null;
            }

            // 동물 인덱스 -> 프레임 순서대로 모은 스프라이트 목록
            var bySpecies = new List<List<Sprite>>();

            foreach (string framePath in framePaths)
            {
                List<Sprite> subSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(framePath)
                    .OfType<Sprite>()
                    .OrderBy(s => TrailingNumber(s.name))
                    .ToList();

                if (subSprites.Count == 0)
                {
                    Debug.LogWarning(
                        $"[AudienceAnim] '{framePath}' 가 슬라이스되어 있지 않습니다 " +
                        "(Texture Type = Sprite, Sprite Mode = Multiple 로 6x2 슬라이스 필요).");
                    return null;
                }

                for (int i = 0; i < subSprites.Count; i++)
                {
                    if (bySpecies.Count <= i) bySpecies.Add(new List<Sprite>());
                    bySpecies[i].Add(subSprites[i]);
                }
            }

            var clips = new SpriteAnimationClip[bySpecies.Count];
            for (int i = 0; i < bySpecies.Count; i++)
            {
                clips[i] = new SpriteAnimationClip
                {
                    clipName = $"{clipNamePrefix}_{i}",
                    frames = bySpecies[i].ToArray(),
                    fps = 12f,
                    loop = true,
                };
            }
            return clips;
        }

        /// <summary>(성향, 단계) 에 해당하는 목록 항목을 찾아 덮어쓰거나, 없으면 새로 추가한다.</summary>
        static void WriteGroup(
            SerializedProperty listProp,
            CrowdPreference preference,
            AudienceEngagementStage stage,
            SpriteAnimationClip[] clips)
        {
            int index = FindGroupIndex(listProp, preference, stage);
            if (index < 0)
            {
                index = listProp.arraySize;
                listProp.arraySize++;
            }

            SerializedProperty element = listProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("preference").enumValueIndex = (int)preference;
            element.FindPropertyRelative("stage").enumValueIndex = (int)stage;
            WriteClips(element.FindPropertyRelative("variants"), clips);
        }

        static int FindGroupIndex(
            SerializedProperty listProp,
            CrowdPreference preference,
            AudienceEngagementStage stage)
        {
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                if ((CrowdPreference)element.FindPropertyRelative("preference").enumValueIndex == preference &&
                    (AudienceEngagementStage)element.FindPropertyRelative("stage").enumValueIndex == stage)
                    return i;
            }
            return -1;
        }

        static void WriteClips(SerializedProperty arrayProperty, SpriteAnimationClip[] clips)
        {
            arrayProperty.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                SerializedProperty element = arrayProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("clipName").stringValue = clips[i].clipName;
                element.FindPropertyRelative("fps").floatValue = clips[i].fps;
                element.FindPropertyRelative("loop").boolValue = clips[i].loop;
                element.FindPropertyRelative("pingPong").boolValue = clips[i].pingPong;

                SerializedProperty framesProp = element.FindPropertyRelative("frames");
                framesProp.arraySize = clips[i].frames.Length;
                for (int f = 0; f < clips[i].frames.Length; f++)
                    framesProp.GetArrayElementAtIndex(f).objectReferenceValue = clips[i].frames[f];
            }
        }

        /// <summary>이름 끝의 연속된 숫자를 정수로 반환한다 (언더스코어로 끊긴 접두부는 무시).</summary>
        static int TrailingNumber(string name)
        {
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            return i < name.Length && int.TryParse(name.Substring(i), out int n) ? n : 0;
        }
    }
}
