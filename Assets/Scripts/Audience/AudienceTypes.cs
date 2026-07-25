using System;

namespace ContextStage
{
    public readonly struct AudienceId : IEquatable<AudienceId>
    {
        public AudienceId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(AudienceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AudienceId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "Invalid";

        public static bool operator ==(AudienceId left, AudienceId right) => left.Equals(right);
        public static bool operator !=(AudienceId left, AudienceId right) => !left.Equals(right);
    }

    public enum AudienceEngagementStage
    {
        Calm,
        Middle,
        Excited
    }

    public enum AudienceJoinReason
    {
        Initialization,
        NaturalArrival,
        SmallTalkReplacement,
        RuntimeCommand
    }

    public enum AudienceChangeReason
    {
        NaturalDecay,
        CardReaction,
        RuntimeCommand
    }

    public enum AudienceDepartureReason
    {
        EngagementDepleted,
        SmallTalkReplacement,
        RuntimeRemoval,
        Reset,
        NearbyConcert
    }

    public enum AudienceExitStyle
    {
        Default,
        NearbyConcert
    }

    public readonly struct AudienceSnapshot
    {
        public AudienceSnapshot(
            AudienceId id,
            CrowdPreference preference,
            float engagement,
            AudienceEngagementStage stage,
            float joinedAt)
        {
            Id = id;
            Preference = preference;
            Engagement = engagement;
            Stage = stage;
            JoinedAt = joinedAt;
        }

        public AudienceId Id { get; }
        public CrowdPreference Preference { get; }
        public float Engagement { get; }
        public AudienceEngagementStage Stage { get; }
        public float JoinedAt { get; }

        internal AudienceSnapshot WithEngagement(
            float engagement,
            AudienceEngagementStage stage) =>
            new AudienceSnapshot(Id, Preference, engagement, stage, JoinedAt);
    }

    public readonly struct AudienceStateChange
    {
        public AudienceStateChange(AudienceSnapshot previous, AudienceSnapshot current)
        {
            Previous = previous;
            Current = current;
        }

        public AudienceSnapshot Previous { get; }
        public AudienceSnapshot Current { get; }
        public bool StageChanged => Previous.Stage != Current.Stage;
    }

    public readonly struct AudienceSummary
    {
        public AudienceSummary(
            int count,
            float averageEngagement,
            int calmCount,
            int middleCount,
            int excitedCount)
        {
            Count = count;
            AverageEngagement = averageEngagement;
            CalmCount = calmCount;
            MiddleCount = middleCount;
            ExcitedCount = excitedCount;
        }

        public int Count { get; }
        public float AverageEngagement { get; }
        public int CalmCount { get; }
        public int MiddleCount { get; }
        public int ExcitedCount { get; }
    }
}
