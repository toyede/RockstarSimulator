namespace ContextStage
{
    public enum AudienceCrisisState
    {
        Dormant,
        Armed,
        Warning,
        Resolving,
        Completed
    }

    public readonly struct AudienceCrisisWarningStarted
    {
        public AudienceCrisisWarningStarted(
            AudienceId[] threatenedAudience,
            float duration,
            float retentionEngagement)
        {
            ThreatenedAudience = threatenedAudience;
            Duration = duration;
            RetentionEngagement = retentionEngagement;
        }

        public AudienceId[] ThreatenedAudience { get; }
        public float Duration { get; }
        public float RetentionEngagement { get; }
    }

    public readonly struct AudienceCrisisProgressChanged
    {
        public AudienceCrisisProgressChanged(
            float remaining,
            int threatenedCount,
            int securedCount,
            int specialSaveCount)
        {
            Remaining = remaining;
            ThreatenedCount = threatenedCount;
            SecuredCount = securedCount;
            SpecialSaveCount = specialSaveCount;
        }

        public float Remaining { get; }
        public int ThreatenedCount { get; }
        public int SecuredCount { get; }
        public int SpecialSaveCount { get; }
    }

    public readonly struct AudienceCrisisTargetsChanged
    {
        public AudienceCrisisTargetsChanged(AudienceId[] targets, bool active)
        {
            Targets = targets;
            Active = active;
        }

        public AudienceId[] Targets { get; }
        public bool Active { get; }
    }

    public readonly struct AudienceCrisisDepartureStarted
    {
        public AudienceCrisisDepartureStarted(int departureCount)
        {
            DepartureCount = departureCount;
        }

        public int DepartureCount { get; }
    }

    public readonly struct AudienceCrisisDepartureEnded
    {
    }

    public readonly struct AudienceCrisisResolved
    {
        public AudienceCrisisResolved(
            int threatenedCount,
            int retainedCount,
            int departedCount)
        {
            ThreatenedCount = threatenedCount;
            RetainedCount = retainedCount;
            DepartedCount = departedCount;
        }

        public int ThreatenedCount { get; }
        public int RetainedCount { get; }
        public int DepartedCount { get; }
    }

    public readonly struct AudienceCrisisCancelled
    {
    }
}
