using System;
using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using LegacyText = UnityEngine.UI.Text;

namespace ContextStage
{
    /// <summary>
    /// 우측 상단 단일 결과 HUD 중 "카드 평가 라벨" 줄. (카드 총점 위에 표시)
    ///
    /// 카드 한 장의 최종 획득 점수(CardResolved.GainedScore)를 구간으로 나눠
    /// LOVE IT! / INTERESTED / (중립은 없음) / NOT FOR ME / BORED 를 보여준다.
    /// 특별 관객 성공(IsSpecialHit)에는 전용 라벨(SPECIAL!)을 띄운다.
    ///
    /// - CardResolved 만 구독한다 (개별 관객 반응은 보지 않는다)
    /// - 관객 Actor·Popup 을 참조하지 않는다
    /// - CardTotalTextUI 와 같은 타이밍(hold → fade)으로 사라진다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardReactionTextUI : MonoBehaviour
    {
        /// <summary>점수 구간 한 칸. minScore 이상이면 이 라벨을 쓴다 (내림차순으로 평가).</summary>
        [Serializable]
        public struct ReactionTier
        {
            [Tooltip("이 점수 이상이면 이 라벨. 리스트에서 가장 높은 조건이 우선한다")]
            public int minScore;

            [Tooltip("표시할 문구. 비우면 아무것도 표시하지 않는다 (중립 구간)")]
            public string label;

            public Color color;
        }

        [Header("Text (assign either one)")]
        [SerializeField] TMP_Text reactionText;
        [SerializeField] LegacyText legacyReactionText;

        [Header("Special Hit 전용 라벨")]
        [SerializeField] string specialHitLabel = "SPECIAL!";
        [SerializeField] Color specialHitColor = new Color(0.35f, 1f, 0.85f, 1f);
        [SerializeField] string mixedReactionLabel = "MIXED REACTION";
        [SerializeField] Color mixedReactionColor = new Color(0.75f, 0.8f, 0.85f, 1f);

        [Header("점수 구간 (minScore 내림차순 자동 정렬)")]
        [SerializeField]
        ReactionTier[] tiers =
        {
            new ReactionTier { minScore = 20,  label = "LOVE IT!",    color = new Color(1f, 0.84f, 0.25f, 1f) },
            new ReactionTier { minScore = 9,   label = "INTERESTED",  color = new Color(0.95f, 0.95f, 0.55f, 1f) },
            new ReactionTier { minScore = -5,  label = "",            color = Color.clear }, // 중립: -5~8 은 표시 없음
            new ReactionTier { minScore = -16, label = "NOT FOR ME",  color = new Color(1f, 0.55f, 0.2f, 1f) },
            new ReactionTier { minScore = int.MinValue, label = "BORED", color = new Color(1f, 0.28f, 0.22f, 1f) },
        };

        [Header("Presentation")]
        [SerializeField, Min(0.1f)] float holdDuration = 0.8f;
        [SerializeField, Min(0.01f)] float fadeDuration = 0.35f;
        [SerializeField, Min(0f)] float presentationDelay = 0.085f;

        float _visibleUntil;
        float _hiddenAt;
        Color _baseColor = Color.white;
        bool _animating;
        Coroutine _presentationRoutine;

        void OnEnable()
        {
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            SetAlpha(0f); // 시작은 숨김 (카드를 낼 때만 나타난다)
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            _presentationRoutine = null;
        }

        void OnCardResolved(CardResolved e)
        {
            if (_presentationRoutine != null)
                StopCoroutine(_presentationRoutine);
            _presentationRoutine = StartCoroutine(ShowAfterDelay(e));
        }

        IEnumerator ShowAfterDelay(CardResolved result)
        {
            if (presentationDelay > 0f)
                yield return new WaitForSecondsRealtime(presentationDelay);
            _presentationRoutine = null;
            ShowResolved(result);
        }

        void ShowResolved(CardResolved e)
        {
            if (e.FeverBonusScore > 0)
            {
                SetAlpha(0f);
                _animating = false;
                return;
            }

            // Utility 카드는 점수 평가 대상이 아니다
            if (e.Role == CardRole.Utility && e.GainedScore == 0) return;

            string label;
            Color color;

            if (e.IsSpecialHit)
            {
                label = specialHitLabel;
                color = specialHitColor;
            }
            else if (!TryResolveTier(e.GainedScore, out label, out color))
            {
                // 어떤 카드 결과도 무반응으로 끝나지 않게 한다.
                label = mixedReactionLabel;
                color = mixedReactionColor;
            }

            _baseColor = color;
            SetText(label);
            SetColor(color);
            SetAlpha(1f);

            _visibleUntil = Time.unscaledTime + holdDuration;
            _hiddenAt = _visibleUntil + fadeDuration;
            _animating = true;
        }

        void Update()
        {
            if (!_animating) return;

            float now = Time.unscaledTime;
            if (now < _visibleUntil) return;
            if (now >= _hiddenAt) { SetAlpha(0f); _animating = false; return; }

            SetAlpha((_hiddenAt - now) / Mathf.Max(0.01f, fadeDuration)); // 1 → 0
        }

        /// <summary>점수 → 라벨. 빈 라벨(중립)이면 false 를 돌려준다.</summary>
        bool TryResolveTier(int score, out string label, out Color color)
        {
            label = null;
            color = Color.white;

            int bestMin = int.MinValue;
            bool found = false;
            for (int i = 0; i < tiers.Length; i++)
            {
                var tier = tiers[i];
                if (score < tier.minScore) continue;      // 이 구간의 최소 점수에 못 미침
                if (found && tier.minScore <= bestMin) continue; // 이미 더 높은 구간을 잡음

                bestMin = tier.minScore;
                label = tier.label;
                color = tier.color;
                found = true;
            }

            return found && !string.IsNullOrEmpty(label);
        }

        void SetText(string value)
        {
            if (reactionText != null) reactionText.text = value;
            if (legacyReactionText != null) legacyReactionText.text = value;
        }

        void SetColor(Color value)
        {
            _baseColor = value;
            if (reactionText != null) reactionText.color = value;
            if (legacyReactionText != null) legacyReactionText.color = value;
        }

        void SetAlpha(float alpha)
        {
            Color c = _baseColor;
            c.a = alpha;
            if (reactionText != null) reactionText.color = c;
            if (legacyReactionText != null) legacyReactionText.color = c;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            holdDuration = Mathf.Max(0.1f, holdDuration);
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
            presentationDelay = Mathf.Max(0f, presentationDelay);
        }
#endif
    }
}
