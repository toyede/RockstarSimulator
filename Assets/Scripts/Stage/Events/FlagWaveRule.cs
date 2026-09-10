using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 3 「깃발 웨이브」 (festival_flag_wave). 조건형: 콤보가 triggerCombo 에 닿으면 시작 (쿨다운·횟수 제한).
    /// 창 안에 Good 이상 카드 requiredCards 장 → 전 관객 몰입도 회복 + 관객 합류. 실패해도 벌칙 없음 (보상형).
    /// </summary>
    public sealed class FlagWaveRule : StageEventRule
    {
        [Header("깃발 웨이브")]
        [SerializeField, Min(1), Tooltip("이 콤보에 닿으면 시작")] int triggerCombo = 10;
        [SerializeField, Min(0f), Tooltip("다음 발동까지 쿨다운(초)")] float cooldown = 25f;
        [SerializeField, Min(1), Tooltip("한 공연 최대 발동 횟수")] int maxTriggers = 3;
        [SerializeField, Min(1), Tooltip("창 안에 필요한 Good 이상 카드 수")] int requiredCards = 2;
        [SerializeField, Min(0f), Tooltip("성공 시 전 관객 몰입도 회복")] float engagementGain = 8f;
        [SerializeField, Min(0), Tooltip("성공 시 합류하는 관객 수")] int joinCount = 2;

        int _goodCards;
        int _triggers;
        float _nextAllowedAt;

        protected override string Instruction => $"관객이 깃발을 흔듭니다! {window:0}초 안에 Good 이상 카드 {requiredCards}장!";
        protected override string ProgressText => $"{_goodCards}/{requiredCards}";

        protected override void OnEventRuleActivate(StageRuleContext context)
        {
            _triggers = 0;
            _nextAllowedAt = 0f;
        }

        protected override void OnComboChanged(ComboChanged e)
        {
            if (IsEventActive || _triggers >= maxTriggers) return;
            if (e.CurrentCombo < triggerCombo || PerformanceTimer.Elapsed < _nextAllowedAt) return;
            if (TryBegin()) _triggers++;
        }

        protected override void OnEventBegin() => _goodCards = 0;

        protected override void OnCardResolved(CardResolved e)
        {
            if (!IsEventActive) return;
            if (e.Judgement == HypeJudgement.Perfect || e.Judgement == HypeJudgement.Good) _goodCards++;
        }

        protected override bool CheckSuccess() => _goodCards >= requiredCards;

        protected override string OnEventEnd(bool success)
        {
            _nextAllowedAt = PerformanceTimer.Elapsed + cooldown;
            if (!success) return "웨이브가 흐지부지 끝났습니다.";

            AudienceRosterSystem roster = Context.AudienceRoster;
            int joined = 0;
            if (roster != null)
            {
                roster.ApplyFeverEngagementPulse(engagementGain);
                for (int i = 0; i < joinCount; i++)
                    if (roster.TryAddRandom(AudienceJoinReason.RuntimeCommand, out _)) joined++;
            }
            return joined > 0
                ? $"깃발이 물결쳤습니다! 관객 {joined}명이 합류했습니다."
                : "깃발이 물결쳤습니다! 관객 전원의 몰입도가 올랐습니다.";
        }
    }
}
