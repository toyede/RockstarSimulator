using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 룰이 필요로 하는 읽기 전용 참조를 한 번에 전달한다. (기획서 §10 StageRuleContext)
    /// UI 오브젝트나 카드 프리팹은 넘기지 않는다 — 화면은 EventBus 결과를 구독한다.
    /// </summary>
    public readonly struct StageRuleContext
    {
        public StageRuleContext(
            StageDefinition stage,
            AudienceStagePreset audiencePreset,
            AudienceRosterSystem audienceRoster,
            PerformanceTimerSystem timer,
            SpecialAudienceManager specialAudience,
            NearbyConcertCrisisDirector crisisDirector,
            int runSeed)
        {
            Stage = stage;
            AudiencePreset = audiencePreset;
            AudienceRoster = audienceRoster;
            Timer = timer;
            SpecialAudience = specialAudience;
            CrisisDirector = crisisDirector;
            RunSeed = runSeed;
        }

        public StageDefinition Stage { get; }
        public AudienceStagePreset AudiencePreset { get; }
        public AudienceRosterSystem AudienceRoster { get; }
        public PerformanceTimerSystem Timer { get; }
        public SpecialAudienceManager SpecialAudience { get; }
        public NearbyConcertCrisisDirector CrisisDirector { get; }

        /// <summary>런 시드. 같은 런에서 같은 결과가 나오게 하고 싶은 룰이 쓴다.</summary>
        public int RunSeed { get; }

        public string StageId => Stage != null ? Stage.StageId : string.Empty;
    }

    /// <summary>공연장 룰 하나. (기획서 §10 IStageRule) 점수·UI·증강을 직접 수정하지 않는다.</summary>
    public interface IStageRule
    {
        string RuleId { get; }
        bool IsActive { get; }
        void Activate(StageRuleContext context);
        void Deactivate();
    }

    /// <summary>
    /// 씬에 배치하는 룰 컴포넌트의 공통 뼈대. StageRuntimeDirector 가 자식에서 찾아 ID 로 켠다.
    /// Activate/Deactivate 는 중복 호출에 안전하고, 컴포넌트가 꺼지면 스스로 해제된다.
    /// </summary>
    public abstract class StageRuleBehaviour : MonoBehaviour, IStageRule
    {
        [SerializeField, Tooltip("StageDefinition.venueRuleIds 에 적는 값")]
        string ruleId = "";

        public string RuleId => ruleId;
        public bool IsActive { get; private set; }
        protected StageRuleContext Context { get; private set; }

        public void Activate(StageRuleContext context)
        {
            if (IsActive) Deactivate();
            Context = context;
            IsActive = true;
            OnActivate(context);
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            OnDeactivate();
            Context = default;
        }

        protected abstract void OnActivate(StageRuleContext context);
        protected abstract void OnDeactivate();

        protected virtual void OnDisable() => Deactivate();

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorSetRuleId(string id) => ruleId = id ?? string.Empty;

        protected virtual void OnValidate()
        {
            ruleId = ruleId == null ? string.Empty : ruleId.Trim();
        }
#endif
    }
}
