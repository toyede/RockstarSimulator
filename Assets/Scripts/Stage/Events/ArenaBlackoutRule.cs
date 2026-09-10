using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 4 「정전」 (arena_blackout). 생방송 중 불이 꺼진다 — 관객이 실루엣이 되어 성향을 볼 수 없고 BGM 도 멈춘다.
    /// 창(8초) 동안 카드를 1장 이상 내고 Miss 가 한 번도 없으면 목표 점수의 bonusRatio 를 가산한다.
    /// 벌칙은 없다. 대신 Stage 4 목표 점수를 올려 두어 정전 보너스 없이는 클리어가 어렵다 (2회 중 1회 이상 성공 전제).
    /// 카드 판정 텍스트는 그대로 보인다.
    /// </summary>
    [RequireComponent(typeof(BlackoutPresentation))]
    public sealed class ArenaBlackoutRule : StageEventRule
    {
        [Header("정전")]
        [SerializeField, Range(0f, 1f), Tooltip("무 Miss 성공 시 목표 점수 대비 가산")] float bonusRatio = 0.15f;
        [SerializeField, Min(0), Tooltip("성공으로 인정하려면 창 안에 낸 카드 수")] int minimumCards = 1;
        [SerializeField, Min(0f), Tooltip("성공 시 전 관객 몰입도 회복")] float engagementGain = 5f;

        BlackoutPresentation _presentation;
        int _cards;
        bool _missed;

        protected override string Instruction => $"정전! {window:0}초 동안 관객을 기억해 카드를 내세요. Miss 없이 버티면 보너스.";
        protected override string ProgressText => _missed ? $"카드 {_cards}장 · Miss 발생" : $"카드 {_cards}장 · Miss 0";

        protected override void OnEventRuleActivate(StageRuleContext context)
        {
            _presentation = GetComponent<BlackoutPresentation>();
        }

        protected override void OnEventBegin()
        {
            _cards = 0;
            _missed = false;
            if (_presentation != null) _presentation.Begin();
        }

        protected override void OnCardResolved(CardResolved e)
        {
            if (!IsEventActive) return;
            _cards++;
            if (e.Judgement == HypeJudgement.Miss || e.Judgement == HypeJudgement.RiskMiss) _missed = true;
        }

        // 정전은 정해진 시간 동안 이어진다. 판정은 창이 닫힐 때
        protected override bool CheckSuccess() => false;
        protected override bool OnWindowExpired() => !_missed && _cards >= minimumCards;

        protected override string OnEventEnd(bool success)
        {
            if (_presentation != null) _presentation.End();
            if (!success)
            {
                return _cards < minimumCards
                    ? "방송 복구. 정전 중에 카드를 내지 않아 보너스가 없습니다."
                    : "방송 복구. Miss 가 나와 보너스를 놓쳤습니다.";
            }

            int bonus = Mathf.RoundToInt(PerformanceTimer.TargetScore * bonusRatio);
            if (bonus > 0 && GameManager.HasInstance) GameManager.Instance.AddScore(bonus);
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster != null && engagementGain > 0f) roster.ApplyFeverEngagementPulse(engagementGain);
            return $"방송 복구! 암전 속에서도 실수 없는 연주 — 보너스 {bonus:N0}점.";
        }

        protected override void OnEventCancel()
        {
            if (_presentation != null) _presentation.End(immediate: true);
        }
    }
}
