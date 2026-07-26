using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 관객 반응 효과음 + 겹쳐 까는 무대 앰비언스 셋업.
    ///
    /// <c>Tools/Audio/Setup Audience Reaction And Ambience</c> 한 번이면:
    /// <list type="number">
    /// <item>기존 <c>[CrowdAmbience]</c> 오브젝트에 두 컴포넌트를 붙이고</item>
    /// <item>점수 구간별 반응 소리(발 구르기·휘파람·헛기침·귀뚜라미·야유)를 채우고</item>
    /// <item>앰비언스 4겹(앰프 잡음·축제 웅성거림·crowd_middle·crowd_high)을 채운다</item>
    /// </list>
    ///
    /// <b>이미 값이 채워져 있으면 덮어쓰지 않는다.</b> 여러 번 실행해도 안전하다.
    /// </summary>
    public static class AudienceReactionAudioSetup
    {
        const string AudioRootName = "[CrowdAmbience]";

        const string OneShotFolder = "Assets/Audio/OneShot/";
        const string BgmFolder = "Assets/Audio/BGM/";

        [MenuItem("Tools/Audio/Setup Audience Reaction And Ambience", false, 10)]
        public static void Setup()
        {
            GameObject root = ResolveAudioRoot();
            if (root == null)
            {
                Debug.LogError(
                    $"[AudienceAudio] '{AudioRootName}' 오브젝트를 찾지 못했습니다. " +
                    "Main 씬을 연 뒤 다시 실행하세요.");
                return;
            }

            bool reactionFilled = SetupReactions(root);
            bool ambienceFilled = SetupAmbience(root);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;

            Debug.Log(
                "[AudienceAudio] 셋업 완료.\n" +
                $"  반응 효과음: {(reactionFilled ? "새로 채움" : "이미 있어 유지")}\n" +
                $"  앰비언스 겹: {(ambienceFilled ? "새로 채움" : "이미 있어 유지")}\n" +
                "  Play 중 인스펙터 ⋮ 메뉴로 각 소리를 미리 들어볼 수 있습니다. " +
                "(씬을 Ctrl+S 로 저장할 것)",
                root);
        }

        static GameObject ResolveAudioRoot()
        {
            var existing = GameObject.Find(AudioRootName);
            if (existing != null) return existing;

            // 이름이 바뀌었을 수 있으니 기존 오디오 컴포넌트가 붙은 오브젝트도 찾아본다
            var bgm = Object.FindFirstObjectByType<StageBgmPlayer>(FindObjectsInactive.Include);
            return bgm != null ? bgm.gameObject : null;
        }

        // ---------------- 관객 반응 효과음 ----------------

        static bool SetupReactions(GameObject root)
        {
            var view = EnsureComponent<AudienceReactionSfx>(root);
            var serialized = new SerializedObject(view);

            SerializedProperty list = serialized.FindProperty("reactions");
            if (list == null)
            {
                Debug.LogError("[AudienceAudio] 'reactions' 필드를 찾지 못했습니다.");
                return false;
            }

            if (list.arraySize > 0)
                return false; // 이미 구성돼 있으면 손대지 않는다

            AudioClip bigStomp = Load(OneShotFolder + "BigStomp.wav");
            AudioClip whistle = Load(OneShotFolder + "Whistle.wav");
            AudioClip cough = Load(OneShotFolder + "cough_sarcastic.wav");
            AudioClip cricket = Load(OneShotFolder + "cricket.wav");
            AudioClip booing = Load(BgmFolder + "crowd_low.wav");

            // 구간 경계는 CardReactionTextUI 의 라벨 구간과 같다 —
            // 화면 문구(LOVE IT! / INTERESTED / NOT FOR ME / BORED)와 소리가 어긋나지 않게
            list.arraySize = 4;
            FillReaction(list.GetArrayElementAtIndex(0), "Great (LOVE IT!)", 20, bigStomp,
                volume: 0.85f, startTime: 0f, duration: 0f, pitch: new Vector2(0.97f, 1.03f));
            FillReaction(list.GetArrayElementAtIndex(1), "Good (INTERESTED)", 9, whistle,
                volume: 0.7f, startTime: 0f, duration: 0f, pitch: new Vector2(0.95f, 1.06f));
            FillReaction(list.GetArrayElementAtIndex(2), "Bad (NOT FOR ME)", -16, cough,
                volume: 0.6f, startTime: 0f, duration: 1.2f, pitch: new Vector2(0.98f, 1.04f));
            // 귀뚜라미는 4번 우는 2초짜리라 첫 울음 하나만 0.3초로 잘라 쓴다
            FillReaction(list.GetArrayElementAtIndex(3), "Awful (BORED, 귀뚜라미)", int.MinValue, cricket,
                volume: 0.8f, startTime: 0.05f, duration: 0.3f, pitch: new Vector2(1f, 1f));

            FillReaction(serialized.FindProperty("specialHitReaction"), "Special Hit", 0, bigStomp,
                volume: 1f, startTime: 0f, duration: 0f, pitch: new Vector2(1.02f, 1.08f));

            // 야유는 21초짜리 군중 소리라 앞부분만 잘라 쓴다
            FillReaction(serialized.FindProperty("booing"), "야유", 0, booing,
                volume: 0.75f, startTime: 0.2f, duration: 2.5f, pitch: new Vector2(1f, 1f));

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            WarnMissing("BigStomp", bigStomp);
            WarnMissing("Whistle", whistle);
            WarnMissing("cough_sarcastic", cough);
            WarnMissing("cricket", cricket);
            WarnMissing("crowd_low(야유)", booing);
            return true;
        }

        static void FillReaction(
            SerializedProperty entry,
            string label,
            int minScore,
            AudioClip clip,
            float volume,
            float startTime,
            float duration,
            Vector2 pitch)
        {
            if (entry == null) return;

            entry.FindPropertyRelative("label").stringValue = label;
            entry.FindPropertyRelative("minScore").intValue = minScore;
            entry.FindPropertyRelative("clip").objectReferenceValue = clip;
            entry.FindPropertyRelative("volume").floatValue = volume;
            entry.FindPropertyRelative("startTime").floatValue = startTime;
            entry.FindPropertyRelative("duration").floatValue = duration;
            entry.FindPropertyRelative("pitchRange").vector2Value = pitch;
        }

        // ---------------- 앰비언스 겹 ----------------

        static bool SetupAmbience(GameObject root)
        {
            var view = EnsureComponent<StageAmbienceLayers>(root);
            var serialized = new SerializedObject(view);

            SerializedProperty list = serialized.FindProperty("layers");
            if (list == null)
            {
                Debug.LogError("[AudienceAudio] 'layers' 필드를 찾지 못했습니다.");
                return false;
            }

            if (list.arraySize > 0) return false;

            AudioClip amp = Load(BgmFolder + "amp_noise_ambience.wav");
            AudioClip festival = Load(BgmFolder + "festival_noise_ambience.wav");
            AudioClip crowdMiddle = Load(BgmFolder + "crowd_middle.wav");
            AudioClip crowdHigh = Load(BgmFolder + "crowd_high.wav");

            list.arraySize = 4;
            FillLayer(list.GetArrayElementAtIndex(0), "Amp Noise", amp,
                volume: 0.35f, minCombo: 0, fade: 2f, offset: 0f);
            FillLayer(list.GetArrayElementAtIndex(1), "Festival Noise", festival,
                volume: 0.18f, minCombo: 0, fade: 2.5f, offset: 3f);
            FillLayer(list.GetArrayElementAtIndex(2), "Crowd Middle (combo)", crowdMiddle,
                volume: 0.5f, minCombo: 3, fade: 1.5f, offset: 0f);
            FillLayer(list.GetArrayElementAtIndex(3), "Crowd High (combo)", crowdHigh,
                volume: 0.5f, minCombo: 7, fade: 1.5f, offset: 2f);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            WarnMissing("amp_noise_ambience", amp);
            WarnMissing("festival_noise_ambience", festival);
            WarnMissing("crowd_middle", crowdMiddle);
            WarnMissing("crowd_high", crowdHigh);
            return true;
        }

        static void FillLayer(
            SerializedProperty entry,
            string layerName,
            AudioClip clip,
            float volume,
            int minCombo,
            float fade,
            float offset)
        {
            if (entry == null) return;

            entry.FindPropertyRelative("layerName").stringValue = layerName;
            entry.FindPropertyRelative("clip").objectReferenceValue = clip;
            entry.FindPropertyRelative("volume").floatValue = volume;
            entry.FindPropertyRelative("minCombo").intValue = minCombo;
            entry.FindPropertyRelative("fadeDuration").floatValue = fade;
            entry.FindPropertyRelative("startOffset").floatValue = offset;
        }

        static AudioClip Load(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        static void WarnMissing(string label, AudioClip clip)
        {
            if (clip == null) Debug.LogWarning($"[AudienceAudio] '{label}' 클립을 찾지 못했습니다.");
        }
    }
}
