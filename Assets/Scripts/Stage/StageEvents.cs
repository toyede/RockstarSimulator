namespace ContextStage
{
    /// <summary>
    /// StageRuntimeDirector 가 스테이지 데이터를 다 적용한 직후 발행. 배경·BGM·조명 프리셋 등
    /// "곁다리로 같이 반응하는" 표현 담당이 구독한다. Stage 가 null 이면 스테이지 없이(씬 기본값) 시작한 것.
    /// </summary>
    public readonly struct StageRuntimeApplied
    {
        public StageRuntimeApplied(StageDefinition stage, AudienceStagePreset audiencePreset)
        {
            Stage = stage;
            AudiencePreset = audiencePreset;
        }

        public StageDefinition Stage { get; }
        public AudienceStagePreset AudiencePreset { get; }
        public string StageId => Stage != null ? Stage.StageId : string.Empty;
    }

    /// <summary>
    /// 보스 룰: 라이벌이 특정 팬층을 노린다는 경고가 시작될 때 발행.
    /// (관객 외곽선·이탈 연출은 기존 AudienceCrisis* 이벤트를 그대로 재사용하고,
    ///  보스 전용 문구·전광판 아이콘은 이 이벤트로 그린다)
    /// </summary>
    public readonly struct RivalAttackWarningStarted
    {
        public RivalAttackWarningStarted(
            int attackIndex,
            CrowdPreference targetPreference,
            AudienceId[] targets,
            float duration,
            float safeEngagement,
            string rivalName)
        {
            AttackIndex = attackIndex;
            TargetPreference = targetPreference;
            Targets = targets;
            Duration = duration;
            SafeEngagement = safeEngagement;
            RivalName = rivalName;
        }

        public int AttackIndex { get; }
        public CrowdPreference TargetPreference { get; }
        public AudienceId[] Targets { get; }
        public float Duration { get; }
        public float SafeEngagement { get; }
        public string RivalName { get; }
    }

    /// <summary>보스 룰: 공격 하나가 끝났을 때 발행.</summary>
    public readonly struct RivalAttackResolved
    {
        public RivalAttackResolved(
            int attackIndex,
            CrowdPreference targetPreference,
            int threatenedCount,
            int retainedCount,
            int stolenCount,
            bool defendedBySpecialHit)
        {
            AttackIndex = attackIndex;
            TargetPreference = targetPreference;
            ThreatenedCount = threatenedCount;
            RetainedCount = retainedCount;
            StolenCount = stolenCount;
            DefendedBySpecialHit = defendedBySpecialHit;
        }

        public int AttackIndex { get; }
        public CrowdPreference TargetPreference { get; }
        public int ThreatenedCount { get; }
        public int RetainedCount { get; }
        public int StolenCount { get; }
        public bool DefendedBySpecialHit { get; }
    }
}
