using System.Collections;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 공연 시간이 흐르기 전(Ready)에 시작형 증강을 하나씩 보여 준 뒤 공연을 시작한다.
    /// 사전 홍보·취향 간파·손패 증감·전력 질주의 효과를 처음에는 빼 두었다가 문구와 함께 적용한다.
    /// 해당 증강이 없으면 아무것도 붙잡지 않으므로 기존 자동 시작과 동일하다.
    /// 튜토리얼이 뜨는 공연이면 튜토리얼이 끝나고 공연을 다시 시작할 때 연출한다.
    /// </summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class PerformanceIntroDirector : MonoBehaviour
    {
        [SerializeField] AudienceRosterPresenter presenter;
        [SerializeField] AudiencePreferenceHoverController preferenceHover;
        [SerializeField] TutorialFlow tutorial;

        [Header("연출 시간 (초)")]
        [SerializeField, Min(0f), Tooltip("씬이 보인 뒤 첫 문구까지 기다리는 시간")]
        float startDelay = 0.5f;
        [SerializeField, Min(0f), Tooltip("문구가 튀어나온 뒤 효과를 적용하기까지")]
        float effectDelay = 0.25f;
        [SerializeField, Min(0.1f), Tooltip("증강 하나에 쓰는 전체 시간")]
        float stepDuration = 1.2f;
        [SerializeField, Min(0f)] float audienceJoinInterval = 0.2f;
        [SerializeField, Min(0f), Tooltip("마지막 문구 뒤 공연 시작까지")]
        float endDelay = 0.3f;

        static int s_deferredInitialAudience;
        static int s_deferredHandDelta;
        static bool s_deferredDuration;
        static bool s_deferredReveal;
        static bool s_highRiskTempo;
        static bool s_planned;
        static PerformanceIntroDirector s_active;

        /// <summary>시작 연출이 공연 시작을 붙잡고 있는 동안 true.</summary>
        public static bool IsHolding { get; private set; }

        /// <summary>연출 전까지 손패 크기에 증강 보정을 넣지 않는다.</summary>
        public static bool DefersHandSize => s_planned && s_deferredHandDelta != 0;

        /// <summary>연출 전까지 취향 테두리를 켜지 않는다.</summary>
        public static bool DefersPreferenceReveal => s_planned && s_deferredReveal;

        /// <summary>관객 초기 인원에 더할 증강 보정. 연출이 나중에 넣을 인원은 빼고 돌려준다.</summary>
        public static int InitialAudienceBonus(AugmentRuntimeModifiers modifiers)
            => s_planned && s_deferredInitialAudience > 0 ? 0 : modifiers.InitialAudienceBonus;

        /// <summary>
        /// 튜토리얼이 끝나 공연을 다시 시작하기 직전(Ready)에 호출한다.
        /// 미뤄 둔 연출이 있으면 연출 뒤에 직접 공연을 시작하고 true, 없으면 false.
        /// </summary>
        public static bool TryRunAfterTutorial()
        {
            if (s_active == null || !s_planned || IsHolding) return false;
            s_active.Begin();
            return true;
        }

        void Awake()
        {
            ResetStatics();
            s_active = this;
            if (!TourRunManager.HasInstance || TourRunManager.Instance.CurrentRun == null) return;

            AugmentRuntimeModifiers modifiers = AugmentRuntime.Current;
            s_deferredInitialAudience = Mathf.Max(0, modifiers.InitialAudienceBonus);
            s_deferredHandDelta = modifiers.MinimumHandSizeDelta;
            s_deferredDuration = !Mathf.Approximately(modifiers.PerformanceDurationMultiplier, 1f);
            s_deferredReveal = modifiers.RevealAudiencePreferences;
            s_highRiskTempo = modifiers.ComboGainPerSuccessfulCard > 1;
            s_planned = s_deferredInitialAudience > 0 || s_deferredHandDelta != 0 ||
                        s_deferredDuration || s_deferredReveal || s_highRiskTempo;

            // TourPerformanceBridge 가 이미 줄인 시간을 원래 길이로 되돌려 두고, 연출에서 줄인다.
            if (s_deferredDuration && TryGetStage(out StageDefinition stage))
                PerformanceTimerSystem.Instance.SetRuntimeStageSettings(stage.Duration, stage.TargetScore);
        }

        void Start()
        {
            // 실행 순서상 CardSystem.Start(자동 시작)보다 먼저 불린다.
            if (!s_planned) return;
            if (tutorial != null && tutorial.WillAutoRunOnStart) return; // 튜토리얼 뒤로 미룬다
            Begin();
        }

        void Begin()
        {
            IsHolding = true;
            StartCoroutine(Run());
        }

        void OnDestroy()
        {
            if (IsHolding) CardInput.Locked = false;
            if (s_active == this) s_active = null;
            ResetStatics();
        }

        IEnumerator Run()
        {
            bool previousLock = CardInput.Locked;
            CardInput.Locked = true;
            // 튜토리얼 뒤 Ready 에서 TourPerformanceBridge 가 시간을 다시 줄였을 수 있어 원래 길이로 되돌린다.
            if (s_deferredDuration && TryGetStage(out StageDefinition fullStage))
                PerformanceTimerSystem.Instance.SetRuntimeStageSettings(fullStage.Duration, fullStage.TargetScore);
            yield return new WaitForSecondsRealtime(startDelay);

            if (s_deferredInitialAudience > 0)
                yield return Step(AugmentNoticeKind.PreAdvertising, AddDeferredAudience());

            if (s_deferredReveal)
            {
                s_deferredReveal = false;
                yield return Step(AugmentNoticeKind.PreferenceInsight, Do(() =>
                {
                    if (preferenceHover != null) preferenceHover.SetAllPreferencesRevealed(true);
                }));
            }

            if (s_deferredHandDelta > 0 || s_highRiskTempo)
            {
                AugmentNoticeKind kind = s_deferredHandDelta > 0
                    ? AugmentNoticeKind.ExtraHand
                    : AugmentNoticeKind.HighRiskTempo;
                s_deferredHandDelta = 0;
                yield return Step(kind, Do(() =>
                {
                    if (CardSystem.HasInstance) CardSystem.Instance.ApplyAugmentHandSize();
                }));
            }

            if (s_deferredDuration)
            {
                s_deferredDuration = false;
                yield return Step(AugmentNoticeKind.ShortSet, Do(ShortenDuration));
            }

            yield return new WaitForSecondsRealtime(endDelay);
            ResetStatics();
            CardInput.Locked = previousLock;
            if (GameManager.HasInstance && GameManager.Instance.State == GameState.Ready)
                GameManager.Instance.StartGame();
        }

        IEnumerator Step(AugmentNoticeKind kind, IEnumerator effect)
        {
            float startedAt = Time.unscaledTime;
            EventBus.Raise(new AugmentNoticeRequested(kind));
            yield return new WaitForSecondsRealtime(effectDelay);
            yield return effect;
            float remaining = stepDuration - (Time.unscaledTime - startedAt);
            if (remaining > 0f) yield return new WaitForSecondsRealtime(remaining);
        }

        static IEnumerator Do(System.Action action)
        {
            action();
            yield break;
        }

        IEnumerator AddDeferredAudience()
        {
            int count = s_deferredInitialAudience;
            s_deferredInitialAudience = 0;
            if (!AudienceRosterSystem.HasInstance) yield break;

            var roster = AudienceRosterSystem.Instance;
            for (int i = 0; i < count; i++)
            {
                if (!roster.TryAddRandom(AudienceJoinReason.Initialization, out AudienceSnapshot audience))
                    break;
                if (presenter != null && presenter.TryGetActor(audience.Id, out AudienceMemberActor actor))
                    actor.MarkBossArrivalCue();
                if (i < count - 1) yield return new WaitForSecondsRealtime(audienceJoinInterval);
            }
        }

        void ShortenDuration()
        {
            if (!TryGetStage(out StageDefinition stage)) return;

            var timer = PerformanceTimerSystem.Instance;
            float from = timer.Duration;
            float to = stage.Duration * AugmentRuntime.Current.PerformanceDurationMultiplier;
            EventBus.Raise(new PerformanceTimeShortened(from, to));
            timer.SetRuntimeStageSettings(to, stage.TargetScore);
        }

        static bool TryGetStage(out StageDefinition stage)
        {
            // Awake 순서상 타이머의 Awake 가 아직일 수 있어 HasInstance 대신 Instance(씬 검색)를 쓴다.
            stage = TourRunManager.HasInstance ? TourRunManager.Instance.CurrentStageDefinition : null;
            return stage != null && PerformanceTimerSystem.Instance != null;
        }

        static void ResetStatics()
        {
            IsHolding = false;
            s_planned = false;
            s_deferredInitialAudience = 0;
            s_deferredHandDelta = 0;
            s_deferredDuration = false;
            s_deferredReveal = false;
            s_highRiskTempo = false;
        }
    }
}
