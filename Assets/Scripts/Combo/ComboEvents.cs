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
}
