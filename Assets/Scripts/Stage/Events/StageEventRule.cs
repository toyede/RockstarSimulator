using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 스테이지 기믹 이벤트의 공통 뼈대. 한 번에 하나만 진행된다 (전역 게이트).
    ///
    ///   스케줄형 : triggerTimes(공연 경과 초)에 도달하면 시작. 다른 이벤트가 진행 중이면 끝날 때까지 기다린다
    ///   조건형   : triggerTimes 를 비우고, 하위 클래스가 조건을 보고 TryBegin() 을 부른다
    ///   창       : window 초 동안 CheckSuccess() 가 true 면 성공, 창이 닫히면 OnWindowExpired() 가 결정 (기본 실패)
    ///
    /// 알림은 StageEventStarted/Progress/Resolved 로만 내보내고 UI(StageEventNoticeUI)는 룰을 모른다.
    /// 수치는 인스펙터(씬의 [StageRuntime]/Rule_*)에서 조절한다.
    /// </summary>
    public abstract class StageEventRule : StageRuleBehaviour
    {
        [Header("이벤트 공통")]
        [SerializeField, Tooltip("알림 제목")] protected string title = "이벤트";
        [SerializeField, Tooltip("공연 경과 몇 초에 시작할지. 비우면 조건형")] protected float[] triggerTimes = new float[0];
        [SerializeField, Min(1f), Tooltip("판정 창(초)")] protected float window = 8f;
        [SerializeField, Tooltip("특별 관객 요청이 진행 중이면 그 요청이 끝날 때까지 미룬다")] protected bool waitForSpecialRequest = true;

        [Header("통계 (읽기 전용)")]
        [SerializeField] int eventsResolved;
        [SerializeField] int eventsSucceeded;

        static readonly List<StageEventRule> s_activeRules = new List<StageEventRule>();
        static StageEventRule s_running;

        float _remaining;
        int _nextTriggerIndex;

        public bool IsEventActive => s_running == this;
        public float Remaining => _remaining;
        public int EventsResolved => eventsResolved;
        public int EventsSucceeded => eventsSucceeded;

        /// <summary>어떤 기믹 이벤트든 진행 중인지.</summary>
        public static bool AnyRunning => s_running != null;

        /// <summary>알림에 보여줄 지시문 (시작 시점 값).</summary>
        protected abstract string Instruction { get; }

        /// <summary>진행도 문구 (매 프레임). 없으면 빈 문자열.</summary>
        protected virtual string ProgressText => "";

        // ---------------- 룰 수명 ----------------

        protected override void OnActivate(StageRuleContext context)
        {
            _nextTriggerIndex = 0;
            _remaining = 0f;
            eventsResolved = 0;
            eventsSucceeded = 0;
            if (!s_activeRules.Contains(this)) s_activeRules.Add(this);
            EventBus.Subscribe<CardResolved>(HandleCardResolved);
            EventBus.Subscribe<ComboChanged>(HandleComboChanged);
            OnEventRuleActivate(context);
        }

        protected override void OnDeactivate()
        {
            EventBus.Unsubscribe<CardResolved>(HandleCardResolved);
            EventBus.Unsubscribe<ComboChanged>(HandleComboChanged);
            if (IsEventActive) Cancel();
            s_activeRules.Remove(this);
            OnEventRuleDeactivate();
        }

        protected virtual void OnEventRuleActivate(StageRuleContext context) { }
        protected virtual void OnEventRuleDeactivate() { }

        // ---------------- 매 프레임 ----------------

        void Update()
        {
            if (!IsActive || !GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;

            if (IsEventActive)
            {
                float dt = Time.deltaTime;
                _remaining = Mathf.Max(0f, _remaining - dt);
                OnEventTick(dt);
                EventBus.Raise(new StageEventProgress(RuleId, _remaining, window, ProgressText));

                if (CheckSuccess()) Resolve(true);
                else if (_remaining <= 0f) Resolve(OnWindowExpired());
                return;
            }

            OnIdleTick();

            if (triggerTimes != null && _nextTriggerIndex < triggerTimes.Length &&
                PerformanceTimer.Elapsed >= triggerTimes[_nextTriggerIndex])
            {
                // 다른 이벤트·특별 관객 요청이 끝날 때까지 같은 인덱스로 기다린다
                if (TryBegin()) _nextTriggerIndex++;
            }
        }

        /// <summary>조건형 이벤트가 매 프레임 조건을 볼 때.</summary>
        protected virtual void OnIdleTick() { }

        // ---------------- 시작 · 종료 ----------------

        /// <summary>지금 시작할 수 있는지. 하위 클래스가 조건을 더할 수 있다.</summary>
        protected virtual bool CanBegin()
        {
            if (s_running != null) return false;
            if (waitForSpecialRequest && SpecialAudience.HasActiveRequest) return false;
            return true;
        }

        /// <summary>조건형 이벤트 시작 시도. 게이트에 막히면 false.</summary>
        protected bool TryBegin()
        {
            if (!IsActive || IsEventActive || !CanBegin()) return false;
            Begin();
            return true;
        }

        void Begin()
        {
            s_running = this;
            _remaining = window;
            OnEventBegin();
            Debug.Log($"[StageEvent] 시작: {title} ({RuleId}) · {window:0}s · {Instruction}", this);
            EventBus.Raise(new StageEventStarted(RuleId, title, Instruction, window));
        }

        void Resolve(bool success)
        {
            if (!IsEventActive) return;
            s_running = null;
            eventsResolved++;
            if (success) eventsSucceeded++;
            string result = OnEventEnd(success);
            Debug.Log($"[StageEvent] {(success ? "성공" : "실패")}: {title} · {result}", this);
            EventBus.Raise(new StageEventResolved(RuleId, title, success, result));
        }

        /// <summary>룰 해제·공연 종료로 조용히 끝낸다 (결과 이벤트 없음).</summary>
        void Cancel()
        {
            if (s_running == this) s_running = null;
            OnEventCancel();
        }

        // ---------------- 하위 클래스 훅 ----------------

        protected abstract void OnEventBegin();
        protected virtual void OnEventTick(float deltaTime) { }
        protected abstract bool CheckSuccess();

        /// <summary>창이 닫혔을 때의 판정. 기본은 실패.</summary>
        protected virtual bool OnWindowExpired() => false;

        /// <summary>보상·벌칙을 적용하고 결과 문구를 돌려준다.</summary>
        protected abstract string OnEventEnd(bool success);

        protected virtual void OnEventCancel() { }
        protected virtual void OnCardResolved(CardResolved e) { }
        protected virtual void OnComboChanged(ComboChanged e) { }

        void HandleCardResolved(CardResolved e) { if (IsActive) OnCardResolved(e); }
        void HandleComboChanged(ComboChanged e) { if (IsActive) OnComboChanged(e); }

        // ---------------- 도우미 ----------------

        protected static string PreferenceLabel(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return "CHILL";
                case CrowdPreference.Singalong: return "SINGALONG";
                case CrowdPreference.Mosh: return "MOSH";
                default: return preference.ToString().ToUpperInvariant();
            }
        }

        /// <summary>몰입도가 가장 낮은 관객 한 명을 내보낸다. 없으면 false.</summary>
        protected bool RemoveLowestEngagement(AudienceDepartureReason reason)
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null || roster.Count == 0) return false;
            IReadOnlyList<AudienceSnapshot> members = roster.Members;
            AudienceId lowestId = default;
            float lowest = float.MaxValue;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Engagement >= lowest) continue;
                lowest = members[i].Engagement;
                lowestId = members[i].Id;
            }
            return lowest < float.MaxValue && roster.TryRemove(lowestId, reason, out _);
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] 제목은 항상 갱신, 스케줄·창은 아직 기본값(비어 있음)일 때만 채운다.</summary>
        public void EditorConfigure(string eventTitle, float[] times, float windowSeconds)
        {
            title = eventTitle;
            if (triggerTimes == null || triggerTimes.Length == 0) triggerTimes = times ?? new float[0];
            if (Mathf.Approximately(window, 8f)) window = Mathf.Max(1f, windowSeconds);
        }
#endif

        // ---------------- 디버그 ----------------

        /// <summary>[디버그] 스케줄과 상관없이 지금 시작. 진행 중이면 무시.</summary>
        public bool DebugTriggerNow() => TryBegin();

        /// <summary>[디버그] 활성 룰 중 아직 덜 돌린 이벤트부터 시작한다 (F5). 누를 때마다 다른 이벤트가 나온다.</summary>
        public static bool DebugTriggerFirstIdle()
        {
            if (s_running != null) return false;
            StageEventRule pick = null;
            for (int i = 0; i < s_activeRules.Count; i++)
            {
                StageEventRule rule = s_activeRules[i];
                if (rule == null || !rule.IsActive) continue;
                if (pick == null || rule.eventsResolved < pick.eventsResolved) pick = rule;
            }
            return pick != null && pick.DebugTriggerNow();
        }
    }
}
