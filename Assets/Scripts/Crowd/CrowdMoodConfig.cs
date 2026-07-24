using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객 한 명의 움직임을 만드는 수치 묶음. 티어마다 하나씩 들고 있다.
    /// "어떻게 움직이는가"를 전부 수치로 표현하므로 새 상태(예: 야유·기절)를 추가할 때
    /// 코드를 건드리지 않고 이 값만 채우면 된다.
    /// </summary>
    [System.Serializable]
    public class CrowdMotionProfile
    {
        [Header("제자리 반동 (항상 적용)")]
        [Tooltip("위아래로 까딱이는 높이(월드 유닛)")]
        public float bobHeight = 0.04f;

        [Tooltip("초당 까딱임 횟수")]
        public float bobSpeed = 1.2f;

        [Header("좌우 흔들림")]
        [Tooltip("좌우로 기우는 최대 각도(도)")]
        public float swayAngle = 3f;

        [Tooltip("초당 흔들림 횟수")]
        public float swaySpeed = 1f;

        [Header("점프 (high 상태의 '방방 뛰기')")]
        [Tooltip("점프 높이(월드 유닛). 0 이면 점프하지 않는다")]
        public float jumpHeight = 0f;

        [Tooltip("초당 점프 횟수")]
        public float jumpsPerSecond = 0f;

        [Tooltip("한 사이클 중 공중에 떠 있는 비율(0~1). 나머지는 착지 후 웅크리는 시간")]
        [Range(0.1f, 1f)] public float airTimeRatio = 0.7f;

        [Tooltip("착지 순간 눌리는 정도(0~0.5). 점프에 무게감을 준다")]
        [Range(0f, 0.5f)] public float squash = 0.12f;

        [Header("개체 편차")]
        [Tooltip("관객마다 속도를 이 비율만큼 랜덤하게 달리해 군무처럼 보이지 않게 한다")]
        [Range(0f, 0.6f)] public float speedVariance = 0.2f;

        [Header("스프라이트")]
        [Tooltip("0 이면 티어당 스프라이트 1장 고정(개체별 변형만 사용). " +
                 "0보다 크면 세트를 프레임 애니메이션처럼 이 FPS 로 순환한다")]
        public float spriteCycleFps = 0f;
    }

    /// <summary>
    /// 관객 비주얼 한 단계. 사운드의 CrowdAmbienceTier 와 같은 구조(IHypeTier)라
    /// 두 시스템이 정확히 같은 호응도 구간에서 함께 바뀐다.
    /// </summary>
    [System.Serializable]
    public class CrowdMoodTier : IHypeTier
    {
        [Tooltip("표시·디버그용 이름. ForceMood(\"High\") 로 지정할 때도 쓰인다")]
        public string moodName = "Low";

        [Tooltip("호응도 비율(0~1)이 이 값 이상이면 이 상태")]
        [Range(0f, 1f)] public float minNormalized = 0f;

        [Tooltip("이 상태에서 쓸 관객 스프라이트 세트. 관객마다 이 중 하나를 나눠 갖는다")]
        public Sprite[] sprites;

        [Tooltip("실루엣 색조. 조명 연출과 맞추면 상태 구분이 훨씬 쉬워진다")]
        public Color tint = Color.white;

        [Tooltip("이 상태의 크기 배율 (열기가 오를수록 살짝 커지면 체감이 좋다)")]
        public float scaleMultiplier = 1f;

        [Tooltip("이 상태의 움직임 수치")]
        public CrowdMotionProfile motion = new CrowdMotionProfile();

        public float MinNormalized => minNormalized;
    }

    /// <summary>
    /// 호응도 → 관객 비주얼 매핑 밸런스 에셋.
    /// (Assets/Settings/CrowdMoodConfig.asset · Tools/Crowd/Setup Crowd Scene 으로 자동 생성)
    ///
    /// 상태를 4단계 이상으로 늘려도 코드 수정이 필요 없다. 리스트에 항목만 추가하면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "CrowdMoodConfig", menuName = "ContextStage/Crowd Mood Config")]
    public class CrowdMoodConfig : ScriptableObject
    {
        [Header("상태 (호응도 낮은 순서로 정렬 권장)")]
        [SerializeField]
        List<CrowdMoodTier> tiers = new List<CrowdMoodTier>
        {
            new CrowdMoodTier
            {
                moodName = "Low", minNormalized = 0.00f, scaleMultiplier = 0.96f,
                motion = new CrowdMotionProfile
                {
                    bobHeight = 0.03f, bobSpeed = 0.8f, swayAngle = 2f, swaySpeed = 0.6f,
                    jumpHeight = 0f, jumpsPerSecond = 0f, speedVariance = 0.25f,
                },
            },
            new CrowdMoodTier
            {
                moodName = "Middle", minNormalized = 0.40f, scaleMultiplier = 1f,
                motion = new CrowdMotionProfile
                {
                    bobHeight = 0.08f, bobSpeed = 1.6f, swayAngle = 6f, swaySpeed = 1.3f,
                    jumpHeight = 0f, jumpsPerSecond = 0f, speedVariance = 0.2f,
                },
            },
            new CrowdMoodTier
            {
                moodName = "High", minNormalized = 0.70f, scaleMultiplier = 1.05f,
                motion = new CrowdMotionProfile
                {
                    bobHeight = 0.04f, bobSpeed = 2f, swayAngle = 8f, swaySpeed = 2.2f,
                    jumpHeight = 0.45f, jumpsPerSecond = 1.8f, airTimeRatio = 0.7f,
                    squash = 0.15f, speedVariance = 0.15f,
                },
            },
        };

        [Header("전환")]
        [Tooltip("상태가 바뀔 때 움직임이 서서히 섞이는 시간(초). 0 이면 즉시 전환")]
        public float moodBlendDuration = 0.6f;

        [Tooltip("경계에서 상태가 딸깍거리지 않도록 하는 여유폭. 사운드 콘픽과 같은 값을 권장")]
        [Range(0f, 0.2f)] public float hysteresis = 0.04f;

        public IReadOnlyList<CrowdMoodTier> Tiers => tiers;
        public int TierCount => tiers != null ? tiers.Count : 0;

        public CrowdMoodTier GetTier(int index) =>
            tiers != null && index >= 0 && index < tiers.Count ? tiers[index] : null;

        /// <summary>호응도 비율 → 상태 인덱스. 사운드와 같은 HypeTierUtil 을 쓴다.</summary>
        public int ResolveTierIndex(float normalized, int currentIndex = -1) =>
            HypeTierUtil.Resolve(tiers, normalized, currentIndex, hysteresis);

        public int ClampIndex(int index) => HypeTierUtil.ClampIndex(tiers, index);

        /// <summary>이름으로 상태 인덱스를 찾는다. 없으면 -1.</summary>
        public int IndexOfTier(string moodName)
        {
            if (tiers == null || string.IsNullOrEmpty(moodName)) return -1;
            for (int i = 0; i < tiers.Count; i++)
                if (tiers[i] != null && string.Equals(tiers[i].moodName, moodName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (tiers != null) tiers.Sort((a, b) =>
                (a?.minNormalized ?? 0f).CompareTo(b?.minNormalized ?? 0f));
        }
#endif
    }
}
