using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// Assets/Sprites/Crowd/SpecialCrowd 밑의 "프레임 파일 하나에 본체 + 잔상/이펙트 조각들이
    /// 자동 슬라이스된" 이동 애니메이션 시트를 SpecialAudience 프리팹의 성향별 클립에 연결한다.
    ///
    /// 프레임 파일은 이미 Sprite Mode = Multiple 로 슬라이스되어 있어야 한다.
    /// 서브 스프라이트 중 면적이 가장 큰 것을 본체로 간주하고 나머지 조각은 버린다.
    /// </summary>
    public static class SpecialAudienceAnimationImportTool
    {
        const string PrefabPath = "Assets/Prefabs/Audience/SpecialAudience.prefab";

        /// <summary>
        /// 폴더 → SpecialAudienceCrowdActor 필드 매핑표. 아트가 새 이동 애니메이션을 주면
        /// 여기에 한 줄만 추가하면 된다.
        /// </summary>
        static readonly (string folder, string fieldName, string clipName)[] Sources =
        {
            ("Assets/Sprites/Crowd/SpecialCrowd/special_chill_run", "chillClip", "Chill_Run"),
            ("Assets/Sprites/Crowd/SpecialCrowd/special_singalong_run", "singalongClip", "Singalong_Run"),
            ("Assets/Sprites/Crowd/SpecialCrowd/special_mosh_run", "moshClip", "Mosh_Run"),
        };

        [MenuItem("Tools/Audience/Import Special Audience Run Animations")]
        public static void ImportAll()
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError(
                    $"[SpecialAudienceAnim] {PrefabPath} 를 찾을 수 없습니다. " +
                    "Tools/Audience/Create Special Audience Prefab 을 먼저 실행하세요.");
                return;
            }

            var actor = prefabAsset.GetComponentInChildren<SpecialAudienceCrowdActor>(true);
            if (actor == null)
            {
                Debug.LogError("[SpecialAudienceAnim] SpecialAudience 프리팹에 SpecialAudienceCrowdActor 가 없습니다.");
                return;
            }

            var so = new SerializedObject(actor);
            int totalFrames = 0;

            foreach (var source in Sources)
            {
                Sprite[] frames = BuildRunFrames(source.folder);
                if (frames == null) continue;

                SerializedProperty clipProp = so.FindProperty(source.fieldName);
                if (clipProp == null)
                {
                    Debug.LogError(
                        $"[SpecialAudienceAnim] SpecialAudienceCrowdActor의 {source.fieldName} 필드를 찾지 못했습니다.");
                    continue;
                }

                WriteClip(clipProp, source.clipName, frames);
                totalFrames += frames.Length;
                Debug.Log(
                    $"[SpecialAudienceAnim] '{source.folder}' → {source.fieldName} : " +
                    $"{frames.Length}프레임 연결.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[SpecialAudienceAnim] 총 {totalFrames}프레임을 SpecialAudience 프리팹에 연결했습니다. " +
                "fps/loop 은 인스펙터에서 각 클립별로 조정 가능합니다.");
        }

        /// <summary>폴더 안의 프레임 파일들을 순서대로 읽어, 프레임마다 면적이 가장 큰 서브 스프라이트(본체)만 골라 모은다.</summary>
        static Sprite[] BuildRunFrames(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"[SpecialAudienceAnim] 폴더가 없습니다: {folderPath}");
                return null;
            }

            List<string> framePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => TrailingNumber(Path.GetFileNameWithoutExtension(path)))
                .ToList();

            if (framePaths.Count == 0)
            {
                Debug.LogWarning($"[SpecialAudienceAnim] '{folderPath}' 에 프레임 이미지가 없습니다.");
                return null;
            }

            var frames = new List<Sprite>(framePaths.Count);

            foreach (string framePath in framePaths)
            {
                List<Sprite> subSprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(framePath)
                    .OfType<Sprite>()
                    .ToList();

                if (subSprites.Count == 0)
                {
                    Debug.LogWarning(
                        $"[SpecialAudienceAnim] '{framePath}' 가 슬라이스되어 있지 않습니다 " +
                        "(Texture Type = Sprite, Sprite Mode = Multiple 로 슬라이스 필요).");
                    return null;
                }

                Sprite body = subSprites
                    .OrderByDescending(s => s.rect.width * s.rect.height)
                    .First();
                frames.Add(body);
            }

            return frames.ToArray();
        }

        static void WriteClip(SerializedProperty clipProperty, string clipName, Sprite[] frames)
        {
            clipProperty.FindPropertyRelative("clipName").stringValue = clipName;
            clipProperty.FindPropertyRelative("fps").floatValue = 12f;
            clipProperty.FindPropertyRelative("loop").boolValue = true;
            clipProperty.FindPropertyRelative("pingPong").boolValue = false;

            SerializedProperty framesProp = clipProperty.FindPropertyRelative("frames");
            framesProp.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
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
