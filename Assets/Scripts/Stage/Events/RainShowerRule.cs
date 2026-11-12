using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 3 「소나기」 (festival_rain_shower). 45초에 12초 동안 관객 몰입도 자연 감소가 2배가 된다.
    /// 비가 그칠 때까지 시작 관객을 붙잡는다. 새로 들어온 관객은 목표에 세지 않는다.
    /// </summary>
    public sealed class RainShowerRule : StageEventRule
    {
        [Header("소나기")]
        [SerializeField, Min(1f), Tooltip("비 오는 동안 몰입도 자연 감소 배율")] float decayMultiplier = 2f;
        [SerializeField, Min(0), Tooltip("허용하는 시작 관객 이탈 수. 최소 한 명은 남아야 한다.")]
        int allowedDepartures = 1;

        readonly List<AudienceId> _startingAudience = new List<AudienceId>();
        AudienceRosterSystem _rainRoster;
        float _previousDecayMultiplier;
        int RequiredSurvivors => Mathf.Max(1, _startingAudience.Count - allowedDepartures);

        protected override string Instruction =>
            $"몰입도가 더 빨리 줄어요. 비가 그칠 때까지 알맞은 카드로 기존 관객 {RequiredSurvivors}명 이상을 지키세요.";

        protected override string ProgressText => $"기존 관객 {CountSurvivors()}/{_startingAudience.Count}명 · 목표 {RequiredSurvivors}명";

        protected override bool CanBegin() => base.CanBegin() &&
            Context.AudienceRoster != null && Context.AudienceRoster.Count > 0;

        protected override void OnEventBegin()
        {
            _startingAudience.Clear();
            _rainRoster = Context.AudienceRoster;
            foreach (var member in _rainRoster.Members)
                _startingAudience.Add(member.Id);
            _previousDecayMultiplier = _rainRoster.EngagementDecayMultiplier;
            _rainRoster.EngagementDecayMultiplier = _previousDecayMultiplier * decayMultiplier;
        }

        protected override bool CheckSuccess() => false; // 창이 끝날 때 판정

        int CountSurvivors()
        {
            if (_rainRoster == null) return 0;
            int remaining = 0;
            foreach (var id in _startingAudience)
                if (_rainRoster.TryGet(id, out _)) remaining++;
            return remaining;
        }

        protected override bool OnWindowExpired() => CountSurvivors() >= RequiredSurvivors;

        protected override string OnEventEnd(bool success)
        {
            string remaining = $"기존 관객 {CountSurvivors()}/{_startingAudience.Count}명";
            RestoreDecay();
            return success
                ? $"비가 그쳤습니다. {remaining}이 공연을 끝까지 함께했어요."
                : $"비가 그쳤습니다. {remaining}이 남아 목표에 못 미쳤어요.";
        }

        protected override void OnEventCancel() => RestoreDecay();

        void RestoreDecay()
        {
            if (_rainRoster != null) _rainRoster.EngagementDecayMultiplier = _previousDecayMultiplier;
            _rainRoster = null;
            _startingAudience.Clear();
        }
    }
}
