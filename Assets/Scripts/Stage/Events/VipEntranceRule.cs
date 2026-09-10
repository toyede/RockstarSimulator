using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 4 「VIP석 입장」 (arena_vip_entrance). 조건형: 점수가 목표의 triggerScoreRatio 에 닿으면 1회.
    /// 까다로운 VIP vipCount 명이 낮은 몰입도로 입장한다. 창 안에 남아 있는 VIP 전원을 safeEngagement 이상으로.
    /// 성공 = 목표 점수의 bonusRatio 가산. 실패 = 남은 VIP 퇴장 + 일반 관객 extraLeaveOnFail 명 동반 이탈.
    /// </summary>
    public sealed class VipEntranceRule : StageEventRule
    {
        [Header("VIP석 입장")]
        [SerializeField, Range(0.1f, 1f), Tooltip("점수가 목표의 이 비율에 닿으면 시작")] float triggerScoreRatio = 0.5f;
        [SerializeField, Range(1, 5)] int vipCount = 3;
        [SerializeField, Range(0f, 100f), Tooltip("VIP 입장 몰입도")] float entryEngagement = 30f;
        [SerializeField, Range(0f, 100f), Tooltip("이 몰입도 이상이면 만족")] float safeEngagement = 60f;
        [SerializeField, Range(0f, 1f), Tooltip("성공 시 목표 점수 대비 가산 점수")] float bonusRatio = 0.10f;
        [SerializeField, Min(0), Tooltip("실패 시 VIP 외에 동반 이탈하는 일반 관객 수")] int extraLeaveOnFail = 1;

        readonly List<AudienceId> _vips = new List<AudienceId>();
        bool _triggered;
        System.Random _random;

        protected override string Instruction =>
            $"VIP {_vips.Count}명이 입장했습니다! {window:0}초 안에 전원의 몰입도를 {safeEngagement:0} 이상으로 올리세요.";

        protected override string ProgressText
        {
            get
            {
                CountVips(out int alive, out int satisfied);
                return $"만족 {satisfied}/{alive}";
            }
        }

        protected override void OnEventRuleActivate(StageRuleContext context)
        {
            _triggered = false;
            _vips.Clear();
            _random = new System.Random(unchecked(context.RunSeed ^ 0x71B));
        }

        protected override void OnIdleTick()
        {
            if (_triggered) return;
            int target = PerformanceTimer.TargetScore;
            int score = GameManager.HasInstance ? GameManager.Instance.Score : 0;
            if (target <= 0 || score < target * triggerScoreRatio) return;
            if (TryBegin()) _triggered = true;
        }

        protected override void OnEventBegin()
        {
            _vips.Clear();
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null) return;
            for (int i = 0; i < vipCount; i++)
            {
                var preference = (CrowdPreference)_random.Next(3);
                if (roster.TryAdd(preference, entryEngagement, AudienceJoinReason.RuntimeCommand, out AudienceSnapshot vip))
                    _vips.Add(vip.Id);
            }
        }

        protected override bool CheckSuccess()
        {
            CountVips(out int alive, out int satisfied);
            return alive > 0 && alive == _vips.Count && satisfied == alive;
        }

        protected override string OnEventEnd(bool success)
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (success)
            {
                int bonus = Mathf.RoundToInt(PerformanceTimer.TargetScore * bonusRatio);
                if (bonus > 0 && GameManager.HasInstance) GameManager.Instance.AddScore(bonus);
                _vips.Clear();
                return $"VIP 전원이 만족했습니다! 보너스 {bonus:N0}점.";
            }

            int removed = 0;
            if (roster != null)
            {
                for (int i = 0; i < _vips.Count; i++)
                    if (roster.TryRemove(_vips[i], AudienceDepartureReason.RuntimeRemoval, out _)) removed++;
                for (int i = 0; i < extraLeaveOnFail; i++)
                    if (RemoveLowestEngagement(AudienceDepartureReason.RuntimeRemoval)) removed++;
            }
            _vips.Clear();
            return $"VIP가 자리를 떴습니다. 관객 {removed}명 이탈.";
        }

        void CountVips(out int alive, out int satisfied)
        {
            alive = 0;
            satisfied = 0;
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null) return;
            for (int i = 0; i < _vips.Count; i++)
            {
                if (!roster.TryGet(_vips[i], out AudienceSnapshot vip)) continue;
                alive++;
                if (vip.Engagement >= safeEngagement) satisfied++;
            }
        }
    }
}
