using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using LegacyText = UnityEngine.UI.Text;

namespace ContextStage
{
    /// <summary>
    /// 우측 상단 단일 결과 HUD 중 "이번 카드 총점" 줄.
    ///
    /// 카드 한 장을 낼 때 모든 관객 반응을 합산한 최종 점수(CardResolved.GainedScore)를
    /// 한 번만 보여주고 잠깐 뒤 페이드아웃한다. 캐릭터 위에는 아무 텍스트도 띄우지 않는다.
    ///
    /// - CardResolved 만 구독한다 (개별 AudienceCardReacted 는 보지 않는다)
    /// - 관객 Actor·Popup 을 직접 참조하지 않는다
    /// - 콤보 표시는 짝인 ComboTextUI 가 담당한다 (책임 분리)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardTotalTextUI : MonoBehaviour
    {
        [Header("Text (assign either one)")]
        [SerializeField] TMP_Text totalText;
        [SerializeField] LegacyText legacyTotalText;

        [Header("Presentation")]
        [SerializeField, Min(0.1f), Tooltip("점수를 그대로 유지하는 시간(초)")]
        float holdDuration = 0.8f;

        [SerializeField, Min(0.01f), Tooltip("페이드아웃에 걸리는 시간(초)")]
        float fadeDuration = 0.35f;
        [SerializeField, Min(0f), Tooltip(
            "관객 반응 폭발 뒤 결과 숫자를 보여주기 위한 실시간 지연")]
        float presentationDelay = 0.085f;

        [Header("Colors")]
        [SerializeField] Color positiveColor = new Color(1f, 0.88f, 0.25f, 1f);
        [SerializeField] Color zeroColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] Color negativeColor = new Color(1f, 0.28f, 0.22f, 1f);
        [SerializeField] Color specialColor = new Color(0.35f, 1f, 0.85f, 1f);
        [SerializeField] Color feverColor = new Color(0.25f, 1f, 0.95f, 1f);

        float _visibleUntil;    // 이 시각까지는 alpha 1 유지
        float _hiddenAt;        // 이 시각이면 완전히 사라짐
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
            // Utility 카드는 점수 개념이 없으므로 결과 텍스트를 띄우지 않는다 (콤보도 유지되는 카드다)
            if (e.Role == CardRole.Utility && e.GainedScore == 0)
            {
                SetText("UTILITY");
                _baseColor = zeroColor;
            }
            else
            {
                _baseColor = ResolveColor(e);
                SetText(FormatScore(e));
            }

            SetColor(_baseColor);
            SetAlpha(1f);

            _visibleUntil = Time.unscaledTime + holdDuration;
            _hiddenAt = _visibleUntil + fadeDuration;
            _animating = true;
        }

        void Update()
        {
            if (!_animating) return;

            float now = Time.unscaledTime;
            if (now < _visibleUntil) return;             // 아직 유지 구간
            if (now >= _hiddenAt) { SetAlpha(0f); _animating = false; return; }

            float k = (_hiddenAt - now) / Mathf.Max(0.01f, fadeDuration); // 1 → 0
            SetAlpha(k);
        }

        static string FormatScore(CardResolved e)
        {
            // 평가 문구(LOVE IT!/SPECIAL! 등)는 위 CardReactionTextUI 가 담당한다.
            // 이 줄은 숫자 점수만 보여준다.
            if (e.FeverBonusScore > 0)
            {
                return $"FEVER +{e.FeverBonusScore:N0} SCORE";
            }
            if (e.GainedScore > 0) return $"+{e.GainedScore:N0} SCORE";
            if (e.GainedScore < 0) return $"{e.GainedScore:N0} SCORE";
            return "NO SCORE";
        }

        Color ResolveColor(CardResolved e)
        {
            if (e.FeverBonusScore > 0) return feverColor;
            if (e.IsSpecialHit) return specialColor;
            if (e.GainedScore > 0) return positiveColor;
            if (e.GainedScore < 0) return negativeColor;
            return zeroColor;
        }

        // ---------------- 텍스트 헬퍼 (ComboTextUI 와 동일 패턴) ----------------

        void SetText(string value)
        {
            if (totalText != null) totalText.text = value;
            if (legacyTotalText != null) legacyTotalText.text = value;
        }

        void SetColor(Color value)
        {
            _baseColor = value;
            if (totalText != null) totalText.color = value;
            if (legacyTotalText != null) legacyTotalText.color = value;
        }

        void SetAlpha(float alpha)
        {
            Color c = _baseColor;
            c.a = alpha;
            if (totalText != null) totalText.color = c;
            if (legacyTotalText != null) legacyTotalText.color = c;
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
