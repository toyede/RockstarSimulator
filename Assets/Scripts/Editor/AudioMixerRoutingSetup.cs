using System.Collections.Generic;
using System.Text;
using GameJamKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 오디오 믹서 라우팅 셋업·검증.
    ///
    /// 이 프로젝트의 AudioSource 는 <b>전부 런타임 생성</b>이라 씬이나 프리팹에 꽂을 대상이 없다.
    /// 그래서 씬을 고치는 대신 <see cref="AudioRoutingConfig"/> 하나에 그룹 참조를 모으고,
    /// 재생 코드가 그것을 읽어 Output 을 정한다.
    ///
    /// <c>Tools/Audio/Setup Audio Mixer Routing</c>
    /// <list type="number">
    /// <item>기존 GameAudioMixer 를 찾는다 (새로 만들지 않는다)</item>
    /// <item>AudioRoutingConfig 를 만들거나 갱신하고 그룹 6개를 연결한다</item>
    /// <item>SoundLibrary 의 알려진 ID 에 버스를 지정한다</item>
    /// </list>
    ///
    /// <c>Tools/Audio/Validate Audio Mixer Routing</c> 은 아무것도 바꾸지 않고 상태만 보고한다.
    ///
    /// 두 번 실행해도 에셋이나 설정이 중복되지 않는다.
    /// </summary>
    public static class AudioMixerRoutingSetup
    {
        const string MixerPath = "Assets/Audio/GameAudioMixer.mixer";
        const string ConfigFolder = "Assets/GameJamKit/Resources";
        const string ConfigPath = ConfigFolder + "/AudioRoutingConfig.asset";
        const string LibraryPath = "Assets/GameJamKit/Resources/SoundLibrary.asset";

        /// <summary>Config 의 필드 이름 → 믹서 그룹 이름.</summary>
        static readonly (string field, string group)[] GroupBindings =
        {
            ("music", "Music"),
            ("crowd", "Crowd"),
            ("cardSfx", "CardSFX"),
            ("impactSfx", "ImpactSFX"),
            ("eventSfx", "EventSFX"),
            ("ui", "UI"),
        };

        /// <summary>
        /// SoundLibrary ID → 버스. 실제 호출부를 확인해 분류했다.
        ///
        /// big_rock · breakdown 은 BGM 전용 소스로만 나가므로 버스를 쓰지 않는다.
        /// 다만 누군가 실수로 Sound.Play 로 부르면 임팩트로 나가도록 기본값을 그대로 둔다.
        /// </summary>
        static readonly (string id, AudioBus bus)[] BusAssignments =
        {
            // 관객 — CrowdAmbienceSystem 티어 / 관객 반응
            ("crowd_low", AudioBus.Crowd),
            ("crowd_middle", AudioBus.Crowd),
            ("crowd_high", AudioBus.Crowd),
            ("crowd_mistake", AudioBus.Crowd),
            ("hey_high", AudioBus.Crowd),
            ("hey_low", AudioBus.Crowd),

            // 카드 고유 행동음 — CardSfxPlayer 가 카드별 override 로 재생
            ("guitar_stroke", AudioBus.CardSFX),
            ("guitar_solo", AudioBus.CardSFX),
            ("card_tempo_up", AudioBus.CardSFX),
            ("card_response_call", AudioBus.CardSFX),
            ("card_reroll_hand", AudioBus.CardSFX),
            ("card_draw_two", AudioBus.CardSFX),
            ("card_open_mosh_pit", AudioBus.CardSFX),
            // 아직 클립이 없어 라이브러리에 등록되지 않았지만, 등록되는 순간 올바른 버스로 들어가게 미리 둔다
            ("card_hands_up", AudioBus.CardSFX),
            ("card_pass_mic", AudioBus.CardSFX),

            // UI — 버튼 UnityEvent(PlaySfx) · DisplayModeDropdown
            ("ui_click_wooden", AudioBus.UI),
            ("game_start", AudioBus.UI),
        };

        /// <summary>
        /// 알려진 ID 의 기본 버스. <b>분류표의 유일한 출처다.</b>
        ///
        /// AudioSetupMenu 가 SoundLibrary 항목을 새로 만들 때도 이 함수를 쓴다 —
        /// 표를 두 벌 두면 한쪽만 고쳐져 재실행 때 분류가 어긋난다.
        /// </summary>
        internal static bool TryGetDefaultBus(string id, out AudioBus bus)
        {
            for (int i = 0; i < BusAssignments.Length; i++)
            {
                if (BusAssignments[i].id != id) continue;
                bus = BusAssignments[i].bus;
                return true;
            }

            bus = AudioBus.ImpactSFX;
            return false;
        }

        // ---------------- Setup ----------------

        [MenuItem("Tools/Audio/Setup Audio Mixer Routing", false, 30)]
        public static void Setup()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                Debug.LogError(
                    $"[AudioRouting] 믹서를 찾지 못했습니다: {MixerPath}\n" +
                    "새 믹서를 만들지 않습니다 — 경로를 확인하세요.");
                return;
            }

            AudioRoutingConfig config = EnsureConfig(out bool created);
            if (config == null) return;

            int linked = LinkGroups(config, mixer, out List<string> missingGroups);
            int classified = ApplyBuses(out int unknown);

            AssetDatabase.SaveAssets();
            AudioRouting.InvalidateCache();
            Selection.activeObject = config;

            var log = new StringBuilder();
            log.AppendLine($"[AudioRouting] 셋업 완료 — Config {(created ? "생성" : "갱신")}: {ConfigPath}");
            log.AppendLine($"  믹서 그룹 연결: {linked}/{GroupBindings.Length}");
            log.AppendLine($"  SoundLibrary 버스 지정: {classified}개 변경, 미분류 {unknown}개");
            if (missingGroups.Count > 0)
                log.AppendLine($"  ⚠ 믹서에서 찾지 못한 그룹: {string.Join(", ", missingGroups)}");
            log.Append("  Mixer 효과·볼륨은 건드리지 않았습니다.");

            Debug.Log(log.ToString(), config);
        }

        static AudioRoutingConfig EnsureConfig(out bool created)
        {
            created = false;

            var existing = AssetDatabase.LoadAssetAtPath<AudioRoutingConfig>(ConfigPath);
            if (existing != null) return existing;

            EnsureFolder(ConfigFolder);
            var config = ScriptableObject.CreateInstance<AudioRoutingConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            created = true;
            return AssetDatabase.LoadAssetAtPath<AudioRoutingConfig>(ConfigPath);
        }

        /// <summary>
        /// 이름으로 그룹을 찾아 Config 에 연결한다.
        /// 이미 올바르게 연결돼 있으면 건드리지 않아 불필요한 에셋 변경을 만들지 않는다.
        /// </summary>
        static int LinkGroups(AudioRoutingConfig config, AudioMixer mixer, out List<string> missing)
        {
            missing = new List<string>();
            var serialized = new SerializedObject(config);
            int linked = 0;
            bool changed = false;

            for (int i = 0; i < GroupBindings.Length; i++)
            {
                (string field, string groupName) = GroupBindings[i];

                AudioMixerGroup[] found = mixer.FindMatchingGroups(groupName);
                AudioMixerGroup group = PickExact(found, groupName);
                if (group == null)
                {
                    missing.Add(groupName);
                    continue;
                }

                SerializedProperty property = serialized.FindProperty(field);
                if (property == null)
                {
                    Debug.LogWarning($"[AudioRouting] Config 에 '{field}' 필드가 없습니다.");
                    continue;
                }

                linked++;
                if (property.objectReferenceValue == group) continue;

                property.objectReferenceValue = group;
                changed = true;
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(config);
            }
            return linked;
        }

        /// <summary>FindMatchingGroups 는 부분 일치라 이름이 정확히 같은 것을 골라야 한다.</summary>
        static AudioMixerGroup PickExact(AudioMixerGroup[] groups, string name)
        {
            if (groups == null) return null;
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] != null && groups[i].name == name) return groups[i];
            return null;
        }

        /// <summary>
        /// SoundLibrary 항목에 버스를 지정한다.
        /// 표에 없는 ID 는 <b>건드리지 않는다</b> — 사람이 손으로 정한 값을 되돌리지 않기 위해서다.
        /// </summary>
        static int ApplyBuses(out int unknown)
        {
            unknown = 0;

            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                Debug.LogWarning($"[AudioRouting] SoundLibrary 를 찾지 못했습니다: {LibraryPath}");
                return 0;
            }

            var serialized = new SerializedObject(library);
            SerializedProperty sounds = serialized.FindProperty("sounds");
            if (sounds == null) return 0;

            var known = new Dictionary<string, AudioBus>();
            for (int i = 0; i < BusAssignments.Length; i++)
                known[BusAssignments[i].id] = BusAssignments[i].bus;

            int changed = 0;
            var unclassified = new List<string>();

            for (int i = 0; i < sounds.arraySize; i++)
            {
                SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                SerializedProperty bus = entry.FindPropertyRelative("bus");
                if (bus == null) continue;

                if (!known.TryGetValue(id, out AudioBus target))
                {
                    unclassified.Add(id);
                    continue;
                }

                if (bus.enumValueIndex == (int)target) continue;
                bus.enumValueIndex = (int)target;
                changed++;
            }

            if (changed > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(library);
                library.InvalidateCache();
            }

            unknown = unclassified.Count;
            if (unclassified.Count > 0)
            {
                Debug.Log(
                    "[AudioRouting] 분류표에 없어 기본 버스로 남겨둔 ID: " +
                    string.Join(", ", unclassified) +
                    "\n  (big_rock·breakdown 은 BGM 전용 소스로만 나가므로 버스를 쓰지 않습니다)");
            }

            return changed;
        }

        // ---------------- Validation ----------------

        [MenuItem("Tools/Audio/Validate Audio Mixer Routing", false, 31)]
        public static void Validate()
        {
            var report = new StringBuilder();
            report.AppendLine("[AudioRouting] 검증 결과 (아무것도 바꾸지 않았습니다)");

            int problems = 0;

            // 1) 믹서
            string[] mixerGuids = AssetDatabase.FindAssets("t:AudioMixer");
            report.AppendLine($"\n■ AudioMixer 에셋 {mixerGuids.Length}개");
            for (int i = 0; i < mixerGuids.Length; i++)
                report.AppendLine($"   - {AssetDatabase.GUIDToAssetPath(mixerGuids[i])}");
            if (mixerGuids.Length > 1)
            {
                report.AppendLine("   ⚠ 믹서가 여러 개입니다. 하나만 쓰는지 확인하세요.");
                problems++;
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null)
            {
                report.AppendLine($"   ✗ {MixerPath} 없음");
                problems++;
            }

            // 2) Config
            var config = AssetDatabase.LoadAssetAtPath<AudioRoutingConfig>(ConfigPath);
            report.AppendLine("\n■ AudioRoutingConfig");
            if (config == null)
            {
                report.AppendLine($"   ✗ {ConfigPath} 없음 — Setup 을 실행하세요");
                problems++;
            }
            else
            {
                var serialized = new SerializedObject(config);
                for (int i = 0; i < GroupBindings.Length; i++)
                {
                    (string field, string groupName) = GroupBindings[i];
                    SerializedProperty property = serialized.FindProperty(field);
                    Object value = property != null ? property.objectReferenceValue : null;
                    if (value == null)
                    {
                        report.AppendLine($"   ✗ {groupName}: 비어 있음");
                        problems++;
                    }
                    else
                    {
                        report.AppendLine($"   ✓ {groupName} → {value.name}");
                    }
                }
            }

            // 3) SoundLibrary 버스
            var library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            report.AppendLine("\n■ SoundLibrary 버스");
            if (library == null)
            {
                report.AppendLine($"   ✗ {LibraryPath} 없음");
                problems++;
            }
            else
            {
                var serialized = new SerializedObject(library);
                SerializedProperty sounds = serialized.FindProperty("sounds");
                for (int i = 0; i < sounds.arraySize; i++)
                {
                    SerializedProperty entry = sounds.GetArrayElementAtIndex(i);
                    string id = entry.FindPropertyRelative("id").stringValue;
                    SerializedProperty bus = entry.FindPropertyRelative("bus");
                    string busName = bus != null
                        ? ((AudioBus)bus.enumValueIndex).ToString()
                        : "(필드 없음)";
                    report.AppendLine($"   {id,-22} → {busName}");
                }
            }

            // 4) 씬·프리팹의 직렬화된 AudioSource (Main / Title 만)
            report.AppendLine("\n■ 씬·프리팹의 직렬화 AudioSource (Main / Title 범위)");
            int serializedSources = CountSerializedAudioSources(report);
            if (serializedSources == 0)
                report.AppendLine("   ✓ 없음 — 모든 AudioSource 가 런타임 생성입니다");

            report.AppendLine(
                problems == 0
                    ? "\n■ 문제 없음."
                    : $"\n■ 확인이 필요한 항목 {problems}개.");

            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <summary>
        /// Main·Title 과 그 둘이 참조하는 프리팹만 본다.
        /// 레거시·개인 작업 씬(JWY·Hwi·Minyoung)은 열지도 읽지도 않는다.
        /// </summary>
        static int CountSerializedAudioSources(StringBuilder report)
        {
            string[] targets =
            {
                "Assets/Scenes/Main.unity",
                "Assets/Scenes/Title.unity",
            };

            int total = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(targets[i]);
                int count = 0;
                for (int a = 0; a < assets.Length; a++)
                    if (assets[a] is AudioSource) count++;

                if (count > 0)
                {
                    report.AppendLine($"   {targets[i]}: {count}개 — Output 을 직접 확인하세요");
                    total += count;
                }
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Card_Prefab" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                AudioSource[] sources = prefab.GetComponentsInChildren<AudioSource>(true);
                if (sources.Length == 0) continue;

                total += sources.Length;
                for (int s = 0; s < sources.Length; s++)
                {
                    string group = sources[s].outputAudioMixerGroup != null
                        ? sources[s].outputAudioMixerGroup.name
                        : "None";
                    report.AppendLine($"   {path} / {sources[s].name}: Output = {group}");
                }
            }

            return total;
        }
    }
}
