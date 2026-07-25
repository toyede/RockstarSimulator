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
        public bool IsConfigured => _model != null;
        public AudienceSummary Summary =>
            _model != null ? _model.CreateSummary() : default;

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

            if (!_model.ApplyNaturalDecay(Time.deltaTime, _changes, _removed))
                return;

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

            RaiseSummary();
            RequestGameOverIfEmpty();
        }

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

        public bool TryChangeEngagement(
            AudienceId id,
            float delta,
            AudienceChangeReason reason,
            out AudienceSnapshot current)
        {
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

            int clampedReaction = Mathf.Max(0, reactionValue);
            float multiplier = Mathf.Max(0f, cardEngagementMultiplier);
            float engagementDelta =
                clampedReaction *
                _model.EngagementRules.EngagementPerReactionPoint *
                multiplier;
            float appliedDelta =
                _model.EngagementRules.Clamp(previous.Engagement + engagementDelta) -
                previous.Engagement;

            if (appliedDelta <= 0f)
            {
                current = previous;
                EventBus.Raise(new AudienceCardReacted(
                    cardId ?? string.Empty,
                    clampedReaction,
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
                clampedReaction,
                current.Engagement - previous.Engagement,
                previous,
                current));
            PublishStateMutation(change, AudienceChangeReason.CardReaction, departed);
            return true;
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
                flowConfig.CreateRules());
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
