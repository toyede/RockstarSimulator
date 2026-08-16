using System.Collections;
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
    /// "저장" 버튼(OnClickSave)을 누르면 LeaderboardStore 에 기록하고 LeaderboardPopup 을 갱신한다.
    /// 이 팝업 자체는 닫지 않고 화면에 그대로 남기며, 중복 등록을 막기 위해 입력창/버튼만 비활성화한다.
    /// 클리어/게임오버 여부와 무관하게 항상 동일하게 열려 점수를 저장할 수 있다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// - 인스펙터에서 Closable By Escape 는 꺼둘 것 (게임오버를 ESC로 닫으면 안 됨)
    /// </summary>
    public class ScoreEntryPopup : UIPopup
    {
        [SerializeField] InputField nameInput;
        [SerializeField] Text scoreLabel;
        [SerializeField] Button saveButton;

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
            if (TourRunManager.HasInstance && TourRunManager.Instance.CurrentRun != null) return;
            Open();
        }

        protected override void OnOpen()
        {
            if (scoreLabel != null) scoreLabel.text = $"SCORE {GameManager.Instance.Score}";
            if (nameInput != null)
            {
                nameInput.text = string.Empty;
                nameInput.interactable = true;
            }
            if (saveButton != null) saveButton.interactable = true;
        }

        /// <summary>버튼 OnClick 에 연결.</summary>
        public void OnClickSave()
        {
            string name = RemoteLeaderboardClient.NormalizePlayerName(
                nameInput != null ? nameInput.text : string.Empty);
            int score = Mathf.Max(0, GameManager.Instance.Score);

            // 네트워크 응답을 기다리지 않고 로컬 기록부터 보존한다.
            LeaderboardStore.Add(name, score);

            // 패널은 닫지 않고 그대로 둔다 (등록해도 리더보드/입력 화면이 사라지면 안 됨).
            // 대신 중복 등록을 막기 위해 입력창과 등록 버튼을 비활성화한다.
            if (nameInput != null) nameInput.interactable = false;
            if (saveButton != null) saveButton.interactable = false;

            // LeaderboardPopup 은 GameOver 시 이미 자동으로 열려 있을 수 있어 Open() 만으로는
            // 목록이 갱신되지 않는다 (이미 열린 상태면 OnOpen 이 다시 불리지 않음) — Refresh()로 확실히 갱신.
            var leaderboard = UIManager.Instance.Open<LeaderboardPopup>();
            if (leaderboard != null) leaderboard.Refresh();

            StartCoroutine(SubmitRemote(name, score, leaderboard));
        }

        IEnumerator SubmitRemote(
            string playerName,
            int score,
            LeaderboardPopup leaderboard)
        {
            bool uploaded = false;
            yield return RemoteLeaderboardClient.SubmitScore(
                playerName,
                score,
                success => uploaded = success);

            // 실패 시 이미 표시한 로컬 기록을 그대로 유지한다.
            if (uploaded && leaderboard != null)
                leaderboard.Refresh();
        }
    }
}
