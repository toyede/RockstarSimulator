namespace ContextStage
{
    /// <summary>화면 중앙 증강 알림의 종류. 문구와 색은 PassiveAugmentNoticeUI 인스펙터에서 정한다.</summary>
    public enum AugmentNoticeKind
    {
        BandmateCover,
        FeverExtension,
        BreathingRoom,
        PreAdvertising,
        PreferenceInsight,
        ExtraHand,
        HighRiskTempo,
        ShortSet,
        PerfectClear
    }

    /// <summary>증강 발동 문구를 띄우는 순수 연출 이벤트. Value 는 상세 문구의 {0} 에 들어간다.</summary>
    public readonly struct AugmentNoticeRequested
    {
        public AugmentNoticeRequested(AugmentNoticeKind kind, float value = 0f)
        {
            Kind = kind;
            Value = value;
        }

        public AugmentNoticeKind Kind { get; }
        public float Value { get; }
    }

    /// <summary>공연 시작 연출이 손패에서 카드를 사용 없이 뺄 때, 빠지기 직전에 발행한다.</summary>
    public readonly struct HandCardDiscarded
    {
        public HandCardDiscarded(int handIndex) { HandIndex = handIndex; }
        public int HandIndex { get; }
    }

    /// <summary>공연 시작 연출이 제한시간을 줄일 때 발행한다. 시계 UI 가 숫자를 굴려 보여준다.</summary>
    public readonly struct PerformanceTimeShortened
    {
        public PerformanceTimeShortened(float fromSeconds, float toSeconds)
        {
            FromSeconds = fromSeconds;
            ToSeconds = toSeconds;
        }

        public float FromSeconds { get; }
        public float ToSeconds { get; }
    }

    /// <summary>완벽주의 보상이 점수에 더해지기 직전에 발행한다. 점수 HUD 가 카운트업을 보여준다.</summary>
    public readonly struct PerfectClearScoreApplied
    {
        public PerfectClearScoreApplied(int previousScore, int finalScore, float multiplier)
        {
            PreviousScore = previousScore;
            FinalScore = finalScore;
            Multiplier = multiplier;
        }

        public int PreviousScore { get; }
        public int FinalScore { get; }
        public float Multiplier { get; }
    }
}
