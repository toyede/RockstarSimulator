namespace ContextStage
{
    /// <summary>특수 관객 드롭 판정이 카드 사용에 넘기는 최소 컨텍스트.</summary>
    public readonly struct SpecialCardRequest
    {
        public static SpecialCardRequest None => default;

        public SpecialCardRequest(HeatStage requestedStage)
        {
            IsActive = true;
            RequestedStage = requestedStage;
        }

        public bool IsActive { get; }
        public HeatStage RequestedStage { get; }
    }
}
