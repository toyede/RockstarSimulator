using System.Collections.Generic;
using GameJamKit; // HypeJudgement enum 이 킷 GameEvents.cs 에 있다 (킷 README "변경 이력" 참조)
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 열기(호응도) 구간별 점수 배율 한 단계. 열기가 이 비율 이상이면 이 배율을 쓴다.
    /// 사운드/관객과 같은 티어 판정(HypeTierUtil)을 재사용하기 위해 IHypeTier 를 구현한다.
    /// </summary>
    [System.Serializable]
    public class HeatMultiplierTier : IHypeTier
    {
        [Tooltip("표시용 이름 (낮음/중간/높음/MAX 등)")]
        public string label = "Tier";

        [Range(0f, 1f), Tooltip("열기 비율(0~1)이 이 값 이상이면 이 단계")]
        public float minNormalized;

        [Tooltip("이 단계에서 카드 점수에 곱해지는 배율")]
        public float multiplier = 1f;

        public float MinNormalized => minNormalized;
    }

    /// <summary>
    /// 호응도 밸런스 수치 모음. (기획서 "최종 권장안" 기준값이 기본값으로 들어있다)
    ///
    /// 플레이 테스트 후 수치를 바꿀 때는 코드가 아니라 이 에셋
    /// (Assets/Settings/HypeConfig.asset) 만 수정하고,
    /// .myDox/호응도 시스템 작업계획.md 의 "밸런스 튜닝 메모"에 기록한다.
    /// </summary>
    [CreateAssetMenu(fileName = "HypeConfig", menuName = "ContextStage/Hype Config")]
    public class HypeConfig : ScriptableObject
    {
        [Header("기본")]
        [Tooltip("공연 시작 시 호응도")]
        public float startHype = 30f;

        [Tooltip("호응도 최대치. 도달하면 앙코르 발동")]
        public float maxHype = 100f;

        [Tooltip("카드 선택(관찰) 중 초당 감소량")]
        public float decayPerSecond = 1f;

        [Header("열기 단계")]
        [Range(0f, 100f), Tooltip("Singalong 단계가 시작되는 열기")]
        public float singalongMinHype = 50f;

        [Range(0f, 100f), Tooltip("Mosh 단계가 시작되는 열기")]
        public float moshMinHype = 80f;

        [Header("판정별 증감 (감소는 음수로 입력)")]
        [Tooltip("Perfect: 관객 맥락을 정확히 읽음")]
        public float perfectDelta = 30f;

        [Tooltip("Good: 완벽하진 않지만 분위기 유지")]
        public float goodDelta = 15f;

        [Tooltip("Miss: 관객과 맞지 않는 행동")]
        public float missDelta = -15f;

        [Tooltip("RiskMiss: 위험 카드(모쉬핏 등)의 완전 오판")]
        public float riskMissDelta = -25f;

        [Header("점수 배율 (열기 구간별)")]
        [Tooltip("열기 비율이 높을수록 큰 배율. minNormalized 오름차순으로 넣는다.")]
        public List<HeatMultiplierTier> multiplierTiers = new List<HeatMultiplierTier>
        {
            new HeatMultiplierTier { label = "Chill", minNormalized = 0.00f, multiplier = 1f },
            new HeatMultiplierTier { label = "Singalong", minNormalized = 0.50f, multiplier = 3f },
            new HeatMultiplierTier { label = "Mosh", minNormalized = 0.80f, multiplier = 5f },
        };

        public HeatStage ResolveStage(float rawHype)
        {
            float singalong = Mathf.Min(singalongMinHype, moshMinHype);
            float mosh = Mathf.Max(singalongMinHype, moshMinHype);
            if (rawHype >= mosh) return HeatStage.Mosh;
            if (rawHype >= singalong) return HeatStage.Singalong;
            return HeatStage.Chill;
        }

        /// <summary>판정 → 증감량 변환. 카드 담당은 이 함수를 직접 쓸 일 없음 (HypeSystem 이 내부에서 사용).</summary>
        public float GetDelta(HypeJudgement judgement)
        {
            switch (judgement)
            {
                case HypeJudgement.Perfect:  return perfectDelta;
                case HypeJudgement.Good:     return goodDelta;
                case HypeJudgement.Miss:     return missDelta;
                case HypeJudgement.RiskMiss: return riskMissDelta;
                default:                     return 0f;
            }
        }

        /// <summary>
        /// 현재 열기 비율(0~1)에 해당하는 점수 배율. 사운드/관객과 같은 HypeTierUtil 규칙으로 단계를 나눈다.
        /// 티어가 비어 있으면 배율 1(원점수 그대로).
        /// </summary>
        public float GetMultiplier(float normalized)
        {
            if (multiplierTiers == null || multiplierTiers.Count == 0) return 1f;
            int index = HypeTierUtil.Resolve(multiplierTiers, Mathf.Clamp01(normalized));
            return index >= 0 ? multiplierTiers[index].multiplier : 1f;
        }
    }
}
