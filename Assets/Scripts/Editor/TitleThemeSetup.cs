using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 타이틀 화면 메인 테마(Breakdown) 셋업.
    ///
    /// <c>Tools/Audio/Setup Title Theme</c> 한 번이면:
    /// <list type="number">
    /// <item>SoundLibrary 에 <c>breakdown</c> 항목 등록 (없을 때만)</item>
    /// <item>Title 씬에 <c>[TitleBgm]</c> 오브젝트 + <see cref="StageBgmPlayer"/> 배치</item>
    /// <item>씬이 열리자마자 페이드인으로 재생되도록 설정</item>
    /// </list>
    ///
    /// <b>새 런타임 코드를 만들지 않는다.</b> StageBgmPlayer 에 이미 SceneStart 트리거가 있어
    /// 그대로 재사용한다. 곡 교체는 인스펙터의 bgmId 만 바꾸면 된다.
    ///
    /// 이미 있는 것은 덮어쓰지 않는다. 여러 번 실행해도 안전하다.
    /// </summary>
    public static class TitleThemeSetup
    {
        const string BgmObjectName = "[TitleBgm]";
        const string SoundId = "breakdown";
        const string ClipPath = "Assets/Audio/BGM/Breakdown.mp3";
        const string LibraryPath = "Assets/GameJamKit/Resources/SoundLibrary.asset";
        const string TitleSceneName = "Title";

        /// <summary>big_rock(0.615)과 같은 계열로 맞춘 곡별 볼륨. 유저 설정은 BGM 채널이 따로 담당한다.</summary>
        const float TrackVolume = 0.615f;

        [MenuItem("Tools/Audio/Setup Title Theme", false, 11)]
        public static void Setup()
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogError($"[TitleTheme] 곡을 찾지 못했습니다: {ClipPath}");
                return;
            }

            bool registered = EnsureLibraryEntry(clip);

            if (EditorSceneManager.GetActiveScene().name != TitleSceneName)
            {
                Debug.LogWarning(
                    $"[TitleTheme] 현재 열린 씬이 '{TitleSceneName}' 이 아닙니다 " +
                    $"(지금: '{EditorSceneManager.GetActiveScene().name}'). " +
                    "라이브러리 등록만 하고 씬 배치는 건너뜁니다. " +
                    "Title 씬을 연 뒤 다시 실행하세요.");
                return;
            }

            bool placed = EnsureBgmPlayer();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log(
                $"[TitleTheme] 셋업 완료 — Title 메인 테마 '{SoundId}' (Breakdown.mp3)\n" +
                $"  SoundLibrary: {(registered ? "새로 등록" : "이미 있어 유지")} " +
                $"(loop, volume {TrackVolume})\n" +
                $"  씬 배치: {(placed ? "새로 만듦" : "이미 있어 유지")}\n" +
                "  곡별 볼륨은 SoundLibrary 항목에서, 유저 볼륨은 옵션의 BGM 채널에서 조절합니다. " +
                "(씬을 Ctrl+S 로 저장할 것)");
        }

        // ---------------- SoundLibrary 등록 ----------------

        /// <summary>
        /// YAML 을 직접 건드리지 않고 SerializedObject 로 항목을 덧붙인다.
        /// 이미 같은 id 가 있으면 클립만 비어 있을 때 채우고, 볼륨 등 튜닝값은 덮어쓰지 않는다.
        /// </summary>
        static bool EnsureLibraryEntry(AudioClip clip)
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"[TitleTheme] SoundLibrary 를 찾지 못했습니다: {LibraryPath}");
                return false;
            }

            var serialized = new SerializedObject(library);
            SerializedProperty sounds = serialized.FindProperty("sounds");
            if (sounds == null)
            {
                Debug.LogError("[TitleTheme] SoundLibrary 의 'sounds' 필드를 찾지 못했습니다.");
                return false;
            }

            for (int i = 0; i < sounds.arraySize; i++)
            {
                SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").stringValue != SoundId) continue;

                // 이미 등록돼 있다 — 클립이 비어 있을 때만 채운다
                SerializedProperty existingClips = entry.FindPropertyRelative("clips");
                if (existingClips.arraySize == 0 ||
                    existingClips.GetArrayElementAtIndex(0).objectReferenceValue == null)
                {
                    existingClips.arraySize = 1;
                    existingClips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(library);
                }
                return false;
            }

            int index = sounds.arraySize;
            sounds.arraySize = index + 1;
            SerializedProperty added = sounds.GetArrayElementAtIndex(index);

            added.FindPropertyRelative("id").stringValue = SoundId;
            SerializedProperty clips = added.FindPropertyRelative("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            added.FindPropertyRelative("volume").floatValue = TrackVolume;
            added.FindPropertyRelative("pitchMin").floatValue = 1f;
            added.FindPropertyRelative("pitchMax").floatValue = 1f;
            added.FindPropertyRelative("loop").boolValue = true;
            added.FindPropertyRelative("minInterval").floatValue = 0.02f;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return true;
        }

        // ---------------- 씬 배치 ----------------

        static bool EnsureBgmPlayer()
        {
            StageBgmPlayer existing = Object.FindFirstObjectByType<StageBgmPlayer>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                var serializedExisting = new SerializedObject(existing);
                Debug.Log(
                    "[TitleTheme] StageBgmPlayer 가 이미 있습니다 " +
                    $"(bgmId = '{serializedExisting.FindProperty("bgmId").stringValue}'). " +
                    "설정을 덮어쓰지 않았습니다.",
                    existing);
                Selection.activeGameObject = existing.gameObject;
                return false;
            }

            var go = GameObject.Find(BgmObjectName);
            if (go == null)
            {
                go = new GameObject(BgmObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Create Title Bgm");
            }

            var player = EnsureComponent<StageBgmPlayer>(go);
            var serialized = new SerializedObject(player);
            serialized.FindProperty("bgmId").stringValue = SoundId;
            // 타이틀은 공연 상태와 무관하므로 씬이 열리자마자 재생한다
            serialized.FindProperty("startOn").enumValueIndex =
                (int)StageBgmPlayer.StartTrigger.SceneStart;
            serialized.FindProperty("fadeInDuration").floatValue = 2f;
            serialized.FindProperty("stopOnGameOver").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(player);

            Selection.activeGameObject = go;
            return true;
        }
    }
}
