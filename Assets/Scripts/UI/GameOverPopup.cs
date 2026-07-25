using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// GameManager가 GameOver 상태가 되면 화면 중앙에 뜨는 팝업. 킷 UIPopup 상속.
    ///
    /// 동작: GameStateChanged 이벤트를 구독하다가 GameOver 상태가 되면 스스로 Open().
    /// 이 팝업은 성공/실패(목표 점수 달성 여부)와 무관하게 항상 뜨므로,
    /// 목표 미달성으로 점수 저장이 막힌 경우(ScoreEntryPopup 이 열리지 않는 경우)에도
    /// 타이틀로 돌아갈 방법과 리더보드 조회 방법이 이 팝업의 버튼으로 남아 있어야 한다.
    /// (리더보드 "조회"는 성공/실패와 무관하게 항상 가능 — 막히는 건 "저장"뿐이다.
    ///  또한 LeaderboardPopup 이 GameOver 시 스스로도 자동으로 열리므로 이 버튼은 보조 수단이다)
    /// 재시작은 HypeDebugInput 의 R 키(GameManager.RestartScene) 가 처리하고,
    /// 버튼을 달고 싶으면 OnClickRestart / OnClickTitle / OnClickLeaderboard 를 버튼 OnClick 에 연결하면 된다.
    ///
    /// resultText: 열릴 때 PerformanceTimer.Failed 에 따라 clearText/failText 중 하나로 채워진다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// - 인스펙터에서 Closable By Escape 는 꺼둘 것 (게임오버를 ESC로 닫으면 안 됨)
    /// </summary>
    public class GameOverPopup : UIPopup
    {
        [SerializeField, Tooltip("성공/실패 결과 문구를 표시할 텍스트 (선택)")]
        Text resultText;

        [SerializeField, Tooltip("목표 점수 달성(성공) 시 표시할 문구")]
        string clearText = "CLEAR";

        [SerializeField, Tooltip("목표 점수 미달성(실패) 시 표시할 문구")]
        string failText = "GAME OVER";

        protected override void Awake()
        {
            base.Awake(); // 킷 규칙: UIPopup.Awake 를 반드시 호출해야 UIManager 에 등록된다

            // 주의: 닫힌 팝업은 SetActive(false) 상태라서 OnEnable/OnDisable 로 구독하면
            // 닫혀 있는 동안 이벤트를 놓친다. 그래서 Awake/OnDestroy 에서 구독한다.
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            base.OnDestroy(); // 킷 규칙: UIManager 등록 해제
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 게임 규칙 또는 디버그 흐름이 GameOver 상태로 전환하면 여기로 통지가 온다.
            if (e.Current == GameState.GameOver) Open();
        }

        protected override void OnOpen()
        {
            if (resultText != null) resultText.text = PerformanceTimer.Failed ? failText : clearText;
        }

        /// <summary>[선택] 재시작 버튼을 만들면 OnClick 에 이 함수를 연결한다.</summary>
        public void OnClickRestart() => GameManager.Instance.RestartScene();

        /// <summary>[선택] 타이틀로 돌아가는 버튼을 만들면 OnClick 에 이 함수를 연결한다. (LeaderboardPopup.OnClickTitle 과 동일한 패턴)</summary>
        public void OnClickTitle() => TitleReturn.Go();

        /// <summary>[선택] 리더보드를 바로 보고 싶을 때 버튼 OnClick 에 연결한다. GameOver 시 자동으로도 열리므로 보조 수단이다.</summary>
        public void OnClickLeaderboard() => UIManager.Instance.Open<LeaderboardPopup>();
    }
}
