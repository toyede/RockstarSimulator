using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 1 — 지나가는 관객 (busking_walk_in). (기획서 §5)
    ///
    /// 새 계산식을 추가하지 않는다. 특별 관객·위기는 디렉터의 기본 상태(OFF)를 그대로 두고,
    /// 관객 유입·개별 호응도·이탈만 가장 읽기 쉬운 환경에서 보여주는 스테이지라
    /// 이 룰은 "켜져 있다"는 사실만 남긴다. (결과·통계 확장 시 여기서 유입 수를 셀 수 있다)
    /// </summary>
    public sealed class BuskingWalkInRule : StageRuleBehaviour
    {
        [SerializeField, Tooltip("이 스테이지에서 자연 유입으로 들어온 관객 수 (읽기 전용)")]
        int walkInCount;

        public int WalkInCount => walkInCount;

        protected override void OnActivate(StageRuleContext context)
        {
            walkInCount = 0;
            GameJamKit.EventBus.Subscribe<AudienceJoined>(OnAudienceJoined);
        }

        protected override void OnDeactivate()
        {
            GameJamKit.EventBus.Unsubscribe<AudienceJoined>(OnAudienceJoined);
        }

        void OnAudienceJoined(AudienceJoined e)
        {
            if (e.Reason == AudienceJoinReason.NaturalArrival) walkInCount++;
        }
    }
}
