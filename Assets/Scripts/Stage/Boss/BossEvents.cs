namespace ContextStage
{
    /// <summary>보스 체력이 바뀔 때. UI 체력 바가 구독한다.</summary>
    public readonly struct BossHealthChanged
    {
        public BossHealthChanged(float current, float max, float delta, bool peakTime)
        {
            Current = current;
            Max = max;
            Delta = delta;
            PeakTime = peakTime;
        }

        public float Current { get; }
        public float Max { get; }
        public float Delta { get; }
        public bool PeakTime { get; }
        public float Normalized => Max <= 0f ? 0f : UnityEngine.Mathf.Clamp01(Current / Max);
    }

    /// <summary>패턴 예고 시작.</summary>
    public readonly struct BossPatternStarted
    {
        public BossPatternStarted(string patternId, string title, string instruction, float duration, bool enhanced)
        {
            PatternId = patternId;
            Title = title;
            Instruction = instruction;
            Duration = duration;
            Enhanced = enhanced;
        }

        public string PatternId { get; }
        public string Title { get; }
        public string Instruction { get; }
        public float Duration { get; }
        public bool Enhanced { get; }
    }

    /// <summary>패턴 진행 중 매 프레임.</summary>
    public readonly struct BossPatternProgress
    {
        public BossPatternProgress(string patternId, float remaining, float duration, string progressText, bool achieved)
        {
            PatternId = patternId;
            Remaining = remaining;
            Duration = duration;
            ProgressText = progressText;
            Achieved = achieved;
        }

        public string PatternId { get; }
        public float Remaining { get; }
        public float Duration { get; }
        public string ProgressText { get; }
        public bool Achieved { get; }
    }

    /// <summary>패턴 결과. 성공이면 피해·영입, 실패면 회복·이탈이 이미 적용된 뒤 발행된다.</summary>
    public readonly struct BossPatternResolved
    {
        public BossPatternResolved(
            string patternId,
            string title,
            bool success,
            int streak,
            float healthDelta,
            int fansMoved)
        {
            PatternId = patternId;
            Title = title;
            Success = success;
            Streak = streak;
            HealthDelta = healthDelta;
            FansMoved = fansMoved;
        }

        public string PatternId { get; }
        public string Title { get; }
        public bool Success { get; }

        /// <summary>지금까지 성공한 패턴 수 (누적).</summary>
        public int Streak { get; }

        /// <summary>보스 체력 변화 (성공이면 음수, 실패면 양수).</summary>
        public float HealthDelta { get; }

        /// <summary>성공이면 합류한 상대 팬 수, 실패면 떠난 우리 팬 수.</summary>
        public int FansMoved { get; }
    }

    /// <summary>체력 50% 이하 — PEAK TIME 진입.</summary>
    public readonly struct BossPeakTimeEntered
    {
    }

    /// <summary>보스 격파 (체력 0). 공연은 격파 연출 뒤 종료된다. ByStreak 는 예전 규칙용으로 항상 false.</summary>
    public readonly struct BossDefeated
    {
        public BossDefeated(bool byStreak, float remainingSeconds, int bonusScore)
        {
            ByStreak = byStreak;
            RemainingSeconds = remainingSeconds;
            BonusScore = bonusScore;
        }

        public bool ByStreak { get; }
        public float RemainingSeconds { get; }
        public int BonusScore { get; }
    }
}

namespace ContextStage
{
    /// <summary>보스 연출 종류.</summary>
    public enum BossCinematicKind
    {
        /// <summary>패턴 예고: 라이벌 무대로 카메라가 갔다 온다. 돌아온 뒤 패턴 창이 열린다.</summary>
        PatternAnnounce,
        /// <summary>격파: 라이벌 무대 소등을 보여준 뒤 공연이 끝난다.</summary>
        Defeat,
        /// <summary>[디버그] 왕복만.</summary>
        Preview,
    }

    /// <summary>패턴 예고 시작 (연출 시작 시점). 창은 BossPatternStarted 에서 열린다.</summary>
    public readonly struct BossPatternAnnounced
    {
        public BossPatternAnnounced(string patternId, string title, string instruction, bool enhanced, float displaySeconds)
        {
            PatternId = patternId;
            Title = title;
            Instruction = instruction;
            Enhanced = enhanced;
            DisplaySeconds = displaySeconds;
        }

        public string PatternId { get; }
        public string Title { get; }
        public string Instruction { get; }
        public bool Enhanced { get; }

        /// <summary>자막을 띄워 둘 시간(연출이 라이벌 무대에 머무는 동안).</summary>
        public float DisplaySeconds { get; }
    }

    /// <summary>연출 시작: 카드 입력 잠금·틴트·손패 하강이 이때 시작된다.</summary>
    public readonly struct BossCinematicStarted
    {
        public BossCinematicStarted(BossCinematicKind kind) => Kind = kind;
        public BossCinematicKind Kind { get; }
    }

    /// <summary>연출 종료: 카메라 복귀·잠금 해제 완료.</summary>
    public readonly struct BossCinematicEnded
    {
        public BossCinematicEnded(BossCinematicKind kind) => Kind = kind;
        public BossCinematicKind Kind { get; }
    }
}
