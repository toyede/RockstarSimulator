namespace ContextStage
{
    /// <summary>팬 분포가 바뀔 때 (시작·이동·이탈). 팬 쟁탈 바가 구독한다.</summary>
    public readonly struct BossFanBalanceChanged
    {
        public BossFanBalanceChanged(int ours, int rival, int total, bool revenge)
        {
            Ours = ours;
            Rival = rival;
            Total = total;
            Revenge = revenge;
        }

        public int Ours { get; }
        public int Rival { get; }
        public int Total { get; }
        public bool Revenge { get; }
        public float RivalRatio => Total <= 0 ? 0f : UnityEngine.Mathf.Clamp01((float)Rival / Total);
    }

    /// <summary>팬이 무대 사이를 건너갔다 (연출용). toRival = true 면 우리 → 라이벌.</summary>
    public readonly struct BossFanMoved
    {
        public BossFanMoved(bool toRival, int count, int rivalNow, int oursNow)
        {
            ToRival = toRival;
            Count = count;
            RivalNow = rivalNow;
            OursNow = oursNow;
        }

        public bool ToRival { get; }
        public int Count { get; }
        public int RivalNow { get; }
        public int OursNow { get; }
    }

    /// <summary>드레인 틱: 라이벌 팬 수만큼 점수가 깎였다.</summary>
    public readonly struct BossDrainApplied
    {
        public BossDrainApplied(int amount, int rivalFans, float nextIn)
        {
            Amount = amount;
            RivalFans = rivalFans;
            NextIn = nextIn;
        }

        /// <summary>깎인 점수 (양수).</summary>
        public int Amount { get; }
        public int RivalFans { get; }
        public float NextIn { get; }
    }

    /// <summary>드레인 예고 (매 프레임): 다음 감소까지 남은 시간과 예정 감소량.</summary>
    public readonly struct BossDrainCountdown
    {
        public BossDrainCountdown(float remaining, int amount, bool active)
        {
            Remaining = remaining;
            Amount = amount;
            Active = active;
        }

        public float Remaining { get; }
        public int Amount { get; }
        public bool Active { get; }
    }

    /// <summary>REVENGE TIME! 진입 / 해제.</summary>
    public readonly struct BossRevengeChanged
    {
        public BossRevengeChanged(bool active) => Active = active;
        public bool Active { get; }
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
        public float DisplaySeconds { get; }
    }

    /// <summary>패턴 창 시작.</summary>
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

    /// <summary>패턴 결과. 성공이면 팬 영입·보너스, 실패면 팬 이탈이 이미 적용된 뒤 발행된다.</summary>
    public readonly struct BossPatternResolved
    {
        public BossPatternResolved(string patternId, string title, bool success, int successes, int bonusScore, int fansMoved)
        {
            PatternId = patternId;
            Title = title;
            Success = success;
            Successes = successes;
            BonusScore = bonusScore;
            FansMoved = fansMoved;
        }

        public string PatternId { get; }
        public string Title { get; }
        public bool Success { get; }

        /// <summary>지금까지 성공한 패턴 수 (누적).</summary>
        public int Successes { get; }

        /// <summary>성공 보너스 점수 (실패면 0).</summary>
        public int BonusScore { get; }

        /// <summary>성공이면 합류한 라이벌 팬 수, 실패면 떠난 우리 팬 수.</summary>
        public int FansMoved { get; }
    }

    /// <summary>보스 연출 종류.</summary>
    public enum BossCinematicKind
    {
        /// <summary>패턴 예고: 라이벌 무대로 카메라가 갔다 온다. 돌아온 뒤 패턴 창이 열린다.</summary>
        PatternAnnounce,
        /// <summary>엿보기: 버튼으로 라이벌 무대를 보고 버튼으로 돌아온다. 타이머는 멈추지 않는다.</summary>
        Peek,
        /// <summary>[디버그] 왕복만.</summary>
        Preview,
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
