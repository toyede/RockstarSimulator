using System.IO;
using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 사운드 셋업 메뉴. (HypeSetupMenu 와 같은 방식)
    ///
    /// Tools/Audio/Setup Crowd Ambience 한 번이면:
    ///   1. GameJamKit 매니저([Managers]) 생성            ← 킷 메뉴 재사용
    ///   2. Assets/GameJamKit/Resources/SoundLibrary.asset 생성 + Assets/Audio 클립 자동 등록
    ///   3. Assets/Settings/CrowdAmbienceConfig.asset 생성 (low/middle/high 기본 매핑)
    ///   4. [CrowdAmbience] 오브젝트 배치 + 콘픽 연결
    /// 까지 끝난다. 이미 있는 것은 덮어쓰지 않으므로 여러 번 실행해도 안전하다.
    /// </summary>
    public static class AudioSetupMenu
    {
        const string ConfigPath = "Assets/Settings/CrowdAmbienceConfig.asset";
        const string LibraryPath = "Assets/GameJamKit/Resources/SoundLibrary.asset";

        /// <summary>사운드 ID → 클립 경로 매핑. 새 사운드가 늘어나면 이 표에만 추가하면 된다.</summary>
        static readonly (string id, string clipPath, bool loop, float volume)[] DefaultSounds =
        {
            // BGM (루프). 곡 자체의 밸런스는 여기 volume 으로, 유저 설정은 Bgm 채널 볼륨으로 나눠 잡는다
            ("big_rock",      "Assets/Audio/BGM/Big Rock.mp3",          true,  0.5f),

            // 관객 앰비언스 (루프)
            ("crowd_low",     "Assets/Audio/BGM/crowd_low.wav",         true,  1f),
            ("crowd_middle",  "Assets/Audio/BGM/crowd_middle.wav",      true,  1f),
            ("crowd_high",    "Assets/Audio/BGM/crowd_high.wav",        true,  1f),

            // 원샷 효과음
            ("crowd_mistake", "Assets/Audio/OneShot/Crowd_mistake.wav", false, 1f),
            ("guitar_solo",   "Assets/Audio/OneShot/Guitar_Solo.wav",   false, 1f),
            ("guitar_stroke", "Assets/Audio/OneShot/Guitar_Stroke.wav", false, 1f),
            ("hey_high",      "Assets/Audio/OneShot/Hey_high.wav",      false, 1f),
            ("hey_low",       "Assets/Audio/OneShot/Hey_low.wav",       false, 1f),
        };

        [MenuItem("Tools/Audio/Setup Crowd Ambience", false, 0)]
        public static void SetupScene()
        {
            // 1) 킷 매니저 (AudioManager 포함)
            GameJamKit.EditorTools.GameJamKitMenu.CreateManagers();

            // 2) 사운드 라이브러리 + 클립 등록
            RegisterDefaultSounds();

            // 3) 앰비언스 콘픽
            var config = GetOrCreateConfig();

            // 4) 씬 오브젝트
            var go = GameObject.Find("[CrowdAmbience]");
            if (go == null)
            {
                go = new GameObject("[CrowdAmbience]");
                Undo.RegisterCreatedObjectUndo(go, "Create CrowdAmbience");
            }
            var system = go.GetComponent<CrowdAmbienceSystem>() ?? Undo.AddComponent<CrowdAmbienceSystem>(go);
            SetObjectField(system, "config", config);

            // 옵션 UI 가 나오기 전까지 키보드로 확인할 수 있는 디버그 입력 ([ ] - = M T)
            if (go.GetComponent<CrowdAmbienceDebugInput>() == null) Undo.AddComponent<CrowdAmbienceDebugInput>(go);

            // 무대 BGM (씬 시작과 동시에 big_rock 페이드인)
            if (go.GetComponent<StageBgmPlayer>() == null) Undo.AddComponent<StageBgmPlayer>(go);

            // 카드 사용 효과음 (CardSelected 이벤트 구독 → guitar_stroke)
            if (go.GetComponent<CardSfxPlayer>() == null) Undo.AddComponent<CardSfxPlayer>(go);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("[Audio] 관객 앰비언스 셋업 완료. Play 후 공연을 시작하면 호응도에 따라 " +
                      "crowd_low → middle → high 로 자동 크로스페이드됩니다.");
        }

        // ---------------- 사운드 라이브러리 ----------------

        [MenuItem("Tools/Audio/Register Audio Clips To Library", false, 20)]
        public static void RegisterDefaultSounds()
        {
            var library = GetOrCreateLibrary();
            var so = new SerializedObject(library);
            var list = so.FindProperty("sounds");

            int added = 0, missing = 0;
            foreach (var (id, clipPath, loop, volume) in DefaultSounds)
            {
                if (FindEntryIndex(list, id) >= 0) continue; // 이미 등록됨 → 팀원이 만진 설정을 덮어쓰지 않는다

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[Audio] 클립을 찾지 못해 '{id}' 등록을 건너뜁니다: {clipPath}");
                    missing++;
                    continue;
                }

                list.arraySize++;
                var element = list.GetArrayElementAtIndex(list.arraySize - 1);
                element.FindPropertyRelative("id").stringValue = id;
                element.FindPropertyRelative("volume").floatValue = volume;
                element.FindPropertyRelative("pitchMin").floatValue = 1f;
                element.FindPropertyRelative("pitchMax").floatValue = 1f;
                element.FindPropertyRelative("loop").boolValue = loop;
                element.FindPropertyRelative("minInterval").floatValue = 0.02f;

                var clips = element.FindPropertyRelative("clips");
                clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            library.InvalidateCache();

            Debug.Log($"[Audio] SoundLibrary 갱신: {added}개 추가, {missing}개 클립 없음 ({LibraryPath})");
        }

        static int FindEntryIndex(SerializedProperty list, string id)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id) return i;
            return -1;
        }

        static SoundLibrary GetOrCreateLibrary()
        {
            var existing = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<SoundLibrary>();
            AssetDatabase.CreateAsset(asset, LibraryPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Audio] {LibraryPath} 생성 완료. (Resources 폴더라 AudioManager 가 자동 로드한다)");
            return asset;
        }

        // ---------------- 콘픽 ----------------

        static CrowdAmbienceConfig GetOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CrowdAmbienceConfig>(ConfigPath);
            if (existing != null) return existing; // 튜닝 보호: 수치를 덮어쓰지 않는다

            EnsureFolder(Path.GetDirectoryName(ConfigPath).Replace('\\', '/'));
            var asset = ScriptableObject.CreateInstance<CrowdAmbienceConfig>(); // 필드 기본값 = low/middle/high 매핑
            AssetDatabase.CreateAsset(asset, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Audio] {ConfigPath} 생성 완료.");
            return asset;
        }

    }
}
