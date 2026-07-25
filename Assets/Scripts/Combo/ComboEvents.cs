namespace ContextStage
{
    public readonly struct ComboChanged
    {
        public ComboChanged(
            int previousCombo,
            int currentCombo,
            float multiplier,
            bool wasLost)
        {
            PreviousCombo = previousCombo;
            CurrentCombo = currentCombo;
            Multiplier = multiplier;
            WasLost = wasLost;
        }

        public int PreviousCombo { get; }
        public int CurrentCombo { get; }
        public float Multiplier { get; }
        public bool WasLost { get; }
    }

    public readonly struct FeverStateChanged
    {
        public FeverStateChanged(
            bool isActive,
            int triggerCombo,
            float duration)
        {
            IsActive = isActive;
            TriggerCombo = triggerCombo;
            Duration = duration;
        }

        public bool IsActive { get; }
        public int TriggerCombo { get; }
        public float Duration { get; }
    }

    public readonly struct FeverBonusAwarded
    {
        public FeverBonusAwarded(
            string cardId,
            int audienceCount,
            int bonusScore)
        {
            CardId = cardId;
            AudienceCount = audienceCount;
            BonusScore = bonusScore;
        }

        public string CardId { get; }
        public int AudienceCount { get; }
        public int BonusScore { get; }
    }
}
