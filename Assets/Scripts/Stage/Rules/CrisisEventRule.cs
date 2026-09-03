using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 옆 무대 관객 쟁탈 위기 / 성과 적응형 이벤트. (기획서 §7 festival_nearby_concert, §8 arena_adaptive_event)
    ///
    /// 두 룰 모두 기존 NearbyConcertCrisisDirector 를 그대로 쓰고 Config 만 다르다:
    ///   - festival_nearby_concert : useAdaptiveTrigger = false, encounterChance = 1, 진행률 38~52% 에 1회
    ///   - arena_adaptive_event    : useAdaptiveTrigger = true (앞서면 위기, 밀리면 지원, 55% 에 확정)
    /// 같은 컴포넌트를 두 개 두고 ruleId 와 config 만 다르게 지정한다.
    /// </summary>
    public sealed class CrisisEventRule : StageRuleBehaviour
    {
        [SerializeField, Tooltip("이 룰이 켜질 때 디렉터에 주입할 위기 설정")]
        AudienceCrisisConfig config;

        [SerializeField, Tooltip("이 스테이지에서 위기로부터 지킨 관객 수 (읽기 전용)")]
        int retainedCount;

        [SerializeField, Tooltip("이 스테이지에서 위기로 떠난 관객 수 (읽기 전용)")]
        int departedCount;

        public int RetainedCount => retainedCount;
        public int DepartedCount => departedCount;

        protected override void OnActivate(StageRuleContext context)
        {
            retainedCount = 0;
            departedCount = 0;
            EventBus.Subscribe<AudienceCrisisResolved>(OnCrisisResolved);

            NearbyConcertCrisisDirector director = context.CrisisDirector;
            if (director == null)
            {
                Debug.LogWarning("[StageRule] NearbyConcertCrisisDirector 가 없어 위기 룰을 켤 수 없습니다.", this);
                return;
            }

            if (config == null)
                Debug.LogWarning($"[StageRule] '{RuleId}' 룰에 AudienceCrisisConfig 가 비어 있어 디렉터 기본 설정을 씁니다.", this);

            director.ConfigureForStage(config, true);
        }

        protected override void OnDeactivate()
        {
            EventBus.Unsubscribe<AudienceCrisisResolved>(OnCrisisResolved);

            NearbyConcertCrisisDirector director = Context.CrisisDirector;
            if (director != null) director.ConfigureForStage(null, false);
        }

        void OnCrisisResolved(AudienceCrisisResolved e)
        {
            retainedCount += e.RetainedCount;
            departedCount += e.DepartedCount;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(AudienceCrisisConfig crisisConfig) => config = crisisConfig;
#endif
    }
}
