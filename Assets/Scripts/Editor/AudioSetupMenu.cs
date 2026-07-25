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

            // UI 버튼 클릭
            ("ui_click_wooden", "Assets/Audio/OneShot/UI/UI_Click_wooden.wav", false, 1f),

            // 카드별 전용 효과음 (placeholder). 사운드 담당자가 아래 경로에 파일만 넣으면
            // Register Audio Clips To Library 를 다시 눌러 자동 등록된다. 파일이 없으면
            // 경고만 찍고 건너뛰므로 지금 당장 없어도 안전하다.
            ("card_tempo_up",      "Assets/Audio/OneShot/Cards/Card_tempo_up.wav",     false, 1f),
            ("card_response_call", "Assets/Audio/OneShot/Cards/Card_ResponseCall.wav", false, 1f),
            ("card_hands_up",      "Assets/Audio/OneShot/Cards/Card_HandsUp.wav",      false, 1f),
            ("card_pass_mic",      "Assets/Audio/OneShot/Cards/Card_PassMic.wav",      false, 1f),
            ("card_open_mosh_pit", "Assets/Audio/OneShot/Cards/Card_OpenMoshPit.wav",  false, 1f),
            ("card_draw_two",      "Assets/Audio/OneShot/Cards/Card_DrawTwo.wav",      false, 1f),
            ("card_reroll_hand",   "Assets/Audio/OneShot/Cards/Card_RerollHand.wav",   false, 1f),
        };

        /// <summary>
        /// CardDefinition.id → SoundLibrary sfxId 매핑. 카드별로 다른 소리를 내고 싶을 때 이 표만 고치면 된다.
        /// guitar_solo 는 이미 클립이 있어 바로 연결되고, 나머지는 위 DefaultSounds 의 placeholder 클립이
        /// 채워지는 순간 Assign Card Sfx Overrides 메뉴로 자동 연결된다.
        /// </summary>
        static readonly (string cardId, string sfxId)[] CardSfxMap =
        {
            ("tempo_up",      "card_tempo_up"),
            ("response_call", "card_response_call"),
            ("hands_up",      "card_hands_up"),
            ("pass_mic",      "card_pass_mic"),
            ("guitar_solo",   "guitar_solo"),
            ("open_mosh_pit", "card_open_mosh_pit"),
            ("draw_two",      "card_draw_two"),
            ("reroll_hand",   "card_reroll_hand"),
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
            var system = EnsureComponent<CrowdAmbienceSystem>(go);
            SetObjectField(system, "config", config);

            // 옵션 UI 가 나오기 전까지 키보드로 확인할 수 있는 디버그 입력 ([ ] - = M T)
            if (go.GetComponent<CrowdAmbienceDebugInput>() == null) Undo.AddComponent<CrowdAmbienceDebugInput>(go);

            // 무대 BGM (씬 시작과 동시에 big_rock 페이드인)
            if (go.GetComponent<StageBgmPlayer>() == null) Undo.AddComponent<StageBgmPlayer>(go);

            // 카드 사용 효과음 (CardSelected 이벤트 구독 → guitar_stroke, 카드별 override 는 아래에서 자동 배선)
            var cardSfxPlayer = go.GetComponent<CardSfxPlayer>();
            if (cardSfxPlayer == null) cardSfxPlayer = Undo.AddComponent<CardSfxPlayer>(go);

            // 카드별 효과음 자동 배선 (클립이 실제로 있는 카드만, 기존 수동 설정은 건드리지 않음)
            AssignCardSfxOverrides(cardSfxPlayer, GetOrCreateLibrary());

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

            int added = 0, missing = 0, resynced = 0;
            foreach (var (id, clipPath, loop, volume) in DefaultSounds)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                int idx = FindEntryIndex(list, id);

                if (idx >= 0)
                {
                    // 이미 등록된 id → 볼륨/피치 등 수동 튜닝은 보존한다.
                    // 단, 클립 참조가 끊어져 있으면(파일을 지우고 새로 만들어 guid 가 바뀐 경우 등)
                    // 표의 경로로 다시 연결해준다. 유효한 클립이 이미 있으면 절대 덮어쓰지 않는다
                    // (팀원이 다른 용도로 의도적으로 바꿔둔 경우일 수 있으므로).
                    var existingClips = list.GetArrayElementAtIndex(idx).FindPropertyRelative("clips");
                    bool broken = existingClips.arraySize == 0
                                  || existingClips.GetArrayElementAtIndex(0).objectReferenceValue == null;
                    if (broken && clip != null)
                    {
                        existingClips.arraySize = 1;
                        existingClips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                        resynced++;
                    }
                    continue;
                }

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

            Debug.Log($"[Audio] SoundLibrary 갱신: {added}개 추가, {resynced}개 끊어진 참조 복구, {missing}개 클립 없음 ({LibraryPath})");
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

        // ---------------- 카드 효과음 ----------------

        [MenuItem("Tools/Audio/Assign Card Sfx Overrides", false, 21)]
        public static void AssignCardSfxOverridesInScene()
        {
            var library = GetOrCreateLibrary();
            var players = Object.FindObjectsByType<CardSfxPlayer>(FindObjectsSortMode.None);
            if (players.Length == 0)
            {
                Debug.LogWarning("[Audio] 씬에 CardSfxPlayer 가 없습니다. 먼저 Setup Crowd Ambience 를 실행하세요.");
                return;
            }

            foreach (var player in players) AssignCardSfxOverrides(player, library);
        }

        /// <summary>
        /// CardSfxMap 을 기준으로 cardOverrides 를 채운다. 클립이 실제로 등록된 카드만 추가하고,
        /// 이미 override 가 있는 cardId(팀원이 인스펙터에서 손댄 값 포함)는 건드리지 않는다.
        /// </summary>
        static void AssignCardSfxOverrides(CardSfxPlayer player, SoundLibrary library)
        {
            if (player == null) return;

            var so = new SerializedObject(player);
            var list = so.FindProperty("cardOverrides");

            int added = 0, alreadySet = 0, pendingClip = 0;
            foreach (var (cardId, sfxId) in CardSfxMap)
            {
                if (FindCardOverrideIndex(list, cardId) >= 0) { alreadySet++; continue; }

                var entry = library.Find(sfxId);
                if (entry == null || entry.clips == null || entry.clips.Length == 0) { pendingClip++; continue; }

                list.arraySize++;
                var element = list.GetArrayElementAtIndex(list.arraySize - 1);
                element.FindPropertyRelative("cardId").stringValue = cardId;
                element.FindPropertyRelative("sfxId").stringValue = sfxId;
                added++;
            }

            if (added > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(player);
            }

            Debug.Log($"[Audio] {player.name} 카드 효과음 배선: {added}개 추가, {alreadySet}개 이미 설정됨, {pendingClip}개 클립 대기 중");
        }

        static int FindCardOverrideIndex(SerializedProperty list, string cardId)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("cardId").stringValue == cardId) return i;
            return -1;
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
