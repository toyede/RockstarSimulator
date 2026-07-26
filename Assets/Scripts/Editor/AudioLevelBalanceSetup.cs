using System.Collections.Generic;
using System.Text;
using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 클립 레벨 정리. 소스 파일마다 녹음 레벨이 제각각이라 같은 volume 1.0 으로 재생하면
    /// 어떤 소리는 튀고 어떤 소리는 묻힌다.
    ///
    /// <b>추측이 아니라 측정값에서 나온 표다.</b> 각 WAV 의 RMS 를 재서
    /// 역할별 목표 라우드니스에 맞도록 volume 을 역산했다.
    /// 측정 당시 가장 큰 소리와 가장 작은 소리의 차이는 <b>약 35 dB</b> 였다.
    ///
    /// <c>Tools/Audio/Balance Audio Levels</c>
    ///
    /// <b>손으로 맞춰 둔 볼륨을 덮어쓴다.</b> 정렬이 목적이므로 의도된 동작이고,
    /// 바뀐 값은 전부 로그에 이전값과 함께 남는다.
    ///
    /// 믹서 그룹 dB 와 이펙트(High/Low Pass · Compressor · Reverb · Ducking)는
    /// 스크립팅 API 가 없어 이 도구가 건드리지 않는다 — Audio Mixer 창에서 직접 해야 한다.
    /// </summary>
    public static class AudioLevelBalanceSetup
    {
        const string LibraryPath = "Assets/GameJamKit/Resources/SoundLibrary.asset";

        /// <summary>
        /// SoundLibrary 항목별 목표 볼륨.
        ///
        /// measuredRms = 원본 파일의 실제 RMS(dB), targetRms = 역할별 목표.
        /// volume 은 두 값의 차이에서 나온다. 1.0 을 넘어야 하는 항목은
        /// <b>원본이 너무 작아 목표에 못 미치는 것</b>이므로 별도로 보고한다.
        /// </summary>
        static readonly (string id, float measuredRms, float targetRms, float volume, string role)[] LibraryLevels =
        {
            // --- 카드 행동음: 목표 -18 dB ---
            ("guitar_solo",        -6.0f,  -18f, 0.25f, "카드 (가장 시끄러웠음)"),
            ("guitar_stroke",     -14.2f,  -18f, 0.65f, "카드 (4종 평균)"),
            ("card_open_mosh_pit", -10.0f, -18f, 0.40f, "카드"),
            ("card_tempo_up",     -11.4f,  -18f, 0.47f, "카드"),
            ("card_draw_two",     -11.7f,  -18f, 0.48f, "카드"),
            ("card_response_call",-17.9f,  -18f, 0.99f, "카드"),
            ("card_reroll_hand",  -21.0f,  -18f, 1.00f, "카드 (원본이 작아 한계)"),

            // --- 관객 원샷: 목표 -20 dB ---
            ("crowd_mistake",     -21.7f,  -20f, 1.00f, "관객 (원본이 작아 한계)"),
            ("hey_high",          -21.1f,  -20f, 1.00f, "관객 (원본이 작아 한계)"),
            ("hey_low",           -21.7f,  -20f, 1.00f, "관객 (원본이 작아 한계)"),

            // --- UI: 목표 -24 dB. 조작음은 확실히 뒤로 물러나야 한다 ---
            ("ui_click_wooden",   -20.8f,  -24f, 0.69f, "UI"),
            ("game_start",        -41.8f,  -24f, 1.00f, "UI (원본이 매우 작아 한계)"),
        };

        /// <summary>
        /// AudienceReactionSfx 반응별 목표. 등급이 올라갈수록 크게 들리도록 목표를 계단식으로 둔다.
        /// (PERFECT -17 / GOOD -20 / FAIL -21 / BORED -22 / 야유 -18)
        /// </summary>
        static readonly (string label, int minScore, float volume, string note)[] ReactionLevels =
        {
            ("Great (LOVE IT!)",       20,           1.00f, "BigStomp -20.5dB · 원본이 작아 목표 -17 미달"),
            ("Good (INTERESTED)",       9,           0.33f, "Whistle -10.3dB · 크게 낮춤"),
            ("Bad (NOT FOR ME)",      -16,           1.00f, "cough -31.5dB · 원본이 작아 목표 -21 미달"),
            ("Awful (BORED, 귀뚜라미)", int.MinValue, 0.92f, "cricket -22.7dB"),
        };

        const float SpecialHitVolume = 1.00f;  // BigStomp -20.5dB, 임팩트와 겹쳐 쓰는 전제
        const float BooingVolume = 0.54f;      // crowd_low -12.7dB → 목표 -18
        const float FeverIntroVolume = 0.89f;  // fevertime_intro -14.0dB → 목표 -15

        /// <summary>앰비언스는 깔개(-33dB)와 콤보 보상(-27dB) 두 층으로 나눈다.</summary>
        static readonly (string layerName, float volume, string note)[] AmbienceLevels =
        {
            ("Amp Noise",           0.375f, "amp_noise -24.5dB → 깔개 -33"),
            ("Festival Noise",      0.19f,  "festival -18.4dB → 깔개 -33"),
            ("Crowd Middle (combo)",0.53f,  "crowd_middle -21.5dB → 보상 -27"),
            ("Crowd High (combo)",  0.39f,  "crowd_high -18.7dB → 보상 -27"),
        };

        [MenuItem("Tools/Audio/Balance Audio Levels", false, 32)]
        public static void Balance()
        {
            var report = new StringBuilder();
            report.AppendLine("[AudioBalance] 클립 레벨 정리 (측정 RMS 기반)");

            int changed = 0;
            changed += BalanceLibrary(report);
            changed += BalanceReactions(report);
            changed += BalanceAmbience(report);
            changed += BalanceFever(report);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            report.AppendLine($"\n■ 총 {changed}개 값 변경.");
            report.AppendLine("■ 믹서 그룹 dB·이펙트는 건드리지 않았습니다 — Audio Mixer 창에서 직접 하세요.");
            report.Append("■ 씬 컴포넌트를 바꿨다면 Ctrl+S 로 저장하세요.");
            Debug.Log(report.ToString());
        }

        // ---------------- SoundLibrary ----------------

        static int BalanceLibrary(StringBuilder report)
        {
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                report.AppendLine($"\n■ SoundLibrary 없음: {LibraryPath}");
                return 0;
            }

            var serialized = new SerializedObject(library);
            SerializedProperty sounds = serialized.FindProperty("sounds");
            if (sounds == null) return 0;

            var targets = new Dictionary<string, (float volume, float measured, float target, string role)>();
            for (int i = 0; i < LibraryLevels.Length; i++)
            {
                var level = LibraryLevels[i];
                targets[level.id] = (level.volume, level.measuredRms, level.targetRms, level.role);
            }

            report.AppendLine("\n■ SoundLibrary");
            int changed = 0;

            for (int i = 0; i < sounds.arraySize; i++)
            {
                SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                if (!targets.TryGetValue(id, out var target)) continue;

                SerializedProperty volume = entry.FindPropertyRelative("volume");
                float before = volume.floatValue;
                if (Mathf.Approximately(before, target.volume)) continue;

                volume.floatValue = target.volume;
                changed++;
                float delta = 20f * Mathf.Log10(Mathf.Max(0.0001f, target.volume));
                report.AppendLine(
                    $"   {id,-20} {before:0.00} → {target.volume:0.00}  " +
                    $"({delta:+0.0;-0.0} dB, RMS {target.measured:0.0} → {target.target:0.0})  {target.role}");
            }

            if (changed > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(library);
                library.InvalidateCache();
            }
            else
            {
                report.AppendLine("   변경 없음 (이미 정렬돼 있음)");
            }

            report.AppendLine("   big_rock · breakdown 은 BGM 이라 건드리지 않았습니다 (0.615 유지)");
            return changed;
        }

        // ---------------- 관객 반응 ----------------

        static int BalanceReactions(StringBuilder report)
        {
            report.AppendLine("\n■ AudienceReactionSfx");

            var view = Object.FindFirstObjectByType<AudienceReactionSfx>(FindObjectsInactive.Include);
            if (view == null)
            {
                report.AppendLine("   씬에 없음 — Main 씬을 연 뒤 다시 실행하세요");
                return 0;
            }

            var serialized = new SerializedObject(view);
            SerializedProperty list = serialized.FindProperty("reactions");
            int changed = 0;

            if (list != null)
            {
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty entry = list.GetArrayElementAtIndex(i);
                    int minScore = entry.FindPropertyRelative("minScore").intValue;

                    for (int t = 0; t < ReactionLevels.Length; t++)
                    {
                        if (ReactionLevels[t].minScore != minScore) continue;

                        SerializedProperty volume = entry.FindPropertyRelative("volume");
                        float before = volume.floatValue;
                        if (Mathf.Approximately(before, ReactionLevels[t].volume)) break;

                        volume.floatValue = ReactionLevels[t].volume;
                        changed++;
                        report.AppendLine(
                            $"   {ReactionLevels[t].label,-24} {before:0.00} → {ReactionLevels[t].volume:0.00}" +
                            $"   {ReactionLevels[t].note}");
                        break;
                    }
                }
            }

            changed += SetNested(serialized, "specialHitReaction", "volume", SpecialHitVolume,
                "Special Hit", report);
            changed += SetNested(serialized, "booing", "volume", BooingVolume,
                "야유 (crowd_low)", report);

            if (changed > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
            }
            else
            {
                report.AppendLine("   변경 없음 (이미 정렬돼 있음)");
            }
            return changed;
        }

        static int SetNested(
            SerializedObject serialized,
            string parentField,
            string childField,
            float value,
            string label,
            StringBuilder report)
        {
            SerializedProperty parent = serialized.FindProperty(parentField);
            SerializedProperty child = parent?.FindPropertyRelative(childField);
            if (child == null) return 0;

            float before = child.floatValue;
            if (Mathf.Approximately(before, value)) return 0;

            child.floatValue = value;
            report.AppendLine($"   {label,-24} {before:0.00} → {value:0.00}");
            return 1;
        }

        // ---------------- 앰비언스 ----------------

        static int BalanceAmbience(StringBuilder report)
        {
            report.AppendLine("\n■ StageAmbienceLayers");

            var view = Object.FindFirstObjectByType<StageAmbienceLayers>(FindObjectsInactive.Include);
            if (view == null)
            {
                report.AppendLine("   씬에 없음 — Main 씬을 연 뒤 다시 실행하세요");
                return 0;
            }

            var serialized = new SerializedObject(view);
            SerializedProperty list = serialized.FindProperty("layers");
            if (list == null) return 0;

            int changed = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                string name = entry.FindPropertyRelative("layerName").stringValue;

                for (int t = 0; t < AmbienceLevels.Length; t++)
                {
                    if (AmbienceLevels[t].layerName != name) continue;

                    SerializedProperty volume = entry.FindPropertyRelative("volume");
                    float before = volume.floatValue;
                    if (Mathf.Approximately(before, AmbienceLevels[t].volume)) break;

                    volume.floatValue = AmbienceLevels[t].volume;
                    changed++;
                    report.AppendLine(
                        $"   {name,-24} {before:0.00} → {AmbienceLevels[t].volume:0.00}   {AmbienceLevels[t].note}");
                    break;
                }
            }

            if (changed > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(view);
            }
            else
            {
                report.AppendLine("   변경 없음 (이미 정렬돼 있음)");
            }
            return changed;
        }

        // ---------------- Fever ----------------

        static int BalanceFever(StringBuilder report)
        {
            report.AppendLine("\n■ FeverPresentation");

            var view = Object.FindFirstObjectByType<FeverPresentation>(FindObjectsInactive.Include);
            if (view == null)
            {
                report.AppendLine("   씬에 없음");
                return 0;
            }

            var serialized = new SerializedObject(view);
            SerializedProperty volume = serialized.FindProperty("introVolume");
            if (volume == null) return 0;

            float before = volume.floatValue;
            if (Mathf.Approximately(before, FeverIntroVolume))
            {
                report.AppendLine("   변경 없음 (이미 정렬돼 있음)");
                return 0;
            }

            volume.floatValue = FeverIntroVolume;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            report.AppendLine(
                $"   introVolume            {before:0.00} → {FeverIntroVolume:0.00}   " +
                "fevertime_intro -14.0dB → 목표 -15 (임팩트가 가장 앞)");
            return 1;
        }
    }
}
