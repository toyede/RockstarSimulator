using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 3 「소나기」 (festival_rain_shower). 45초에 12초 동안 관객 몰입도 자연 감소가 2배가 된다.
    /// CHILL 카드로 좋은 반응을 내면 '우산' — 전 관객 몰입도가 조금 회복된다.
    /// 성공 = 창이 닫혔을 때 관객 수가 시작 때 이상. 실패해도 추가 벌칙은 없다 (자연 이탈이 벌칙).
    /// </summary>
    public sealed class RainShowerRule : StageEventRule
    {
        [Header("소나기")]
        [SerializeField, Min(1f), Tooltip("비 오는 동안 몰입도 자연 감소 배율")] float decayMultiplier = 2f;
        [SerializeField, Tooltip("우산이 되는 카드 성향")] CrowdPreference umbrellaPreference = CrowdPreference.Chill;
        [SerializeField, Min(0f), Tooltip("우산 1회당 전 관객 몰입도 회복")] float umbrellaGain = 6f;

        int _startCount;
        int _umbrellas;

        protected override string Instruction =>
            $"몰입도가 빠르게 식습니다! {PreferenceLabel(umbrellaPreference)} 카드로 좋은 반응을 내면 우산이 됩니다.";

        protected override string ProgressText => $"우산 {_umbrellas}회 · 관객 {(Context.AudienceRoster != null ? Context.AudienceRoster.Count : 0)}/{_startCount}";

        protected override void OnEventBegin()
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            _startCount = roster != null ? roster.Count : 0;
            _umbrellas = 0;
            if (roster != null) roster.EngagementDecayMultiplier = decayMultiplier;
        }

        protected override void OnCardResolved(CardResolved e)
        {
            if (!IsEventActive) return;
            if (e.TargetPreference != umbrellaPreference || e.CrowdReaction == CrowdReactionGrade.Weak) return;
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null) return;
            roster.ApplyFeverEngagementPulse(umbrellaGain);
            _umbrellas++;
        }

        protected override bool CheckSuccess() => false; // 창이 끝날 때 판정

        protected override bool OnWindowExpired()
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            return roster != null && roster.Count >= _startCount;
        }

        protected override string OnEventEnd(bool success)
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster != null) roster.EngagementDecayMultiplier = 1f;
            int now = roster != null ? roster.Count : 0;
            return success
                ? $"비가 그쳤습니다. 관객 {now}명 전원이 자리를 지켰습니다 (우산 {_umbrellas}회)."
                : $"비에 지친 관객 {Mathf.Max(0, _startCount - now)}명이 떠났습니다.";
        }

        protected override void OnEventCancel()
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster != null) roster.EngagementDecayMultiplier = 1f;
        }
    }
}
