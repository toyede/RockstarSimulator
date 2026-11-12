namespace ContextStage
{
    /// <summary>스테이지 기믹 이벤트 시작. 알림 UI 가 구독한다.</summary>
    public readonly struct StageEventStarted
    {
        public StageEventStarted(string eventId, string title, string instruction, float duration)
        {
            EventId = eventId;
            Title = title;
            Instruction = instruction;
            Duration = duration;
        }

        public string EventId { get; }
        public string Title { get; }
        public string Instruction { get; }
        public float Duration { get; }
    }

    /// <summary>이벤트 진행 중 매 프레임 (남은 시간·진행도 문구).</summary>
    public readonly struct StageEventProgress
    {
        public StageEventProgress(string eventId, float remaining, float duration, string progressText)
        {
            EventId = eventId;
            Remaining = remaining;
            Duration = duration;
            ProgressText = progressText;
        }

        public string EventId { get; }
        public float Remaining { get; }
        public float Duration { get; }
        public string ProgressText { get; }
    }

    /// <summary>이벤트 결과. 보상·벌칙은 이미 적용된 뒤 발행된다. 기록 수집기가 성공/전체 수를 센다.</summary>
    public readonly struct StageEventResolved
    {
        public StageEventResolved(string eventId, string title, bool success, string resultText)
        {
            EventId = eventId;
            Title = title;
            Success = success;
            ResultText = resultText;
        }

        public string EventId { get; }
        public string Title { get; }
        public bool Success { get; }
        public string ResultText { get; }
    }
}
