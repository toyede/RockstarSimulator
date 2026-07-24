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
}
