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
        [SerializeField] private bool showTargetScore = true;
        [SerializeField] private HudNumberFeedback numberFeedback;

        float _lastCardPresentationAt = float.NegativeInfinity;
        Coroutine _scoreRoutine;
        Text _drainText;
        Color _normalColor;
        float _drainAge = -1f;
        static ScoreUI _active;
        public static bool HasDrainFeedback => _active != null && _active.isActiveAndEnabled && _active.scoreText != null;

        void OnEnable()
        {
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<CardPresentationStarted>(OnCardPresentationStarted);
            EventBus.Subscribe<BossDrainApplied>(OnDrain);
            EventBus.Subscribe<GameStateChanged>(OnState);
            _active = this;
            if (scoreText != null) _normalColor = scoreText.color;

            // 씬 로드 직후 이벤트가 오기 전에도 현재 점수와 동기화
            if (GameManager.HasInstance) Refresh(GameManager.Instance.Score);
            else Refresh(0);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Unsubscribe<CardPresentationStarted>(OnCardPresentationStarted);
            EventBus.Unsubscribe<BossDrainApplied>(OnDrain);
            EventBus.Unsubscribe<GameStateChanged>(OnState);
            if (_active == this) _active = null;
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = null;
            ClearDrain();
        }

        void OnCardPresentationStarted(CardPresentationStarted _) =>
            _lastCardPresentationAt = Time.unscaledTime;

        void OnScoreChanged(ScoreChanged e)
        {
            if (numberFeedback != null)
            {
                Refresh(e.Score);
                if (GameManager.HasInstance && GameManager.Instance.IsPlaying)
                    numberFeedback.Play(e.Delta);
                return;
            }
            // 카드 표시를 기다리는 사이 점수가 감소해도 과거 점수로 되돌아가지 않는다.
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = null;
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

        void OnDrain(BossDrainApplied e)
        {
            // 공통 증감 표시가 있으면 ScoreChanged에서 이미 표시했다.
            if (numberFeedback != null) return;
            if (e.Amount <= 0 || scoreText == null) return;
            if (_drainText == null)
            {
                var go = new GameObject("BossScoreLoss", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(scoreText.transform, false);
                _drainText = go.GetComponent<Text>();
                _drainText.font = scoreText.font; _drainText.fontSize = 30;
                _drainText.alignment = TextAnchor.UpperRight; _drainText.raycastTarget = false;
                _drainText.horizontalOverflow = HorizontalWrapMode.Overflow;
                _drainText.verticalOverflow = VerticalWrapMode.Overflow;
                var r = _drainText.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(1f, 0f); r.pivot = new Vector2(1f, 1f);
                r.sizeDelta = new Vector2(480f, 44f);
            }
            _drainText.text = $"−{e.Amount:N0}점";
            _drainText.gameObject.SetActive(true); _drainAge = 0f;
        }

        void Update()
        {
            if (_drainAge < 0f || (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused)) return;
            _drainAge += Time.deltaTime;
            if (_drainAge >= .8f) { ClearDrain(); return; }
            float t = _drainAge / .8f;
            var red = new Color32(0xFF, 0x77, 0x65, 0xFF);
            scoreText.color = Color.Lerp(red, _normalColor, Mathf.Clamp01(t * 2f));
            _drainText.color = new Color(red.r / 255f, red.g / 255f, red.b / 255f, 1f - t * t);
            _drainText.rectTransform.anchoredPosition = new Vector2(0f, -8f - 24f * t);
        }

        void OnState(GameStateChanged e)
        {
            if (e.Current != GameState.Ready && e.Current != GameState.GameOver) return;
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = null;
            _lastCardPresentationAt = float.NegativeInfinity;
            ClearDrain();
            if (numberFeedback != null) numberFeedback.Clear();
            Refresh(GameManager.HasInstance ? GameManager.Instance.Score : 0);
        }

        void ClearDrain()
        {
            _drainAge = -1f;
            if (scoreText != null) scoreText.color = _normalColor;
            if (_drainText != null) _drainText.gameObject.SetActive(false);
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
            scoreText.text = showTargetScore && target > 0
                ? $"{prefix}{score} / {target}"
                : prefix + score;
        }
    }
}
