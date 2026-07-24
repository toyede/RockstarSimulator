using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 관객 비주얼 셋업 메뉴. (HypeSetupMenu · AudioSetupMenu 와 같은 방식)
    ///
    /// Tools/Crowd/Setup Crowd Scene 한 번이면:
    ///   1. Assets/Settings/CrowdMoodConfig.asset 생성
    ///   2. Assets/Sprites/Crowd/crowd_low·middle·high 시트에서 스프라이트를 뽑아 상태별로 연결
    ///   3. 씬에 [Crowd] 오브젝트 + CrowdMoodDirector + CrowdSpawner 배치
    /// 까지 끝난다. 이미 있는 것은 덮어쓰지 않으므로 여러 번 실행해도 안전하다.
    /// </summary>
    public static class CrowdSetupMenu
    {
        const string ConfigPath = "Assets/Settings/CrowdMoodConfig.asset";
        const string SpriteFolder = "Assets/Sprites/Crowd";

        /// <summary>상태 이름 → 스프라이트 시트 파일명. 상태를 늘리면 이 표에만 한 줄 추가한다.</summary>
        static readonly (string moodName, string sheet)[] MoodSheets =
        {
            ("Low",    "crowd_low"),
            ("Middle", "crowd_middle"),
            ("High",   "crowd_high"),
        };

        [MenuItem("Tools/Crowd/Setup Crowd Scene", false, 0)]
        public static void SetupScene()
        {
            var config = GetOrCreateConfig();
            AssignSprites(config);

            var go = GameObject.Find("[Crowd]");
            if (go == null)
            {
                go = new GameObject("[Crowd]");
                go.transform.position = new Vector3(0f, -3.2f, 0f); // 무대 앞 바닥쯤. 씬에 맞게 조정
                Undo.RegisterCreatedObjectUndo(go, "Create Crowd");
            }

            var director = go.GetComponent<CrowdMoodDirector>() ?? Undo.AddComponent<CrowdMoodDirector>(go);
            SetObjectField(director, "config", config);

            if (go.GetComponent<CrowdSpawner>() == null) Undo.AddComponent<CrowdSpawner>(go);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("[Crowd] 관객 셋업 완료. Play 후 호응도를 70% 이상으로 올리면 관객이 방방 뜁니다. " +
                      "(씬을 반드시 Ctrl+S 로 저장할 것)");
        }

        // ---------------- 스프라이트 연결 ----------------

        [MenuItem("Tools/Crowd/Reassign Crowd Sprites", false, 20)]
        public static void ReassignSprites()
        {
            var config = AssetDatabase.LoadAssetAtPath<CrowdMoodConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogWarning($"[Crowd] {ConfigPath} 가 없습니다. Setup Crowd Scene 을 먼저 실행하세요.");
                return;
            }
            AssignSprites(config);
        }

        /// <summary>
        /// 시트를 슬라이스한 하위 스프라이트를 전부 읽어 상태별 세트로 넣는다.
        /// 아트가 시트를 다시 슬라이스하면 이 메뉴만 다시 실행하면 된다.
        /// </summary>
        static void AssignSprites(CrowdMoodConfig config)
        {
            var so = new SerializedObject(config);
            var tiers = so.FindProperty("tiers");
            int filled = 0;

            for (int i = 0; i < tiers.arraySize; i++)
            {
                var tier = tiers.GetArrayElementAtIndex(i);
                string moodName = tier.FindPropertyRelative("moodName").stringValue;

                string sheet = MoodSheets.FirstOrDefault(m =>
                    string.Equals(m.moodName, moodName, System.StringComparison.OrdinalIgnoreCase)).sheet;
                if (string.IsNullOrEmpty(sheet)) continue;

                var sprites = LoadSheetSprites($"{SpriteFolder}/{sheet}.png");
                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"[Crowd] '{sheet}.png' 에서 스프라이트를 찾지 못했습니다. " +
                                     "Texture Type = Sprite, Sprite Mode = Multiple 로 슬라이스되어 있는지 확인하세요.");
                    continue;
                }

                var list = tier.FindPropertyRelative("sprites");
                list.arraySize = sprites.Count;
                for (int s = 0; s < sprites.Count; s++)
                    list.GetArrayElementAtIndex(s).objectReferenceValue = sprites[s];

                filled += sprites.Count;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Crowd] 스프라이트 {filled}장을 상태별로 연결했습니다.");
        }

        /// <summary>시트 안의 하위 스프라이트를 이름 끝 숫자 순서로 정렬해 반환한다.</summary>
        static List<Sprite> LoadSheetSprites(string path)
        {
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<Sprite>()
                .Where(sprite => sprite.rect.width >= 32f && sprite.rect.height >= 32f)
                .ToList();

            sprites.Sort((a, b) => TrailingNumber(a.name).CompareTo(TrailingNumber(b.name)));
            return sprites;
        }

        static int TrailingNumber(string name)
        {
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            return i < name.Length && int.TryParse(name.Substring(i), out int n) ? n : 0;
        }

        // ---------------- 콘픽 ----------------

        static CrowdMoodConfig GetOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CrowdMoodConfig>(ConfigPath);
            if (existing != null) return existing; // 튜닝 보호: 수치를 덮어쓰지 않는다

            EnsureFolder(Path.GetDirectoryName(ConfigPath).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<CrowdMoodConfig>(); // 필드 기본값 = low/middle/high
            AssetDatabase.CreateAsset(asset, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Crowd] {ConfigPath} 생성 완료.");
            return asset;
        }

        // ---------------- 공용 헬퍼 ----------------

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void SetObjectField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[Crowd] {target.GetType().Name} 에서 '{fieldName}' 필드를 찾지 못했습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
