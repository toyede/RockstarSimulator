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
        [Header("완벽주의 점수 카운트업")]
        [SerializeField, Min(0.1f)] private float perfectClearCountUpDuration = 1f;
        [SerializeField, Tooltip("{0} = 배율")] private string perfectClearMessageFormat = "×{0}";
        [SerializeField] private Color perfectClearMessageColor = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
        [Header("증감 팝업 크기 (점수 크기에 비례)")]
        [SerializeField, Tooltip("끄면 모든 증감 팝업을 템플릿 크기로 표시한다")]
        private bool scalePopupByAmount = true;
        [SerializeField, Min(1), Tooltip("증감 절대값이 이 값 이하면 템플릿 크기(최소 크기)로 표시한다")]
        private int baseSizeAmount = 200;
        [SerializeField, Min(2), Tooltip("증감 절대값이 이 값 이상이면 최대 배율로 표시한다")]
        private int maxSizeAmount = 2000;
        [SerializeField, Range(1f, 4f), Tooltip("가장 큰 증감에 적용할 글자 배율")]
        private float maxPopupScale = 2f;
        [SerializeField, Min(0), Tooltip("0보다 크면 실제 증감과 상관없이 이 값에 해당하는 크기로 고정한다")]
        private int fixedPopupAmount = 400;
        [Header("점수 숫자 반동")]
        [SerializeField, Tooltip("숫자 반동을 오른쪽 끝 기준으로 커지게 하고, 커진 숫자에 가리지 않게 SCORE 라벨을 비켜 둔다")]
        private bool pulseFromRightEdge = true;
        [SerializeField, Min(0f), Tooltip("반동으로 가장 커진 숫자와 SCORE 라벨 사이에 남길 최소 간격(px)")]
        private float labelPulseGap = 4f;
        [Header("자릿수에 따른 SCORE 라벨 이동")]
        [SerializeField, Tooltip("점수 자릿수가 늘면 왼쪽으로 옮길 SCORE 라벨")] private RectTransform scoreLabel;
        [SerializeField, Min(1), Tooltip("이 자릿수까지는 라벨 위치를 고정한다")] private int fixedDigitCount = 4;
        [SerializeField, Min(0f), Tooltip("고정 자릿수를 넘는 한 자리마다 라벨을 왼쪽으로 옮길 거리(px). DungGeunMo 52pt 숫자 한 칸 = 26")]
        private float labelShiftPerDigit = 26f;
        [SerializeField, Min(0), Tooltip("라벨을 최대 몇 칸까지 옮길지. 점수 텍스트의 Best Fit 이 5자리부터 숫자를 칸 폭에 맞춰 줄이므로 1칸이면 충분하다")]
        private int maxShiftedDigits = 1;

        Vector2 _labelBasePosition;
        bool _hasLabelBase;
        float _appliedLabelShift = -1f;
        float _valueBaseRight, _valueBaseScaleX = 1f;
        float _lastCardPresentationAt = float.NegativeInfinity;
        Coroutine _scoreRoutine;
        Text _drainText;
        Color _normalColor;
        float _drainAge = -1f;
        int _countFrom, _countTo;
        float _countAge = -1f;
        static ScoreUI _active;
        public static bool HasDrainFeedback => _active != null && _active.isActiveAndEnabled && _active.scoreText != null;

        void OnEnable()
        {
            if (GetComponent<ScoreLossPresentation>() == null)
                gameObject.AddComponent<ScoreLossPresentation>();
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<CardPresentationStarted>(OnCardPresentationStarted);
            EventBus.Subscribe<BossDrainApplied>(OnDrain);
            EventBus.Subscribe<GameStateChanged>(OnState);
            EventBus.Subscribe<PerfectClearScoreApplied>(OnPerfectClearScoreApplied);
            _active = this;
            if (pulseFromRightEdge && numberFeedback != null)
                numberFeedback.AnchorPulseToRight();
            if (scoreText != null) _normalColor = scoreText.color;
            if (scoreLabel != null && !_hasLabelBase)
            {
                _labelBasePosition = scoreLabel.anchoredPosition;
                if (scoreText != null)
                {
                    // 반동 중 값이 아닌 배치 기준값을 기억해, 라벨 계산이 반동에 흔들리지 않게 한다.
                    var value = scoreText.rectTransform;
                    _valueBaseScaleX = value.localScale.x;
                    _valueBaseRight = value.anchoredPosition.x +
                        value.rect.width * _valueBaseScaleX * (1f - value.pivot.x);
                }
                _hasLabelBase = true;
            }

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
            EventBus.Unsubscribe<PerfectClearScoreApplied>(OnPerfectClearScoreApplied);
            if (_active == this) _active = null;
            _countAge = -1f;
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = null;
            ClearDrain();
        }

        void OnCardPresentationStarted(CardPresentationStarted _) =>
            _lastCardPresentationAt = Time.unscaledTime;

        void OnPerfectClearScoreApplied(PerfectClearScoreApplied e)
        {
            // 바로 이어지는 ScoreChanged 는 건너뛰고 Update 에서 숫자를 굴려 올린다.
            _countFrom = e.PreviousScore;
            _countTo = e.FinalScore;
            _countAge = 0f;
            if (numberFeedback != null)
                numberFeedback.PlayMessage(string.Format(perfectClearMessageFormat, e.Multiplier.ToString("0.#")),
                    perfectClearMessageColor);
        }

        void OnScoreChanged(ScoreChanged e)
        {
            if (_countAge >= 0f) return;
            if (numberFeedback != null)
            {
                Refresh(e.Score);
                if (GameManager.HasInstance && GameManager.Instance.IsPlaying)
                    numberFeedback.Play(e.Delta, numberFeedback.GainColor, PopupScaleFor(e.Delta));
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

        /// <summary>
        /// 증감 절대값을 로그 스케일로 보간해 팝업 글자 배율을 구한다.
        /// baseSizeAmount 이하는 1(현재 크기), maxSizeAmount 이상은 maxPopupScale.
        /// </summary>
        float PopupScaleFor(int delta)
        {
            if (!scalePopupByAmount || maxPopupScale <= 1f) return 1f;
            int amount = fixedPopupAmount > 0 ? fixedPopupAmount : Mathf.Abs(delta);
            if (amount <= baseSizeAmount) return 1f;
            int upper = Mathf.Max(baseSizeAmount + 1, maxSizeAmount);
            float t = Mathf.InverseLerp(Mathf.Log(baseSizeAmount), Mathf.Log(upper), Mathf.Log(amount));
            return Mathf.Lerp(1f, maxPopupScale, t);
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
            if (_countAge >= 0f)
            {
                _countAge += Time.unscaledDeltaTime;
                float countT = Mathf.Clamp01(_countAge / perfectClearCountUpDuration);
                Refresh(Mathf.RoundToInt(Mathf.Lerp(_countFrom, _countTo, 1f - (1f - countT) * (1f - countT))));
                if (countT >= 1f) _countAge = -1f;
            }
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
            _countAge = -1f;
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
            UpdateLabelOffset(score);
        }

        /// <summary>
        /// 점수는 오른쪽 정렬이라 자릿수가 늘면 왼쪽으로 자란다.
        /// 고정 자릿수를 넘은 만큼(최대 maxShiftedDigits 칸) SCORE 라벨을 왼쪽으로 밀어 숫자와 겹치지 않게 한다.
        /// </summary>
        void UpdateLabelOffset(int score)
        {
            if (scoreLabel == null || !_hasLabelBase) return;
            int extra = Mathf.Clamp(CharacterCount(score) - fixedDigitCount, 0, maxShiftedDigits);
            float shift = Mathf.Max(extra * labelShiftPerDigit, PulseClearanceShift());
            if (Mathf.Approximately(shift, _appliedLabelShift)) return;
            _appliedLabelShift = shift;
            scoreLabel.anchoredPosition = _labelBasePosition + Vector2.left * shift;
        }

        /// <summary>
        /// 오른쪽 끝 기준 반동으로 가장 커졌을 때(감소 흔들림 포함) 숫자의 왼쪽 끝이
        /// SCORE 라벨 오른쪽 끝 + labelPulseGap 보다 왼쪽으로 오면, 그만큼 라벨을 왼쪽으로 옮길 거리.
        /// 숫자와 라벨이 같은 부모·같은 가로 앵커일 때만 계산한다.
        /// </summary>
        float PulseClearanceShift()
        {
            if (!pulseFromRightEdge || numberFeedback == null || scoreText == null) return 0f;
            var value = scoreText.rectTransform;
            if (scoreLabel.parent != value.parent ||
                !Mathf.Approximately(scoreLabel.anchorMin.x, value.anchorMin.x) ||
                !Mathf.Approximately(scoreLabel.anchorMax.x, value.anchorMax.x)) return 0f;

            // 숫자는 오른쪽 정렬이고 반동 중에도 칸 오른쪽 끝이 고정된다. Best Fit 은 칸 폭을 넘지 않는다.
            float glyphWidth = Mathf.Min(scoreText.preferredWidth, value.rect.width) * _valueBaseScaleX;
            float pulsedLeft = _valueBaseRight - glyphWidth * numberFeedback.PeakScale - numberFeedback.LossShakeWidth;
            float labelRight = _labelBasePosition.x +
                scoreLabel.rect.width * scoreLabel.localScale.x * (1f - scoreLabel.pivot.x);
            return Mathf.Max(0f, labelRight + labelPulseGap - pulsedLeft);
        }

        // 표시되는 숫자 글자 수 (음수면 '-' 포함). 문자열을 만들지 않고 센다.
        static int CharacterCount(int value)
        {
            long v = value < 0 ? -(long)value : value;
            int count = value < 0 ? 2 : 1;
            while (v >= 10) { v /= 10; count++; }
            return count;
        }
    }
}
