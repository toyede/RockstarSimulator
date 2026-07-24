using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 공연 점수 표시 UI. 킷의 ScoreChanged 이벤트만 구독하므로 GameManager 를 직접 참조하지 않는다.
    /// 점수 누적은 PerformanceScoreSystem 이 담당하고, 여기서는 표시만 한다.
    ///
    /// 씬 배치는 Tools/Hype/Setup Hype Scene 메뉴가 자동으로 해준다.
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        [SerializeField] Text scoreText;
        [SerializeField, Tooltip("점수 앞에 붙는 라벨")] string prefix = "SCORE ";

        void OnEnable()
        {
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);

            // 씬 로드 직후 이벤트가 오기 전에도 현재 점수와 동기화
            if (GameManager.HasInstance) Refresh(GameManager.Instance.Score);
            else Refresh(0);
        }

        void OnDisable() => EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);

        void OnScoreChanged(ScoreChanged e) => Refresh(e.Score);

        void Refresh(int score)
        {
            if (scoreText != null) scoreText.text = prefix + score;
        }
    }
}
