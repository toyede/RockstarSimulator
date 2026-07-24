using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객 앰비언스 한 단계(티어)의 정의.
    /// 티어를 늘리고 싶으면 코드가 아니라 CrowdAmbienceConfig 에셋의 리스트에 항목만 추가하면 된다.
    /// (low/middle/high 3단계는 기본값일 뿐 개수 제한은 없다)
    /// </summary>
    [System.Serializable]
    public class CrowdAmbienceTier
    {
        [Tooltip("에디터/디버그 표시용 이름. 코드에서 ForceTier(\"High\") 로 지정할 때도 쓰인다")]
        public string tierName = "Low";

        [Tooltip("SoundLibrary 에 등록한 사운드 ID. 예: crowd_low")]
        public string soundId = "crowd_low";

        [Tooltip("라이브러리를 쓰지 않을 때의 직접 참조(비상용). 지정되면 soundId 보다 우선한다")]
        public AudioClip clipOverride;

        [Tooltip("호응도 비율(0~1)이 이 값 이상이면 이 티어. 리스트는 이 값 오름차순으로 정렬해서 쓴다")]
        [Range(0f, 1f)] public float minNormalized = 0f;

        [Tooltip("이 티어의 기본 볼륨 (앰비언스 볼륨·마스터 볼륨과 곱해진다)")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("이 티어로 넘어갈 때의 크로스페이드 시간(초). 0 이면 콘픽 기본값 사용")]
        public float fadeDurationOverride = 0f;
    }

    /// <summary>
    /// 호응도 → 관객 앰비언스 매핑 밸런스 에셋.
    /// (Assets/Settings/CrowdAmbienceConfig.asset · Tools/Audio/Setup Crowd Ambience 로 자동 생성)
    ///
    /// 튜닝은 코드가 아니라 이 에셋에서 한다. 티어 추가/삭제도 여기서만 하면 시스템은 그대로 동작한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CrowdAmbienceConfig", menuName = "ContextStage/Crowd Ambience Config")]
    public class CrowdAmbienceConfig : ScriptableObject
    {
        [Header("티어 (호응도 낮은 순서로 정렬 권장)")]
        [SerializeField]
        List<CrowdAmbienceTier> tiers = new List<CrowdAmbienceTier>
        {
            new CrowdAmbienceTier { tierName = "Low",    soundId = "crowd_low",    minNormalized = 0.00f, volume = 0.8f },
            new CrowdAmbienceTier { tierName = "Middle", soundId = "crowd_middle", minNormalized = 0.40f, volume = 0.9f },
            new CrowdAmbienceTier { tierName = "High",   soundId = "crowd_high",   minNormalized = 0.70f, volume = 1.0f },
        };

        [Header("전환")]
        [Tooltip("티어 전환 크로스페이드 기본 시간(초)")]
        public float crossfadeDuration = 1.2f;

        [Tooltip("경계에서 티어가 딸깍거리지 않도록 하는 여유폭(0~1). " +
                 "올라갈 때는 경계+여유, 내려갈 때는 경계-여유를 넘어야 바뀐다")]
        [Range(0f, 0.2f)] public float hysteresis = 0.04f;

        [Header("볼륨")]
        [Tooltip("앰비언스 채널 기본 볼륨 (저장된 값이 없을 때 사용)")]
        [Range(0f, 1f)] public float defaultAmbienceVolume = 1f;

        [Tooltip("공연 시작(첫 재생) 시 페이드인 시간(초)")]
        public float startFadeDuration = 1.5f;

        [Tooltip("공연 종료(게임오버/정지) 시 페이드아웃 시간(초)")]
        public float stopFadeDuration = 1.0f;

        public IReadOnlyList<CrowdAmbienceTier> Tiers => tiers;
        public int TierCount => tiers != null ? tiers.Count : 0;

        public CrowdAmbienceTier GetTier(int index) =>
            tiers != null && index >= 0 && index < tiers.Count ? tiers[index] : null;

        /// <summary>이름으로 티어 인덱스를 찾는다. 없으면 -1. (대소문자 무시)</summary>
        public int IndexOfTier(string tierName)
        {
            if (tiers == null || string.IsNullOrEmpty(tierName)) return -1;
            for (int i = 0; i < tiers.Count; i++)
                if (tiers[i] != null && string.Equals(tiers[i].tierName, tierName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        /// <summary>
        /// 호응도 비율(0~1) → 티어 인덱스.
        /// currentIndex 를 넘기면 히스테리시스가 적용되어 경계에서 티어가 진동하지 않는다.
        /// (처음 결정할 때는 currentIndex 에 -1 을 넘긴다)
        /// </summary>
        public int ResolveTierIndex(float normalized, int currentIndex = -1)
        {
            if (tiers == null || tiers.Count == 0) return -1;

            int result = 0;
            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                if (tier == null) continue;

                // 지금 티어보다 위로 올라갈 때는 경계보다 조금 더, 내려갈 때는 조금 덜 가야 바뀐다
                float threshold = tier.minNormalized;
                if (currentIndex >= 0)
                    threshold += i > currentIndex ? hysteresis : -hysteresis;

                if (normalized >= threshold) result = i;
            }
            return result;
        }

        /// <summary>인덱스가 유효 범위 밖이면 잘라낸다.</summary>
        public int ClampIndex(int index) => TierCount == 0 ? -1 : Mathf.Clamp(index, 0, TierCount - 1);

#if UNITY_EDITOR
        void OnValidate()
        {
            // 리스트 순서가 뒤죽박죽이어도 ResolveTierIndex 가 동작하지만,
            // 인스펙터 가독성을 위해 정렬해 둔다.
            if (tiers != null) tiers.Sort((a, b) =>
                (a?.minNormalized ?? 0f).CompareTo(b?.minNormalized ?? 0f));
        }
#endif
    }
}
