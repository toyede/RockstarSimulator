using System.Collections.Generic;
using System.Text;
using GameJamKit;
using TMPro;
using UnityEngine;
using LegacyText = UnityEngine.UI.Text;

namespace ContextStage
{
    /// <summary>
    /// 저격 성공 순간, 플레이어가 <b>무엇을 얻었는지</b>를 화면 한가운데에서 크게 알린다.
    ///
    /// <example>
    /// <code>
    /// ★ SPECIAL HIT ★
    /// +320  ·  관객 +3  ·  COMBO x1.5
    /// </code>
    /// </example>
    ///
    /// <b>카드 시스템을 수정하지 않는다.</b> 저격 효과의 실행 결과는 CardSystem 안에서
    /// 버려지지만(<c>out _</c>), 실제로 일어난 일은 전부 이벤트로 흘러나온다:
    ///
    /// | 이벤트 | 뽑는 값 |
    /// |---|---|
    /// | <c>AudienceJoined</c> (SpecialCardTarget) | 새로 들어온 관객 수 |
    /// | <c>AudienceStateChanged</c> (SpecialCardTarget) | 몰입도가 바뀐 관객 수 |
    /// | <c>SpecialHitLanded</c> | 연출 유지 시간 |
    /// | <c>CardResolved</c> (IsSpecialHit) | 최종 점수·콤보 배율 |
    ///
    /// 카드 설정값이 아니라 <b>실제 적용 결과</b>를 읽으므로, 카드 밸런스를 바꿔도
    /// 문구가 자동으로 맞는다.
    ///
    /// 순서 주의: 관객 효과와 SpecialHitLanded 는 CardResolved <b>보다 먼저</b> 발행된다.
    /// 그래서 관객 쪽 값을 먼저 모아 두었다가 CardResolved 가 올 때 합쳐서 한 장으로 띄운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialHitRewardBanner : MonoBehaviour
    {
        [Header("텍스트 (TMP 와 Legacy 중 있는 쪽을 쓴다)")]
        [SerializeField, Tooltip("큰 제목 줄")]
        TMP_Text headlineText;

        [SerializeField, Tooltip("보상 상세 줄. 비워두면 제목만 뜬다")]
        TMP_Text detailText;

        [SerializeField, Tooltip("프로젝트 HUD 는 Legacy Text 를 쓴다. 셋업 메뉴가 이쪽을 연결한다")]
        LegacyText legacyHeadlineText;

        [SerializeField] LegacyText legacyDetailText;

        [SerializeField, Tooltip("전체를 함께 페이드하려면 연결한다. 비워두면 각 텍스트 알파를 직접 만진다")]
        CanvasGroup canvasGroup;

        [Header("문구")]
        [SerializeField] string headline = "★ SPECIAL HIT ★";

        [SerializeField, Tooltip("{0} = 획득 점수. 0이면 표시하지 않는다")]
        string scoreFormat = "+{0}";

        [SerializeField, Tooltip("{0} = 새로 들어온 관객 수. 0이면 표시하지 않는다")]
        string joinedFormat = "관객 +{0}";

        [SerializeField, Tooltip("{0} = 몰입도가 오른 관객 수. 0이면 표시하지 않는다")]
        string boostedFormat = "관객 {0}명 열광";

        [SerializeField, Tooltip(
            "{0} = 몰입도가 내려간 관객 수. 모쉬핏처럼 다른 관객을 진정시키는 카드가 있다. " +
            "보상 배너라 기본적으로는 표시하지 않는다 — 보여주려면 문구를 채운다")]
        string calmedFormat = "";

        [SerializeField, Tooltip("{0} = 콤보 배율. 배율이 1 이하면 표시하지 않는다")]
        string comboFormat = "COMBO x{0:0.0}";

        [SerializeField, Tooltip("상세 항목 사이에 넣는 구분자")]
        string separator = "  ·  ";

        [Header("색")]
        [SerializeField] Color headlineColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] Color detailColor = new Color(0.95f, 0.98f, 1f, 1f);

        [Header("연출")]
        [SerializeField, Tooltip(
            "켜면 SpecialHitLanded 의 연출 유지 시간을 그대로 쓴다. " +
            "특별 관객이 환호하는 동안 배너가 떠 있게 된다")]
        bool useSpecialHitHoldDuration = true;

        [SerializeField, Min(0.05f), Tooltip("유지 시간(초). 위 옵션이 꺼져 있을 때 쓴다")]
        float holdDuration = 1.1f;

        [SerializeField, Min(0.01f)] float popInDuration = 0.12f;
        [SerializeField, Min(0.01f)] float fadeOutDuration = 0.35f;

        [SerializeField, Min(0.01f), Tooltip("톡 튀어나올 때의 최대 크기 배율")]
        float popPeakScale = 1.35f;

        [Header("집계")]
        [SerializeField, Min(0.01f), Tooltip(
            "관객 효과와 카드 결과를 같은 저격으로 묶는 시간(초). " +
            "둘은 같은 프레임에 발생하므로 짧아도 된다")]
        float collectWindow = 0.25f;

        // ---------------- 상태 ----------------

        readonly StringBuilder _builder = new StringBuilder(64);
        readonly List<string> _segments = new List<string>(4);

        int _pendingJoinedCount;
        int _pendingBoostedCount;
        int _pendingCalmedCount;
        float _pendingHoldDuration;
        float _pendingCapturedAt = float.NegativeInfinity;

        bool _playing;
        float _elapsed;
        float _activeHold;
        Vector3 _baseScale = Vector3.one;

        float VisibleTotal => popInDuration + _activeHold + fadeOutDuration;

        void Awake()
        {
            _baseScale = transform.localScale;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        void OnEnable()
        {
            EventBus.Subscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Subscribe<AudienceStateChanged>(OnAudienceStateChanged);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHitLanded);
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            SetVisible(false);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Unsubscribe<AudienceStateChanged>(OnAudienceStateChanged);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHitLanded);
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            SetVisible(false);
            ClearPending();
        }

        // ---------------- 수집 (CardResolved 보다 먼저 도착한다) ----------------

        void OnAudienceJoined(AudienceJoined e)
        {
            if (e.Reason != AudienceJoinReason.SpecialCardTarget) return;

            TouchPending();
            _pendingJoinedCount++;
        }

        /// <summary>
        /// 같은 SpecialCardTarget 사유라도 올라간 것과 내려간 것이 섞여 있다.
        /// (모쉬핏 카드는 모쉬 관객을 부르면서 나머지 관객의 몰입도를 눌러 놓는다)
        /// 실제 증감 방향으로 나눠서 "열광"과 "진정"을 혼동하지 않는다.
        /// </summary>
        void OnAudienceStateChanged(AudienceStateChanged e)
        {
            if (e.Reason != AudienceChangeReason.SpecialCardTarget) return;

            TouchPending();
            if (e.Current.Engagement > e.Previous.Engagement) _pendingBoostedCount++;
            else if (e.Current.Engagement < e.Previous.Engagement) _pendingCalmedCount++;
        }

        void OnSpecialHitLanded(SpecialHitLanded e)
        {
            TouchPending();
            _pendingHoldDuration = Mathf.Max(0f, e.HoldDuration);
        }

        /// <summary>수집 창이 지났으면 이전 저격의 잔여값을 버린다.</summary>
        void TouchPending()
        {
            if (Time.unscaledTime - _pendingCapturedAt > collectWindow) ClearPending();
            _pendingCapturedAt = Time.unscaledTime;
        }

        void ClearPending()
        {
            _pendingJoinedCount = 0;
            _pendingBoostedCount = 0;
            _pendingCalmedCount = 0;
            _pendingHoldDuration = 0f;
            _pendingCapturedAt = float.NegativeInfinity;
        }

        // ---------------- 표시 ----------------

        void OnCardResolved(CardResolved e)
        {
            if (!e.IsSpecialHit) return;

            bool fresh = Time.unscaledTime - _pendingCapturedAt <= collectWindow;
            int joined = fresh ? _pendingJoinedCount : 0;
            int boosted = fresh ? _pendingBoostedCount : 0;
            int calmed = fresh ? _pendingCalmedCount : 0;
            float hold = fresh && _pendingHoldDuration > 0f ? _pendingHoldDuration : holdDuration;
            ClearPending();

            Play(BuildDetail(e, joined, boosted, calmed),
                 useSpecialHitHoldDuration ? hold : holdDuration);
        }

        string BuildDetail(CardResolved e, int joinedCount, int boostedCount, int calmedCount)
        {
            _segments.Clear();

            if (e.GainedScore != 0 && !string.IsNullOrEmpty(scoreFormat))
                _segments.Add(string.Format(scoreFormat, e.GainedScore));

            if (joinedCount > 0 && !string.IsNullOrEmpty(joinedFormat))
                _segments.Add(string.Format(joinedFormat, joinedCount));

            if (boostedCount > 0 && !string.IsNullOrEmpty(boostedFormat))
                _segments.Add(string.Format(boostedFormat, boostedCount));

            if (calmedCount > 0 && !string.IsNullOrEmpty(calmedFormat))
                _segments.Add(string.Format(calmedFormat, calmedCount));

            if (e.ComboMultiplier > 1f && !string.IsNullOrEmpty(comboFormat))
                _segments.Add(string.Format(comboFormat, e.ComboMultiplier));

            _builder.Clear();
            for (int i = 0; i < _segments.Count; i++)
            {
                if (i > 0) _builder.Append(separator);
                _builder.Append(_segments[i]);
            }

            return _builder.ToString();
        }

        void Play(string detail, float hold)
        {
            _activeHold = Mathf.Max(0.05f, hold);
            _elapsed = 0f;
            _playing = true;

            SetHeadline(headline);
            SetDetail(detail);
            SetVisible(true);
            transform.localScale = Vector3.zero;
        }

        void Update()
        {
            if (!_playing) return;

            // 저격 성공 순간 히트스톱으로 timeScale 이 떨어져도 배너는 정상 속도로 재생돼야 한다
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= VisibleTotal)
            {
                _playing = false;
                SetVisible(false);
                transform.localScale = _baseScale;
                return;
            }

            float punch = _elapsed < popInDuration
                ? Mathf.Lerp(0f, popPeakScale, _elapsed / popInDuration)
                : Mathf.Lerp(popPeakScale, 1f, Mathf.Clamp01((_elapsed - popInDuration) / 0.18f));
            transform.localScale = _baseScale * punch;

            float fadeElapsed = _elapsed - (popInDuration + _activeHold);
            float alpha = fadeElapsed <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(fadeElapsed / fadeOutDuration);
            SetAlpha(alpha);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연이 시작되면 이전 판의 배너가 남지 않게 한다
            if (e.Current != GameState.Ready) return;

            _playing = false;
            SetVisible(false);
            transform.localScale = _baseScale;
            ClearPending();
        }

        void SetHeadline(string value)
        {
            if (headlineText != null) headlineText.text = value;
            if (legacyHeadlineText != null) legacyHeadlineText.text = value;
        }

        void SetDetail(string value)
        {
            if (detailText != null) detailText.text = value;
            if (legacyDetailText != null) legacyDetailText.text = value;
        }

        void SetVisible(bool visible)
        {
            if (canvasGroup != null) canvasGroup.alpha = visible ? 1f : 0f;

            if (headlineText != null) headlineText.enabled = visible;
            if (detailText != null) detailText.enabled = visible;
            if (legacyHeadlineText != null) legacyHeadlineText.enabled = visible;
            if (legacyDetailText != null) legacyDetailText.enabled = visible;
            if (!visible) return;

            SetAlpha(1f);
        }

        void SetAlpha(float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
                return;
            }

            Color head = headlineColor;
            head.a = alpha;
            if (headlineText != null) headlineText.color = head;
            if (legacyHeadlineText != null) legacyHeadlineText.color = head;

            Color detail = detailColor;
            detail.a = alpha;
            if (detailText != null) detailText.color = detail;
            if (legacyDetailText != null) legacyDetailText.color = detail;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Play Sample Banner")]
        void DebugPlaySample()
        {
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
            Play("+320  ·  관객 +3  ·  COMBO x1.5", holdDuration);
        }
#endif
    }
}
