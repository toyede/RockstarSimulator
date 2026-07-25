using System.Collections;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 우측 상단의 랭크 아이콘 + 목표 달성 게이지.
    ///
    /// 현재 점수를 목표 점수(PerformanceTimer.TargetScore)로 나눈 비율로 랭크를 정한다.
    /// 목표를 못 넘기면 F, 넘기면 D, 그 위로 C/B/A/S 순.
    ///
    /// 점수 숫자("4200 / 5000")는 기존 ScoreUI 가 그대로 담당한다 — 여기서는 랭크와 막대만 그린다.
    /// ScoreChanged 이벤트만 구독하므로 점수 시스템을 직접 참조하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreRankUI : MonoBehaviour
    {
        /// <summary>랭크 한 칸. minRatio(목표 대비 비율) 이상이면 이 랭크.</summary>
        [System.Serializable]
        public struct RankTier
        {
            [Tooltip("표시용 이름 (F/D/C/B/A/S)")]
            public string label;

            [Tooltip("목표 점수 대비 비율. 1.0 = 목표 정확히 달성, 1.1 = 110%")]
            public float minRatio;

            [Tooltip("이 랭크의 아이콘")]
            public Sprite icon;
        }

        [Header("표시 대상")]
        [SerializeField, Tooltip("랭크 아이콘을 그릴 Image")]
        Image rankImage;

        [SerializeField, Tooltip("게이지 채움. Image Type = Filled, Fill Method = Vertical 이어야 한다")]
        Image gaugeFill;

        [Header("랭크 구간 (minRatio 오름차순)")]
        [SerializeField]
        RankTier[] rankTiers =
        {
            new RankTier { label = "F", minRatio = 0f },     // 목표 미달
            new RankTier { label = "D", minRatio = 1.00f },  // 목표 달성
            new RankTier { label = "C", minRatio = 1.10f },
            new RankTier { label = "B", minRatio = 1.25f },
            new RankTier { label = "A", minRatio = 1.50f },
            new RankTier { label = "S", minRatio = 2.00f },
        };

        [Header("게이지")]
        [SerializeField, Tooltip("체크하면 목표(100%)에서 게이지가 가득 찬 상태로 멈춘다. 랭크는 계속 올라간다")]
        bool clampFillAtTarget = true;

        [SerializeField, Min(0f), Tooltip("게이지가 목표값을 따라가는 속도. 0 이면 즉시")]
        float fillLerpSpeed = 6f;

        int _score;
        float _targetFill;
        int _rankIndex = -1;
        [SerializeField, Min(0f)] float cardPresentationDelay = 0.085f;
        float _lastCardPresentationAt = float.NegativeInfinity;
        Coroutine _scoreRoutine;

        /// <summary>지금 랭크 이름. 결과 화면에서 읽어 쓸 수 있다.</summary>
        public string CurrentRankLabel =>
            _rankIndex >= 0 && _rankIndex < rankTiers.Length ? rankTiers[_rankIndex].label : "F";

        /// <summary>목표 점수 대비 달성 비율. 목표가 0이면 0.</summary>
        public float AchievedRatio
        {
            get
            {
                int target = PerformanceTimer.TargetScore;
                return target > 0 ? (float)_score / target : 0f;
            }
        }

        void OnEnable()
        {
            EventBus.Subscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Subscribe<CardPresentationStarted>(OnCardPresentationStarted);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

            // 씬 로드 직후 이벤트가 오기 전에도 현재 점수와 맞춘다
            _score = GameManager.HasInstance ? GameManager.Instance.Score : 0;
            Refresh(instant: true);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ScoreChanged>(OnScoreChanged);
            EventBus.Unsubscribe<CardPresentationStarted>(OnCardPresentationStarted);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            _scoreRoutine = null;
        }

        void Update()
        {
            if (gaugeFill == null) return;

            gaugeFill.fillAmount = fillLerpSpeed <= 0f
                ? _targetFill
                : Mathf.Lerp(gaugeFill.fillAmount, _targetFill, 1f - Mathf.Exp(-fillLerpSpeed * Time.unscaledDeltaTime));
        }

        void OnScoreChanged(ScoreChanged e)
        {
            bool followsCard =
                Time.unscaledTime - _lastCardPresentationAt < 0.1f;
            if (!followsCard || cardPresentationDelay <= 0f)
            {
                ApplyScore(e.Score);
                return;
            }

            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = StartCoroutine(ApplyScoreAfterDelay(e.Score));
        }

        void OnCardPresentationStarted(CardPresentationStarted _) =>
            _lastCardPresentationAt = Time.unscaledTime;

        IEnumerator ApplyScoreAfterDelay(int score)
        {
            yield return new WaitForSecondsRealtime(cardPresentationDelay);
            _scoreRoutine = null;
            ApplyScore(score);
        }

        void ApplyScore(int score)
        {
            _score = score;
            Refresh(instant: false);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연 준비에는 게이지를 즉시 0으로 되돌린다 (이전 판 잔상 제거)
            if (e.Current != GameState.Ready) return;
            if (_scoreRoutine != null)
            {
                StopCoroutine(_scoreRoutine);
                _scoreRoutine = null;
            }
            _score = 0;
            Refresh(instant: true);
        }

        void Refresh(bool instant)
        {
            float ratio = AchievedRatio;

            // 게이지
            _targetFill = clampFillAtTarget ? Mathf.Clamp01(ratio) : Mathf.Max(0f, ratio);
            if (instant && gaugeFill != null) gaugeFill.fillAmount = _targetFill;

            // 랭크 — 조건을 만족하는 가장 높은 구간
            int index = GetRankIndex(ratio, rankTiers);

            if (index == _rankIndex) return; // 같은 랭크면 스프라이트를 다시 꽂지 않는다
            _rankIndex = index;

            if (rankImage == null) return;
            Sprite icon = rankTiers[index].icon;
            rankImage.sprite = icon;
            rankImage.enabled = icon != null;
        }

        /// <summary>비율을 만족하는 가장 높은 랭크 구간의 인덱스를 고른다. 다른 UI(게임오버 팝업 등)에서도 재사용한다.</summary>
        public static int GetRankIndex(float ratio, RankTier[] tiers)
        {
            int index = 0;
            for (int i = 0; i < tiers.Length; i++)
                if (ratio >= tiers[i].minRatio && tiers[i].minRatio >= tiers[index].minRatio)
                    index = i;
            return index;
        }
    }
}
