using System.Collections.Generic;
using GameJamKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 기타 사운드 다양화 + 타이틀 시작음 셋업.
    ///
    /// <c>Tools/Audio/Setup Guitar Variations And Start Sound</c> 한 번이면:
    /// <list type="number">
    /// <item><c>guitar_stroke</c> 에 스트로크 4종을 모두 등록 (재생 때마다 랜덤)</item>
    /// <item><c>guitar_solo</c> 에 솔로 3종을 모두 등록 (재생 때마다 랜덤)</item>
    /// <item><c>game_start</c> 항목 등록 후 타이틀 StartButton 이 이 소리를 내도록 교체</item>
    /// </list>
    ///
    /// <b>런타임 코드를 만들지 않는다.</b> <c>SoundEntry.PickClip()</c> 이 이미
    /// clips 배열에서 랜덤으로 뽑으므로, 클립을 더 넣는 것만으로 다양해진다.
    /// 나중에 스트로크를 더 받으면 이 표에 경로만 추가하면 된다.
    ///
    /// 이미 등록된 클립은 다시 넣지 않고, 사람이 바꿔 둔 값은 덮어쓰지 않는다.
    /// </summary>
    public static class GuitarVariationSetup
    {
        const string LibraryPath = "Assets/GameJamKit/Resources/SoundLibrary.asset";
        const string OneShot = "Assets/Audio/OneShot/";

        const string StartSoundId = "game_start";
        const string StartSoundPath = OneShot + "UI/GameStartSound.wav";
        const string ButtonsPrefabPath = "Assets/Prefabs/UI/Buttons.prefab";
        const string StartButtonName = "StartButton";

        /// <summary>이 소리로 바뀌기 전의 값. 이 값일 때만 교체해 사람이 고른 소리를 지키지 않는다.</summary>
        const string PreviousStartSoundId = "ui_click_wooden";

        static readonly string[] StrokeClips =
        {
            OneShot + "Guitar_Stroke.wav",
            OneShot + "Guitar_Stroke2.mp3",
            OneShot + "Guitar_Stroke3.mp3",
            OneShot + "Guitar_Stroke4.wav",
        };

        static readonly string[] SoloClips =
        {
            OneShot + "Guitar_Solo.wav",
            OneShot + "Guitar_Solo2.mp3",
            OneShot + "Guitar_Solo3.mp3",
        };

        [MenuItem("Tools/Audio/Setup Guitar Variations And Start Sound", false, 12)]
        public static void Setup()
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogError($"[GuitarVariation] SoundLibrary 를 찾지 못했습니다: {LibraryPath}");
                return;
            }

            var serialized = new SerializedObject(library);
            SerializedProperty sounds = serialized.FindProperty("sounds");
            if (sounds == null)
            {
                Debug.LogError("[GuitarVariation] SoundLibrary 의 'sounds' 필드를 찾지 못했습니다.");
                return;
            }

            int strokeAdded = AddClipsToEntry(sounds, "guitar_stroke", StrokeClips);
            int soloAdded = AddClipsToEntry(sounds, "guitar_solo", SoloClips);
            bool startRegistered = EnsureEntry(sounds, StartSoundId, StartSoundPath, volume: 0.9f);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            string buttonResult = RetargetStartButton();

            int strokeTotal = CountClips(sounds, "guitar_stroke");
            int soloTotal = CountClips(sounds, "guitar_solo");
            string startState = startRegistered ? "새로 등록" : "이미 있어 유지";

            Debug.Log(
                "[GuitarVariation] 셋업 완료.\n" +
                $"  guitar_stroke: 클립 {strokeAdded}개 추가 (총 {strokeTotal}개 중 랜덤)\n" +
                $"  guitar_solo  : 클립 {soloAdded}개 추가 (총 {soloTotal}개 중 랜덤)\n" +
                $"  {StartSoundId}: {startState}\n" +
                $"  StartButton  : {buttonResult}",
                library);
        }

        // ---------------- SoundLibrary ----------------

        static SerializedProperty FindEntry(SerializedProperty sounds, string id)
        {
            for (int i = 0; i < sounds.arraySize; i++)
            {
                SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").stringValue == id) return entry;
            }
            return null;
        }

        static int CountClips(SerializedProperty sounds, string id)
        {
            SerializedProperty entry = FindEntry(sounds, id);
            return entry != null ? entry.FindPropertyRelative("clips").arraySize : 0;
        }

        /// <summary>
        /// 항목에 클립을 <b>덧붙인다.</b> 이미 들어 있는 클립은 건너뛰므로
        /// 여러 번 실행해도 같은 소리가 중복 등록되지 않는다.
        /// </summary>
        static int AddClipsToEntry(SerializedProperty sounds, string id, string[] paths)
        {
            SerializedProperty entry = FindEntry(sounds, id);
            if (entry == null)
            {
                Debug.LogWarning($"[GuitarVariation] '{id}' 항목이 라이브러리에 없습니다. 건너뜁니다.");
                return 0;
            }

            SerializedProperty clips = entry.FindPropertyRelative("clips");

            var already = new HashSet<Object>();
            for (int i = 0; i < clips.arraySize; i++)
            {
                Object existing = clips.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing != null) already.Add(existing);
            }

            int added = 0;
            for (int i = 0; i < paths.Length; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);
                if (clip == null)
                {
                    Debug.LogWarning($"[GuitarVariation] 클립을 찾지 못했습니다: {paths[i]}");
                    continue;
                }
                if (already.Contains(clip)) continue;

                int index = clips.arraySize;
                clips.arraySize = index + 1;
                clips.GetArrayElementAtIndex(index).objectReferenceValue = clip;
                already.Add(clip);
                added++;
            }

            return added;
        }

        static bool EnsureEntry(SerializedProperty sounds, string id, string clipPath, float volume)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"[GuitarVariation] 클립을 찾지 못했습니다: {clipPath}");
                return false;
            }

            SerializedProperty existing = FindEntry(sounds, id);
            if (existing != null)
            {
                SerializedProperty existingClips = existing.FindPropertyRelative("clips");
                if (existingClips.arraySize == 0 ||
                    existingClips.GetArrayElementAtIndex(0).objectReferenceValue == null)
                {
                    existingClips.arraySize = 1;
                    existingClips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
                }
                return false;
            }

            int index = sounds.arraySize;
            sounds.arraySize = index + 1;
            SerializedProperty added = sounds.GetArrayElementAtIndex(index);

            added.FindPropertyRelative("id").stringValue = id;
            SerializedProperty clips = added.FindPropertyRelative("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            added.FindPropertyRelative("volume").floatValue = volume;
            added.FindPropertyRelative("pitchMin").floatValue = 1f;
            added.FindPropertyRelative("pitchMax").floatValue = 1f;
            added.FindPropertyRelative("loop").boolValue = false;
            added.FindPropertyRelative("minInterval").floatValue = 0.02f;
            return true;
        }

        // ---------------- 타이틀 시작 버튼 ----------------

        /// <summary>
        /// StartButton 의 OnClick 에 이미 걸려 있는 <c>AudioManager.PlaySfx(string)</c> 호출의
        /// 문자열 인자만 바꾼다. 호출을 새로 추가하지 않으므로 클릭음이 두 번 겹치지 않는다.
        /// </summary>
        static string RetargetStartButton()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ButtonsPrefabPath);
            if (root == null) return $"프리팹을 찾지 못함 ({ButtonsPrefabPath})";

            try
            {
                Transform target = FindDeep(root.transform, StartButtonName);
                if (target == null) return $"'{StartButtonName}' 오브젝트를 찾지 못함";

                var button = target.GetComponent<Button>();
                if (button == null) return $"'{StartButtonName}' 에 Button 컴포넌트가 없음";

                var serialized = new SerializedObject(button);
                SerializedProperty calls =
                    serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                if (calls == null) return "OnClick 목록을 찾지 못함";

                for (int i = 0; i < calls.arraySize; i++)
                {
                    SerializedProperty call = calls.GetArrayElementAtIndex(i);
                    if (call.FindPropertyRelative("m_MethodName").stringValue != "PlaySfx") continue;

                    SerializedProperty argument =
                        call.FindPropertyRelative("m_Arguments.m_StringArgument");
                    if (argument == null) continue;

                    if (argument.stringValue == StartSoundId) return "이미 game_start";
                    if (argument.stringValue != PreviousStartSoundId)
                        return $"'{argument.stringValue}' 로 지정돼 있어 덮어쓰지 않음";

                    argument.stringValue = StartSoundId;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, ButtonsPrefabPath);
                    return $"'{PreviousStartSoundId}' → '{StartSoundId}' 로 교체";
                }

                return "PlaySfx 호출을 찾지 못함";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
