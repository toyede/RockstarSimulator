using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class AudienceRosterSystem : MonoSingleton<AudienceRosterSystem>
    {
        static readonly AudienceSnapshot[] EmptyMembers = Array.Empty<AudienceSnapshot>();

        [SerializeField] AudienceEngagementConfig engagementConfig;
        [SerializeField] AudienceFlowConfig flowConfig;

        readonly List<AudienceSnapshot> _added = new List<AudienceSnapshot>(10);
        readonly List<AudienceSnapshot> _removed = new List<AudienceSnapshot>(10);
        readonly List<AudienceStateChange> _changes = new List<AudienceStateChange>(10);

        AudienceRosterModel _model;
        bool _gameOverRequested;

        protected override bool Persistent => false;

        public AudienceEngagementConfig EngagementConfig => engagementConfig;
        public AudienceFlowConfig FlowConfig => flowConfig;
        public IReadOnlyList<AudienceSnapshot> Members =>
            _model != null ? _model.Members : EmptyMembers;
        public int Count => _model != null ? _model.Count : 0;
        public int Capacity => _model != null ? _model.Capacity : 0;
        public bool IsConfigured => _model != null;
        public AudienceSummary Summary =>
            _model != null ? _model.CreateSummary() : default;
        public float SecondsUntilArrivalCheck =>
            _model != null ? _model.SecondsUntilArrivalCheck : 0f;

        protected override void OnAwake()
        {
            if (!TryCreateModel())
            {
                enabled = false;
                return;
            }

            ResetRoster();
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void Update()
        {
            if (_model == null ||
                !GameManager.HasInstance ||
                !GameManager.Instance.IsPlaying)
                return;

            bool rosterChanged = false;

            // 1) 몰입도 자연 감소 → 상태 변화·이탈 이벤트
            // SuppressEngagementDecay: 튜토리얼이 관객 몰입도를 고정하는 동안만 true.
            // (TrySetEngagement/TryChangeEngagement 수동 호출은 계속 동작한다)
            if (!SuppressEngagementDecay &&
                !IsFeverActive &&
                _model.ApplyNaturalDecay(Time.deltaTime, _changes, _removed))
            {
                for (int i = 0; i < _changes.Count; i++)
                {
                    EventBus.Raise(new AudienceStateChanged(
                        _changes[i],
                        AudienceChangeReason.NaturalDecay));
                }

                for (int i = 0; i < _removed.Count; i++)
                {
                    EventBus.Raise(new AudienceDeparted(
                        _removed[i],
                        AudienceDepartureReason.EngagementDepleted));
                }

                rosterChanged = true;

                // 마지막 관객이 이탈한 프레임에는 유입보다 게임오버를 먼저 확정한다.
                RequestGameOverIfEmpty();
            }

            // 2) 신규 관객의 확률적 유입 (기획서 §4.2)
            // SuppressNaturalArrivals: 튜토리얼이 관객 구성을 고정하는 동안만 true.
            // (수동 TryAdd/TryRemove 는 계속 동작한다)
            if (!SuppressNaturalArrivals &&
                !_gameOverRequested &&
                _model.TryTickArrival(Time.deltaTime, Time.time, out AudienceSnapshot arrived))
            {
                EventBus.Raise(new AudienceJoined(arrived, AudienceJoinReason.NaturalArrival));
                rosterChanged = true;
            }

            if (rosterChanged) RaiseSummary();
        }

        /// <summary>[튜토리얼 전용] true 인 동안 자연 유입을 멈춘다. 평소에는 false.</summary>
        public bool SuppressNaturalArrivals { get; set; }

        /// <summary>[튜토리얼 전용] true인 동안 관객 개별 몰입도 자연 감소를 멈춘다. 평소에는 false.</summary>
        public bool SuppressEngagementDecay { get; set; }

        static bool IsFeverActive =>
            FeverSystem.HasInstance && FeverSystem.Instance.IsActive;

        public bool ResetRoster()
        {
            if (_model == null) return false;

            _model.Reset(Time.time, _removed, _added);
            _gameOverRequested = false;

            for (int i = 0; i < _removed.Count; i++)
            {
                EventBus.Raise(new AudienceDeparted(
                    _removed[i],
                    AudienceDepartureReason.Reset));
            }

            for (int i = 0; i < _added.Count; i++)
            {
                EventBus.Raise(new AudienceJoined(
                    _added[i],
                    AudienceJoinReason.Initialization));
            }

            RaiseSummary();
            return true;
        }

        public bool TryGet(AudienceId id, out AudienceSnapshot audience)
        {
            if (_model != null) return _model.TryGet(id, out audience);
            audience = default;
            return false;
        }

        public bool TryAddRandom(
            AudienceJoinReason reason,
            out AudienceSnapshot audience)
        {
            audience = default;
            if (_model == null || !_model.TryAddRandom(Time.time, out audience))
                return false;

            EventBus.Raise(new AudienceJoined(audience, reason));
            RaiseSummary();
            return true;
        }

        public bool TryAdd(
            CrowdPreference preference,
            float engagement,
            AudienceJoinReason reason,
            out AudienceSnapshot audience)
        {
            audience = default;
            if (_model == null ||
                !_model.TryAdd(preference, engagement, Time.time, out audience))
                return false;

            EventBus.Raise(new AudienceJoined(audience, reason));
            RaiseSummary();
            return true;
        }

        public bool TryAdd(
            CrowdPreference preference,
            AudienceJoinReason reason,
            out AudienceSnapshot audience)
        {
            audience = default;
            if (_model == null ||
                !_model.TryAdd(preference, Time.time, out audience))
                return false;

            EventBus.Raise(new AudienceJoined(audience, reason));
            RaiseSummary();
            return true;
        }

        public bool TryChangeEngagement(
            AudienceId id,
            float delta,
            AudienceChangeReason reason,
            out AudienceSnapshot current)
        {
            if (IsFeverActive && delta < 0f)
                return TryGet(id, out current);

            if (_model == null ||
                !_model.TryChangeEngagement(id, delta, out AudienceStateChange change, out bool departed))
            {
                current = default;
                return false;
            }

            current = change.Current;
            PublishStateMutation(change, reason, departed);
            return true;
        }

        public bool TrySetEngagement(
            AudienceId id,
            float engagement,
            AudienceChangeReason reason,
            out AudienceSnapshot current)
        {
            if (IsFeverActive &&
                _model != null &&
                _model.TryGet(id, out AudienceSnapshot previous) &&
                engagement < previous.Engagement)
            {
                current = previous;
                return true;
            }

            if (_model == null ||
                !_model.TrySetEngagement(
                    id,
                    engagement,
                    out AudienceStateChange change,
                    out bool departed))
            {
                current = default;
                return false;
            }

            current = change.Current;
            PublishStateMutation(change, reason, departed);
            return true;
        }

        public bool TryApplyCardReaction(
            AudienceId id,
            string cardId,
            int reactionValue,
            float cardEngagementMultiplier,
            out AudienceSnapshot current)
        {
            current = default;
            if (_model == null || !_model.TryGet(id, out AudienceSnapshot previous))
                return false;

            float multiplier = Mathf.Max(0f, cardEngagementMultiplier);
            float engagementDelta =
                reactionValue *
                _model.EngagementRules.EngagementPerReactionPoint *
                multiplier;
            if (IsFeverActive && engagementDelta < 0f)
                engagementDelta = 0f;

            float appliedDelta =
                _model.EngagementRules.Clamp(previous.Engagement + engagementDelta) -
                previous.Engagement;

            if (Mathf.Approximately(appliedDelta, 0f))
            {
                current = previous;
                EventBus.Raise(new AudienceCardReacted(
                    cardId ?? string.Empty,
                    reactionValue,
                    0f,
                    previous,
                    current));
                return true;
            }

            if (!_model.TryChangeEngagement(
                    id,
                    appliedDelta,
                    out AudienceStateChange change,
                    out bool departed))
            {
                current = previous;
                return false;
            }

            current = change.Current;
            EventBus.Raise(new AudienceCardReacted(
                cardId ?? string.Empty,
                reactionValue,
                current.Engagement - previous.Engagement,
                previous,
                current));
            PublishStateMutation(change, AudienceChangeReason.CardReaction, departed);
            return true;
        }

        public int ApplyFeverEngagementPulse(float engagementGain)
        {
            if (_model == null || !IsFeverActive || engagementGain <= 0f)
                return 0;

            int affectedCount = 0;
            for (int i = _model.Members.Count - 1; i >= 0; i--)
            {
                AudienceId id = _model.Members[i].Id;
                if (TryChangeEngagement(
                        id,
                        engagementGain,
                        AudienceChangeReason.CardReaction,
                        out _))
                {
                    affectedCount++;
                }
            }

            return affectedCount;
        }

        public bool TryRemove(
            AudienceId id,
            AudienceDepartureReason reason,
            out AudienceSnapshot removed)
        {
            removed = default;
            if (_model == null || !_model.TryRemove(id, out removed))
                return false;

            EventBus.Raise(new AudienceDeparted(removed, reason));
            RaiseSummary();
            RequestGameOverIfEmpty();
            return true;
        }

        void PublishStateMutation(
            AudienceStateChange change,
            AudienceChangeReason reason,
            bool departed)
        {
            if (departed)
            {
                EventBus.Raise(new AudienceDeparted(
                    change.Current,
                    AudienceDepartureReason.EngagementDepleted));
            }
            else
            {
                EventBus.Raise(new AudienceStateChanged(change, reason));
            }

            RaiseSummary();
            RequestGameOverIfEmpty();
        }

        void RequestGameOverIfEmpty()
        {
            if (_gameOverRequested || _model == null || _model.Count > 0)
                return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying)
                return;

            _gameOverRequested = true;
            GameManager.Instance.GameOver();
        }

        void RaiseSummary() =>
            EventBus.Raise(new AudienceSummaryChanged(_model.CreateSummary()));

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
                ResetRoster();
            else if (e.Previous == GameState.Ready &&
                     e.Current == GameState.Playing &&
                     Count == 0)
                ResetRoster();
        }

        bool TryCreateModel()
        {
            if (engagementConfig == null)
            {
                Debug.LogError(
                    "[AudienceRoster] AudienceEngagementConfig is required.",
                    this);
                return false;
            }

            if (flowConfig == null)
            {
                Debug.LogError("[AudienceRoster] AudienceFlowConfig is required.", this);
                return false;
            }

            if (!engagementConfig.TryValidate(out string engagementError))
            {
                Debug.LogError(
                    $"[AudienceRoster] Invalid engagement config: {engagementError}",
                    engagementConfig);
                return false;
            }

            if (!flowConfig.TryValidate(out string flowError))
            {
                Debug.LogError(
                    $"[AudienceRoster] Invalid flow config: {flowError}",
                    flowConfig);
                return false;
            }

            _model = new AudienceRosterModel(
                engagementConfig.CreateRules(),
                flowConfig.CreateRules(AugmentRuntime.Current.InitialAudienceBonus));
            return true;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (engagementConfig == null)
                Debug.LogWarning(
                    "[AudienceRoster] AudienceEngagementConfig is not assigned.",
                    this);
            if (flowConfig == null)
                Debug.LogWarning("[AudienceRoster] AudienceFlowConfig is not assigned.", this);
        }
#endif
    }

    public static class AudienceRoster
    {
        static readonly AudienceSnapshot[] EmptyMembers = Array.Empty<AudienceSnapshot>();

        public static bool Exists => AudienceRosterSystem.HasInstance;
        public static int Count =>
            AudienceRosterSystem.HasInstance ? AudienceRosterSystem.Instance.Count : 0;
        public static IReadOnlyList<AudienceSnapshot> Members =>
            AudienceRosterSystem.HasInstance
                ? AudienceRosterSystem.Instance.Members
                : EmptyMembers;
        public static AudienceSummary Summary =>
            AudienceRosterSystem.HasInstance
                ? AudienceRosterSystem.Instance.Summary
                : default;

        public static bool TryGet(AudienceId id, out AudienceSnapshot audience)
        {
            if (AudienceRosterSystem.HasInstance)
                return AudienceRosterSystem.Instance.TryGet(id, out audience);

            audience = default;
            return false;
        }
    }
}
