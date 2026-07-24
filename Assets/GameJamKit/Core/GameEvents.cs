using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 킷이 기본으로 발행하는 이벤트들. 프로젝트 고유 이벤트는 이 파일에 계속 추가해서 쓰면 된다.
    /// struct 로 만드는 이유: GC 할당 없음.
    /// </summary>
    public struct GameStateChanged
    {
        public GameState Previous;
        public GameState Current;
    }

    public struct ScoreChanged
    {
        public int Score;
        public int Delta;
    }

    /// <summary>Health 컴포넌트가 사망 시 자동 발행.</summary>
    public struct EntityDied
    {
        public GameObject Entity;
        public GameObject Killer;
        public Vector3 Position;
    }

    /// <summary>Health 컴포넌트가 피격 시 자동 발행.</summary>
    public struct EntityDamaged
    {
        public GameObject Entity;
        public float Amount;
        public float RemainingNormalized;
    }

    public struct WaveStarted
    {
        public int Index;
        public string Name;
    }

    public struct WaveCleared
    {
        public int Index;
        public bool WasLast;
    }

    // ==================================================================
    // 여기서부터 CONTEXT STAGE(락스타 시뮬레이터) 프로젝트 고유 이벤트.
    // 파일 상단 안내대로 프로젝트 이벤트는 이 파일에 모아서 관리한다.
    // ※ 킷을 수정하면 반드시 GameJamKit/README.md 하단 "변경 이력"에 기록할 것!
    // ==================================================================

    /// <summary>
    /// 카드 사용 결과 판정 등급. 카드 담당이 관객 상태와 카드를 비교해 결정한 뒤
    /// HypeSystem.Instance.ApplyJudgement() 로 넘기면 호응도가 증감된다.
    /// (판정별 증감 수치는 코드가 아니라 HypeConfig 에셋에서 튜닝)
    /// </summary>
    public enum HypeJudgement
    {
        Perfect,    // 관객의 현재 맥락을 정확히 읽음 (+30)
        Good,       // 완벽하지 않지만 분위기 유지 (+15)
        Miss,       // 현재 관객과 맞지 않는 행동 (-15)
        RiskMiss    // 위험 카드(모쉬핏 등)의 완전 오판 (-25)
    }

    /// <summary>호응도 값이 바뀔 때마다 HypeSystem 이 발행. UI 게이지가 구독한다.</summary>
    public struct HypeChanged
    {
        public float Value;       // 변경 후 호응도 (0~100)
        public float Delta;       // 변화량 (감소면 음수)
        public float Normalized;  // 0~1 비율 (게이지 fillAmount 용)
    }

    /// <summary>카드 판정이 적용된 순간 발행. "Perfect!" 같은 연출 텍스트용.</summary>
    public struct HypeJudgementApplied
    {
        public HypeJudgement Judgement;
        public float Delta;       // 이 판정으로 적용된 증감량
    }

    /// <summary>
    /// 호응도 100 도달(앙코르) 시 발행.
    /// 카드 담당: 이 이벤트를 구독해서 카드 1장 추가 드로우를 구현하면 된다.
    /// 연출 담당: 게이지 점멸·관객 함성도 여기에 붙인다.
    /// 발행 직후 호응도는 70으로 내려가고 잠시 감소가 멈춘다. (HypeSystem 이 처리)
    /// </summary>
    public struct EncoreTriggered { }

    /// <summary>
    /// 호응도 0 도달(공연 실패) 시 발행. 직후 GameManager.GameOver() 가 호출된다.
    /// 연출 담당: 조명 소등·야유 등 실패 연출을 여기에 붙인다.
    /// </summary>
    public struct HypeDepleted { }

    /// <summary>
    /// 관객 앰비언스 단계(low/middle/high...)가 바뀔 때 CrowdAmbienceSystem 이 발행.
    /// 연출 담당: 조명 색·관객 애니메이션 속도를 사운드와 같은 타이밍에 바꾸고 싶을 때 구독한다.
    /// </summary>
    public struct CrowdAmbienceTierChanged
    {
        public int PreviousIndex;  // 이전 티어 (-1 = 없음)
        public int Index;          // 새 티어 인덱스 (0 = 가장 낮음)
        public string TierName;    // 콘픽에 적힌 티어 이름 ("Low" / "Middle" / "High" ...)
    }


    /// <summary>
    /// 관객 비주얼 상태(low/middle/high...)가 바뀔 때 CrowdMoodDirector 가 발행.
    /// 관객 스프라이트 자체는 ICrowdMoodReactor 로 직접 갱신되므로,
    /// 이 이벤트는 조명·카메라·UI 처럼 "곁다리로 같이 반응하는" 쪽이 구독한다.
    /// </summary>
    public struct CrowdMoodChanged
    {
        public int PreviousIndex;  // 이전 상태 (-1 = 없음)
        public int Index;          // 새 상태 인덱스 (0 = 가장 낮음)
        public string MoodName;    // 콘픽에 적힌 상태 이름
    }
    /// <summary>손패 구성이 바뀐 뒤 발행. 카드 UI가 구독한다.</summary>
    public struct HandChanged
    {
        public int Count;
        public int BaseHandSize;
        public int BonusCardCount;
    }

    /// <summary>덱에서 카드가 손패로 들어올 때 발행.</summary>
    public struct CardDrawn
    {
        public string CardId;
        public string DisplayName;
        public int HandIndex;
        public bool IsEncoreBonus;
    }

    /// <summary>숫자키로 카드가 선택되어 열기(호응도) 판정이 적용될 때 발행.</summary>
    public struct CardSelected
    {
        public string CardId;
        public string DisplayName;
        public int HandIndex;
        public HypeJudgement Judgement;
        public float Delta;
        public int BaseScore;    // 카드 기본 점수 (열기 배율 적용 전). 점수 담당이 구독해 누적한다
        public float Multiplier; // 카드를 낼 때의 열기 배율. 획득 점수 = BaseScore × Multiplier
    }

    /// <summary>
    /// Final authoritative result of one card play.
    /// Score systems consume this event only; CardSelected remains for input/audio/visual feedback.
    /// </summary>
    public struct CardResolved
    {
        public string CardId;
        public string DisplayName;
        public int HandIndex;
        public ContextStage.CardRole Role;
        public ContextStage.CrowdPreference TargetPreference;
        public ContextStage.CrowdReactionGrade CrowdReaction;
        public HypeJudgement Judgement;
        public int BaseScore;
        public float HypeMultiplier;
        public float CrowdMultiplier;
        public int GainedScore;
        public float HypeDelta;
        public bool IsSpecialHit;
    }

    // ------------------------------------------------------------------
    // 특별 관객 (SpecialAudienceManager 가 발행)
    // 매니저에는 C# event 도 함께 있으니, 직접 참조가 있으면 그쪽을 써도 된다.
    // ------------------------------------------------------------------

    /// <summary>특별 관객이 등장했을 때 발행.</summary>
    public struct SpecialAudienceSpawned
    {
        public ContextStage.HeatStage RequestType;
        public float Duration;   // 전체 제한시간(초)
    }

    /// <summary>특별 관객 요청이 끝났을 때 발행. 이유는 Reason 으로 구분한다.</summary>
    public struct SpecialAudienceEnded
    {
        public ContextStage.HeatStage RequestType;
        public ContextStage.SpecialAudienceEndReason Reason;
    }

    /// <summary>
    /// Special Hit 성공 시 발행. 한 요청당 정확히 한 번만 발행된다.
    /// 점수·열기 담당: 이 이벤트를 구독해 Reward 를 적용하면 된다.
    /// (특별 관객 시스템은 점수·열기를 직접 건드리지 않는다)
    /// </summary>
    public struct SpecialHitLanded
    {
        public ContextStage.HeatStage RequestType;
        public ContextStage.SpecialHitReward Reward;

        /// <summary>
        /// true 면 카드 판정 경로가 이미 점수·열기를 적용했으므로 <b>다시 적용하면 안 된다.</b>
        /// (연출·사운드처럼 "성공했다"는 사실만 필요한 구독자는 이 값과 무관하게 반응하면 된다)
        /// </summary>
        public bool AlreadyApplied;
    }
}

