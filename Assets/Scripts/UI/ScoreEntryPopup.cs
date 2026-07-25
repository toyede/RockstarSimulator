using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 게임오버 시 이름을 입력받아 점수를 로컬 리더보드에 저장하는 팝업. 킷 UIPopup 상속.
    ///
    /// 동작: GameStateChanged 이벤트를 구독하다가 GameOver 상태가 되면 스스로 Open()
    /// (기존 GameOverPopup 과 동시에 뜬다 — GameOverPopup 은 재시작 전용 디버그 팝업으로 별도 유지).
    /// "저장" 버튼(OnClickSave)을 누르면 LeaderboardStore 에 기록하고 LeaderboardPopup 으로 넘어간다.
    /// 단, 제한시간 초과 + 목표 미달성으로 실패한 판(PerformanceTimer.Failed)에서는 열리지 않는다 —
    /// 실패한 공연은 점수를 리더보드에 기록할 수 없다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// - 인스펙터에서 Closable By Escape 는 꺼둘 것 (게임오버를 ESC로 닫으면 안 됨)
    /// </summary>
    public class ScoreEntryPopup : UIPopup
    {
        [SerializeField] InputField nameInput;
        [SerializeField] Text scoreLabel;

        protected override void Awake()
        {
            base.Awake(); // 킷 규칙: UIPopup.Awake 를 반드시 호출해야 UIManager 에 등록된다
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            base.OnDestroy();
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current != GameState.GameOver) return;
            if (PerformanceTimer.Failed) return; // 목표 미달성으로 실패한 판은 점수 저장 불가
            Open();
        }

        protected override void OnOpen()
        {
            if (scoreLabel != null) scoreLabel.text = $"SCORE {GameManager.Instance.Score}";
            if (nameInput != null) nameInput.text = string.Empty;
        }

        /// <summary>버튼 OnClick 에 연결.</summary>
        public void OnClickSave()
        {
            string name = nameInput == null || string.IsNullOrWhiteSpace(nameInput.text)
                ? "Player"
                : nameInput.text.Trim();

            LeaderboardStore.Add(name, GameManager.Instance.Score);

            Close();
            // LeaderboardPopup 은 GameOver 시 이미 자동으로 열려 있을 수 있어 Open() 만으로는
            // 목록이 갱신되지 않는다 (이미 열린 상태면 OnOpen 이 다시 불리지 않음) — Refresh()로 확실히 갱신.
            var leaderboard = UIManager.Instance.Open<LeaderboardPopup>();
            if (leaderboard != null) leaderboard.Refresh();
        }
    }
}
