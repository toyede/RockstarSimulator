using GameJamKit; // HypeJudgement enum 이 킷 GameEvents.cs 에 있다 (킷 README "변경 이력" 참조)
using UnityEngine;

namespace ContextStage
{
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

        [Header("판정별 증감 (감소는 음수로 입력)")]
        [Tooltip("Perfect: 관객 맥락을 정확히 읽음")]
        public float perfectDelta = 30f;

        [Tooltip("Good: 완벽하진 않지만 분위기 유지")]
        public float goodDelta = 15f;

        [Tooltip("Miss: 관객과 맞지 않는 행동")]
        public float missDelta = -15f;

        [Tooltip("RiskMiss: 위험 카드(모쉬핏 등)의 완전 오판")]
        public float riskMissDelta = -25f;

        [Header("앙코르 (100 도달 보상)")]
        [Tooltip("앙코르 발동 후 호응도가 이 값으로 변경된다 (0이나 30으로 떨어뜨리면 보상감이 사라짐)")]
        public float encoreResetValue = 70f;

        [Tooltip("앙코르 직후 감소가 멈추는 시간(초). 플레이어가 새 카드를 확인할 여유")]
        public float encoreDecayPauseDuration = 1.5f;

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
    }
}
