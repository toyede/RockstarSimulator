using System.Collections;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 완벽주의 보상이 걸린 공연이 시간 종료되면, 게임오버 전에 무대를 그대로 둔 채
    /// '완벽주의!' 문구와 점수 2배 카운트업을 보여 준 뒤 공연을 끝낸다.
    /// 그동안 상태는 Playing 이므로 BGM·관객·밴드 애니메이션은 이어지고, 게임 진행만 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PerfectClearFinishDirector : MonoBehaviour
    {
        [Header("연출 시간 (초)")]
        [SerializeField, Min(0f), Tooltip("문구가 튀어나온 뒤 점수를 올리기 시작할 때까지")]
        float scoreDelay = 0.35f;
        [SerializeField, Min(0.1f), Tooltip("문구 등장부터 공연 종료(결과 신문)까지")]
        float holdDuration = 2.2f;

        static PerfectClearFinishDirector s_active;
        bool _prevLocked, _prevDecaySuppressed, _prevArrivalsSuppressed, _prevFeverAuto = true;

        /// <summary>시간 종료 후 마무리 연출 중이면 true. 게임 진행 시스템은 이 동안 멈춘다.</summary>
        public static bool IsHolding { get; private set; }

        void OnEnable() => s_active = this;

        void OnDisable()
        {
            if (s_active == this) s_active = null;
            if (IsHolding) EndHold();
        }

        /// <summary>
        /// 제한시간이 끝난 순간 타이머가 호출한다. 완벽주의 보상이 적용될 상황이면 마무리 연출을 시작하고 true.
        /// false 면 호출한 쪽이 기존대로 바로 공연을 끝낸다.
        /// </summary>
        public static bool TryBegin()
        {
            if (s_active == null || IsHolding || !s_active.isActiveAndEnabled) return false;
            if (!PerformanceScoreSystem.HasInstance ||
                !PerformanceScoreSystem.Instance.WillApplyTimedPerformanceFinalRewards)
                return false;

            s_active.StartCoroutine(s_active.Run());
            return true;
        }

        IEnumerator Run()
        {
            BeginHold();
            float multiplier = AugmentRuntime.Current.PerfectClearScoreMultiplier;
            EventBus.Raise(new AugmentNoticeRequested(AugmentNoticeKind.PerfectClear, multiplier));
            yield return new WaitForSecondsRealtime(scoreDelay);

            if (PerformanceScoreSystem.HasInstance)
                PerformanceScoreSystem.Instance.ApplyTimedPerformanceFinalRewards();
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdDuration - scoreDelay));

            EndHold();
            if (GameManager.HasInstance && GameManager.Instance.IsPlaying)
                GameManager.Instance.GameOver();
        }

        void BeginHold()
        {
            IsHolding = true;
            _prevLocked = CardInput.Locked;
            CardInput.Locked = true;
            if (HypeSystem.HasInstance) HypeSystem.Instance.SetDecayPaused(true);
            if (AudienceRosterSystem.HasInstance)
            {
                var roster = AudienceRosterSystem.Instance;
                _prevDecaySuppressed = roster.SuppressEngagementDecay;
                _prevArrivalsSuppressed = roster.SuppressNaturalArrivals;
                roster.SuppressEngagementDecay = true;
                roster.SuppressNaturalArrivals = true;
            }
            if (FeverSystem.HasInstance)
            {
                _prevFeverAuto = FeverSystem.Instance.AutomaticTriggerEnabled;
                FeverSystem.Instance.SetAutomaticTriggerEnabled(false);
            }
        }

        void EndHold()
        {
            IsHolding = false;
            CardInput.Locked = _prevLocked;
            // 씬 종료 중에는 HypeSystem 이 먼저 사라질 수 있다.
            if (HypeSystem.HasInstance) HypeSystem.Instance.SetDecayPaused(false);
            if (AudienceRosterSystem.HasInstance)
            {
                AudienceRosterSystem.Instance.SuppressEngagementDecay = _prevDecaySuppressed;
                AudienceRosterSystem.Instance.SuppressNaturalArrivals = _prevArrivalsSuppressed;
            }
            if (FeverSystem.HasInstance) FeverSystem.Instance.SetAutomaticTriggerEnabled(_prevFeverAuto);
        }
    }
}
