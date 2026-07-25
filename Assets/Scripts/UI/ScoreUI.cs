using System.Collections;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 공연 점수 표시 UI. 킷의 ScoreChanged 이벤트만 구독하므로 GameManager 를 직접 참조하지 않는다.
    /// 점수 누적은 PerformanceScoreSystem 이 담당하고, 여기서는 표시만 한다.
    /// 목표 점수는 PerformanceTimer(PerformanceTimerSystem 정적 파사드)에서 읽는다 — 이 컴포넌트는 값을 소유하지 않는다.
    /// 목표가 0보다 크면 "현재 / 목표" 형식으로, 아니면 현재 점수만 표시한다.
    /// (클리어 판정·GameOver 트리거는 PerformanceTimerSystem 담당 — 여기서는 표시만 한다.)
    ///
    /// 씬 배치는 Tools/Hype/Setup Hype Scene 메뉴가 자동으로 해준다.
    /// </summary>
    public class ScoreUI : MonoBehaviour
    {
        [SerializeField] Text scoreText;
        [SerializeField, Tooltip("점수 앞에 붙는 라벨")] string prefix = "SCORE ";
        [SerializeField, Min(0f)] float cardPresentationDelay = 0.085f;

        float _lastCardPresentationAt = float.NegativeInfinity;
        Coroutine _scoreRoutine;

        void OnEnable()
        {
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<CardPresentationStarted>(OnCardPresentationStarted);

            // 씬 로드 직후 이벤트가 오기 전에도 현재 점수와 동기화
            if (GameManager.HasInstance) Refresh(GameManager.Instance.Score);
            else Refresh(0);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Unsubscribe<CardPresentationStarted>(OnCardPresentationStarted);
            _scoreRoutine = null;
        }

        void OnCardPresentationStarted(CardPresentationStarted _) =>
            _lastCardPresentationAt = Time.unscaledTime;

        void OnScoreChanged(ScoreChanged e)
        {
            bool followsCard =
                Time.unscaledTime - _lastCardPresentationAt < 0.1f;
            if (!followsCard || cardPresentationDelay <= 0f)
            {
                Refresh(e.Score);
                return;
            }

            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = StartCoroutine(RefreshAfterDelay(e.Score));
        }

        IEnumerator RefreshAfterDelay(int score)
        {
            yield return new WaitForSecondsRealtime(cardPresentationDelay);
            _scoreRoutine = null;
            Refresh(score);
        }

        void Refresh(int score)
        {
            if (scoreText == null) return;
            int target = PerformanceTimer.TargetScore;
            scoreText.text = target > 0
                ? $"{prefix}{score} / {target}"
                : prefix + score;
        }
    }
}
