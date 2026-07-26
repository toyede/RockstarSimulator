using UnityEngine;
using UnityEngine.Audio;

namespace GameJamKit
{
    /// <summary>
    /// 사운드가 나갈 믹서 버스.
    ///
    /// <b>0번이 ImpactSFX 인 이유:</b> SoundEntry 에 bus 를 나중에 추가했기 때문에
    /// 아직 분류되지 않은 항목은 기본값 0으로 역직렬화된다. 0이 범용 SFX 여야
    /// 마이그레이션 전에도 소리가 엉뚱한 그룹(예: UI)으로 새지 않는다.
    ///
    /// 음악은 이 열거형에 없다 — BGM 은 전용 소스(BGM_A/B)로만 나가고
    /// 항상 Music 그룹에 고정되므로 버스를 고를 여지가 없다.
    /// </summary>
    public enum AudioBus
    {
        ImpactSFX = 0,
        CardSFX = 1,
        EventSFX = 2,
        UI = 3,
        Crowd = 4,
    }

    /// <summary>
    /// 버스 → AudioMixerGroup 매핑을 한곳에 모아 둔 에셋.
    ///
    /// 씬마다 그룹 참조를 심지 않는 이유:
    /// <list type="bullet">
    /// <item>이 프로젝트의 AudioSource 는 <b>전부 런타임 생성</b>이라 씬에 꽂을 대상이 없다</item>
    /// <item>Main·Title 두 씬을 각각 고칠 필요가 없다</item>
    /// <item>새 재생 코드가 생겨도 같은 설정을 그대로 재사용한다</item>
    /// </list>
    ///
    /// <c>Resources/AudioRoutingConfig</c> 에서 <b>한 번만</b> 읽어 캐시한다.
    /// 재생할 때마다 Resources.Load 하거나 그룹 이름을 문자열로 찾지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioRoutingConfig", menuName = "GameJamKit/Audio Routing Config")]
    public sealed class AudioRoutingConfig : ScriptableObject
    {
        public const string ResourcesName = "AudioRoutingConfig";

        [Header("믹서 그룹 (GameAudioMixer)")]
        [SerializeField, Tooltip("BGM 전용. BGM_A/B 가 항상 여기로 나간다")]
        AudioMixerGroup music;

        [SerializeField, Tooltip("관객 앰비언스·관객 반응음")]
        AudioMixerGroup crowd;

        [SerializeField, Tooltip("카드 고유 행동음")]
        AudioMixerGroup cardSfx;

        [SerializeField, Tooltip("공통 판정 임팩트·Fever·Special Hit")]
        AudioMixerGroup impactSfx;

        [SerializeField, Tooltip("Crisis 경고·랜덤 공연 이벤트")]
        AudioMixerGroup eventSfx;

        [SerializeField, Tooltip("버튼·팝업·드롭다운")]
        AudioMixerGroup ui;

        public AudioMixerGroup Music => music;
        public AudioMixerGroup Crowd => crowd;
        public AudioMixerGroup CardSfx => cardSfx;
        public AudioMixerGroup ImpactSfx => impactSfx;
        public AudioMixerGroup EventSfx => eventSfx;
        public AudioMixerGroup UI => ui;

        /// <summary>버스에 해당하는 그룹. 비어 있으면 null 을 돌려주고 경고를 한 번만 남긴다.</summary>
        public AudioMixerGroup Resolve(AudioBus bus)
        {
            AudioMixerGroup group = bus switch
            {
                AudioBus.CardSFX => cardSfx,
                AudioBus.EventSFX => eventSfx,
                AudioBus.UI => ui,
                AudioBus.Crowd => crowd,
                _ => impactSfx,
            };

            if (group == null) AudioRouting.WarnMissingGroupOnce(bus.ToString());
            return group;
        }

        public AudioMixerGroup ResolveMusic()
        {
            if (music == null) AudioRouting.WarnMissingGroupOnce("Music");
            return music;
        }
    }

    /// <summary>
    /// 라우팅 설정에 대한 전역 접근자. 한 번 로드해 캐시한다.
    ///
    /// 설정이 없어도 <b>예외를 내지 않는다</b> — 경고 한 번만 남기고
    /// Output 이 None 인 상태로 재생한다 (Master 로 직행). 소리가 사라지는 것보다 낫다.
    /// </summary>
    public static class AudioRouting
    {
        static AudioRoutingConfig _config;
        static bool _loadAttempted;
        static bool _warnedMissingConfig;
        static string _warnedGroups = string.Empty;

        public static AudioRoutingConfig Config
        {
            get
            {
                if (_loadAttempted) return _config;

                _loadAttempted = true;
                _config = Resources.Load<AudioRoutingConfig>(AudioRoutingConfig.ResourcesName);
                if (_config == null && !_warnedMissingConfig)
                {
                    _warnedMissingConfig = true;
                    Debug.LogWarning(
                        $"[AudioRouting] Resources/{AudioRoutingConfig.ResourcesName}.asset 을 찾지 못했습니다. " +
                        "모든 소리가 Mixer Group 없이(Master 직결) 재생됩니다. " +
                        "Tools/Audio/Setup Audio Mixer Routing 을 실행하세요.");
                }
                return _config;
            }
        }

        public static AudioMixerGroup Resolve(AudioBus bus)
        {
            AudioRoutingConfig config = Config;
            return config != null ? config.Resolve(bus) : null;
        }

        public static AudioMixerGroup ResolveMusic()
        {
            AudioRoutingConfig config = Config;
            return config != null ? config.ResolveMusic() : null;
        }

        /// <summary>어떤 그룹이 비어 있는지 알려 준다. 같은 그룹은 한 번만 경고한다.</summary>
        internal static void WarnMissingGroupOnce(string groupName)
        {
            if (_warnedGroups.Contains($"|{groupName}|")) return;

            _warnedGroups += $"|{groupName}|";
            Debug.LogWarning(
                $"[AudioRouting] AudioRoutingConfig 의 '{groupName}' 그룹이 비어 있습니다. " +
                "해당 소리는 Mixer Group 없이 재생됩니다. " +
                "Tools/Audio/Setup Audio Mixer Routing 을 실행하세요.");
        }

        /// <summary>에디터에서 에셋을 다시 만들었을 때 캐시를 버린다.</summary>
        public static void InvalidateCache()
        {
            _config = null;
            _loadAttempted = false;
            _warnedMissingConfig = false;
            _warnedGroups = string.Empty;
        }
    }
}
