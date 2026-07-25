namespace ContextStage
{
    public readonly struct AudienceJoined
    {
        public AudienceJoined(AudienceSnapshot audience, AudienceJoinReason reason)
        {
            Audience = audience;
            Reason = reason;
        }

        public AudienceSnapshot Audience { get; }
        public AudienceJoinReason Reason { get; }
    }

    public readonly struct AudienceStateChanged
    {
        public AudienceStateChanged(AudienceStateChange change, AudienceChangeReason reason)
        {
            Previous = change.Previous;
            Current = change.Current;
            Reason = reason;
        }

        public AudienceSnapshot Previous { get; }
        public AudienceSnapshot Current { get; }
        public AudienceChangeReason Reason { get; }
        public bool StageChanged => Previous.Stage != Current.Stage;
    }

    public readonly struct AudienceDeparted
    {
        public AudienceDeparted(AudienceSnapshot audience, AudienceDepartureReason reason)
        {
            Audience = audience;
            Reason = reason;
        }

        public AudienceSnapshot Audience { get; }
        public AudienceDepartureReason Reason { get; }
    }

    public readonly struct AudienceCardReacted
    {
        public AudienceCardReacted(
            string cardId,
            int reactionValue,
            float engagementDelta,
            AudienceSnapshot previous,
            AudienceSnapshot current)
        {
            CardId = cardId;
            ReactionValue = reactionValue;
            EngagementDelta = engagementDelta;
            Previous = previous;
            Current = current;
        }

        public string CardId { get; }
        public int ReactionValue { get; }
        public float EngagementDelta { get; }
        public AudienceSnapshot Previous { get; }
        public AudienceSnapshot Current { get; }
    }

    public readonly struct AudienceSummaryChanged
    {
        public AudienceSummaryChanged(AudienceSummary summary)
        {
            Summary = summary;
        }

        public AudienceSummary Summary { get; }
    }
}
