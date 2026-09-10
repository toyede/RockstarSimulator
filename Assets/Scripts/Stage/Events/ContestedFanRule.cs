using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 5 「스탠딩석 팬 쟁탈」 (stadium_contested_fan). 35·85초에 스탠딩석의 팬 한 명이 성향을 외친다.
    /// 창 안에 그 성향 카드로 좋은 반응을 내면 우리 쪽으로 합류. 실패해도 잃는 것은 없다 (라이벌 쪽으로 갈 뿐).
    /// 보스 패턴(예고 포함)이 진행 중이면 끝날 때까지 미룬다 — 보스전은 이미 바쁘므로 가볍게.
    /// </summary>
    public sealed class ContestedFanRule : StageEventRule
    {
        [Header("팬 쟁탈")]
        [SerializeField, Range(0f, 100f), Tooltip("합류 시 몰입도")] float joinEngagement = 55f;

        CrowdPreference _preference;
        bool _won;
        System.Random _random;
        BossBattleRule _boss;

        protected override string Instruction => $"스탠딩석의 팬이 {PreferenceLabel(_preference)}을(를) 외칩니다! 그 성향 카드로 좋은 반응을!";

        protected override void OnEventRuleActivate(StageRuleContext context)
        {
            _random = new System.Random(unchecked(context.RunSeed ^ 0xFA4));
            _boss = FindFirstObjectByType<BossBattleRule>();
        }

        protected override bool CanBegin()
        {
            if (!base.CanBegin()) return false;
            if (_boss != null && _boss.IsActive && (_boss.IsPatternActive || _boss.IsLeadIn || _boss.IsDefeated)) return false;
            return true;
        }

        protected override void OnEventBegin()
        {
            _preference = (CrowdPreference)_random.Next(3);
            _won = false;
        }

        protected override void OnCardResolved(CardResolved e)
        {
            if (!IsEventActive || _won) return;
            if (e.TargetPreference == _preference && e.CrowdReaction != CrowdReactionGrade.Weak) _won = true;
        }

        protected override bool CheckSuccess() => _won;

        protected override string OnEventEnd(bool success)
        {
            if (!success) return $"{PreferenceLabel(_preference)} 팬은 라이벌 무대로 갔습니다.";
            AudienceRosterSystem roster = Context.AudienceRoster;
            bool joined = roster != null && roster.TryAdd(_preference, joinEngagement, AudienceJoinReason.RuntimeCommand, out _);
            return joined
                ? $"{PreferenceLabel(_preference)} 팬이 우리 무대로 넘어왔습니다!"
                : $"{PreferenceLabel(_preference)} 팬이 환호했지만 만석입니다.";
        }
    }
}
