using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// GameManager가 GameOver 상태가 되면 화면 중앙에 뜨는 팝업. 킷 UIPopup 상속.
    ///
    /// 동작: GameStateChanged 이벤트를 구독하다가 GameOver 상태가 되면 스스로 Open().
    /// 이 팝업은 성공/실패(목표 점수 달성 여부)와 무관하게 항상 뜨고, ScoreEntryPopup/LeaderboardPopup 도
    /// 성공/실패와 무관하게 항상 같은 방식으로 열리므로 타이틀로 돌아갈 방법과 리더보드 조회 방법이
    /// 이 팝업의 버튼으로도 보조 수단으로 남아 있다.
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

        [SerializeField, Tooltip("내가 달성한 점수를 표시할 텍스트")]
        Text myPointText;

        [SerializeField, Tooltip("클리어 조건(목표) 점수를 표시할 텍스트")]
        Text pointText;

        [SerializeField, Tooltip("달성 비율에 따른 랭크 이미지")]
        Image rankImage;

        [SerializeField, Tooltip("클리어/게임오버 결과에 따라 스프라이트를 바꿔 보여줄 이미지 (GameOverImage)")]
        Image resultImage;

        [SerializeField, Tooltip("목표 점수 달성(성공) 시 resultImage에 표시할 스프라이트")]
        Sprite clearSprite;

        [SerializeField, Tooltip("목표 점수 미달성(실패) 시 resultImage에 표시할 스프라이트")]
        Sprite failSprite;

        [SerializeField, Tooltip("랭크 구간 (minRatio 오름차순). ScoreRankUI와 동일한 판정 로직(GetRankIndex)을 쓴다")]
        ScoreRankUI.RankTier[] rankTiers =
        {
            new ScoreRankUI.RankTier { label = "F", minRatio = 0f },
            new ScoreRankUI.RankTier { label = "D", minRatio = 1.00f },
            new ScoreRankUI.RankTier { label = "C", minRatio = 1.60f },
            new ScoreRankUI.RankTier { label = "B", minRatio = 2.40f },
            new ScoreRankUI.RankTier { label = "A", minRatio = 3.60f },
            new ScoreRankUI.RankTier { label = "S", minRatio = 5.00f },
        };

        protected override void Awake()
        {
            base.Awake(); // 킷 규칙: UIPopup.Awake 를 반드시 호출해야 UIManager 에 등록된다
            ScoreRankUI.ApplyCurrentBalance(rankTiers);

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
            ScoreRankUI.ApplyCurrentBalance(rankTiers);
            if (resultText != null) resultText.text = PerformanceTimer.Failed ? failText : clearText;

            // 게임오버 시점의 최종 점수와 클리어 목표 점수를 반영한다
            int score = GameManager.HasInstance ? GameManager.Instance.Score : 0;
            int target = PerformanceTimer.TargetScore;

            if (myPointText != null) myPointText.text = score.ToString();
            if (pointText != null) pointText.text = $"/ {target}"; // 기존 prefab 텍스트 포맷("/ 5000") 유지

            if (rankImage != null && rankTiers.Length > 0)
            {
                float ratio = target > 0 ? (float)score / target : 0f;
                int index = ScoreRankUI.GetRankIndex(ratio, rankTiers);
                Sprite icon = rankTiers[index].icon;
                rankImage.sprite = icon;
                rankImage.enabled = icon != null;
            }

            if (resultImage != null)
            {
                Sprite sprite = PerformanceTimer.Failed ? failSprite : clearSprite;
                resultImage.sprite = sprite;
                resultImage.enabled = sprite != null;
            }
        }

        /// <summary>[선택] 재시작 버튼을 만들면 OnClick 에 이 함수를 연결한다.</summary>
        public void OnClickRestart() => GameManager.Instance.RestartScene();

        /// <summary>[선택] 타이틀로 돌아가는 버튼을 만들면 OnClick 에 이 함수를 연결한다. (LeaderboardPopup.OnClickTitle 과 동일한 패턴)</summary>
        public void OnClickTitle() => TitleReturn.Go();

        /// <summary>[선택] 리더보드를 바로 보고 싶을 때 버튼 OnClick 에 연결한다. GameOver 시 자동으로도 열리므로 보조 수단이다.</summary>
        public void OnClickLeaderboard() => UIManager.Instance.Open<LeaderboardPopup>();

#if UNITY_EDITOR
        void OnValidate() => ScoreRankUI.ApplyCurrentBalance(rankTiers);
#endif
    }
}
