using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class NearbyConcertCrisisDirector : MonoBehaviour
    {
        [SerializeField] AudienceCrisisConfig config;
        [SerializeField] AudienceRosterSystem audienceRoster;

        readonly List<CrisisTarget> _targets = new List<CrisisTarget>(4);
        readonly List<AudienceSnapshot> _candidateBuffer =
            new List<AudienceSnapshot>(12);
        readonly List<AudienceId> _departureBuffer =
            new List<AudienceId>(4);

        AudienceCrisisState _state;
        Coroutine _resolutionRoutine;
        float _triggerTime;
        float _latestTriggerTime;
        float _warningRemaining;
        bool _wasPlaying;
        int _runSequence;
        int _specialSaveCount;
        int _minimumSurvivorsForCurrentCrisis;

        public AudienceCrisisState State => _state;
        public bool IsWarning => _state == AudienceCrisisState.Warning;
        public float WarningRemaining => _warningRemaining;

        void Start()
        {
            if (!ValidateDependencies()) enabled = false;
        }

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<AudienceDeparted>(OnAudienceDeparted);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<AudienceDeparted>(OnAudienceDeparted);
            CancelCrisis(false);
        }

        void Update()
        {
            bool playing =
                GameManager.HasInstance && GameManager.Instance.IsPlaying;
            if (!playing)
            {
                _wasPlaying = false;
                return;
            }

            if (!_wasPlaying)
            {
                _wasPlaying = true;
                ArmForCurrentRun();
            }

            if (_state == AudienceCrisisState.Armed)
                UpdateArmed();
            else if (_state == AudienceCrisisState.Warning)
                UpdateWarning();
        }

        [ContextMenu("Debug/Force Nearby Concert Crisis")]
        public void ForceStartCrisis()
        {
            if (!GameManager.HasInstance ||
                !GameManager.Instance.IsPlaying)
            {
                Debug.LogWarning(
                    "[AudienceCrisis] Forced crisis requires Playing state.",
                    this);
                return;
            }

            _wasPlaying = true;
            if (!TryStartCrisis(true))
            {
                Debug.LogWarning(
                    "[AudienceCrisis] At least one removable audience member " +
                    "is required for a forced crisis.",
                    this);
            }
        }

        void UpdateArmed()
        {
            float elapsed = PerformanceTimer.Elapsed;
            if (elapsed < _triggerTime) return;

            if (audienceRoster.Count >= config.MinimumAudienceCount)
            {
                if (!TryStartCrisis(false))
                    _state = AudienceCrisisState.Completed;
                return;
            }

            if (elapsed >= _latestTriggerTime)
                _state = AudienceCrisisState.Completed;
        }

        void UpdateWarning()
        {
            _warningRemaining = Mathf.Max(
                0f,
                _warningRemaining - Time.deltaTime);
            RaiseProgress();
            if (_warningRemaining > 0f) return;

            _resolutionRoutine = StartCoroutine(ResolveCrisis());
        }

        void ArmForCurrentRun()
        {
            CancelCrisis(false);
            _runSequence++;

            int seed = unchecked(
                System.Environment.TickCount ^
                config.RandomSeed ^
                (GetInstanceID() * 397) ^
                _runSequence);
            var random = new System.Random(seed);
            if (random.NextDouble() > config.EncounterChance)
            {
                _state = AudienceCrisisState.Completed;
                return;
            }

            float duration = PerformanceTimer.Duration;
            if (duration <= 0f)
            {
                _state = AudienceCrisisState.Completed;
                return;
            }

            float ratio = Mathf.Lerp(
                config.EarliestPerformanceRatio,
                config.LatestPerformanceRatio,
                (float)random.NextDouble());
            _triggerTime = duration * ratio;
            _latestTriggerTime =
                duration * config.LatestPerformanceRatio;
            _state = AudienceCrisisState.Armed;
        }

        bool TryStartCrisis(bool ignoreMinimumAudience)
        {
            if (_state == AudienceCrisisState.Warning ||
                _state == AudienceCrisisState.Resolving ||
                audienceRoster == null)
                return false;

            int audienceCount = audienceRoster.Count;
            if (!ignoreMinimumAudience &&
                audienceCount < config.MinimumAudienceCount)
                return false;

            int survivorsToKeep = ignoreMinimumAudience
                ? 1
                : config.MinimumSurvivorCount;
            int removableCount = Mathf.Max(
                0,
                audienceCount - survivorsToKeep);
            int targetCount = Mathf.Min(
                config.MaximumThreatenedCount,
                Mathf.CeilToInt(
                    audienceCount * config.ThreatenedRatio),
                removableCount);
            if (targetCount <= 0) return false;

            BuildTargets(targetCount);
            if (_targets.Count == 0) return false;

            _specialSaveCount = 0;
            _minimumSurvivorsForCurrentCrisis = survivorsToKeep;
            _warningRemaining = config.WarningDuration;
            _state = AudienceCrisisState.Warning;

            AudienceId[] ids = CopyTargetIds();
            EventBus.Raise(new AudienceCrisisWarningStarted(
                ids,
                config.WarningDuration,
                config.RetentionEngagement));
            EventBus.Raise(
                new AudienceCrisisTargetsChanged(ids, true));
            RaiseProgress();
            return true;
        }

        void BuildTargets(int targetCount)
        {
            _targets.Clear();
            _candidateBuffer.Clear();

            IReadOnlyList<AudienceSnapshot> members =
                audienceRoster.Members;
            for (int i = 0; i < members.Count; i++)
                _candidateBuffer.Add(members[i]);

            _candidateBuffer.Sort(CompareCandidates);
            int count = Mathf.Min(
                targetCount,
                _candidateBuffer.Count);
            for (int i = 0; i < count; i++)
                _targets.Add(
                    new CrisisTarget(_candidateBuffer[i].Id));
        }

        IEnumerator ResolveCrisis()
        {
            _state = AudienceCrisisState.Resolving;
            _departureBuffer.Clear();
            int departedCount = 0;

            for (int i = 0; i < _targets.Count; i++)
            {
                CrisisTarget target = _targets[i];
                if (target.DepartedDuringWarning)
                {
                    departedCount++;
                    continue;
                }
                if (IsSecured(target)) continue;
                _departureBuffer.Add(target.Id);
            }

            int maximumDeparture = Mathf.Max(
                0,
                audienceRoster.Count -
                _minimumSurvivorsForCurrentCrisis);
            if (_departureBuffer.Count > maximumDeparture)
            {
                _departureBuffer.RemoveRange(
                    maximumDeparture,
                    _departureBuffer.Count - maximumDeparture);
            }

            int plannedDepartureCount =
                departedCount + _departureBuffer.Count;
            EventBus.Raise(
                new AudienceCrisisDepartureStarted(
                    plannedDepartureCount));
            EventBus.Raise(
                new AudienceCrisisTargetsChanged(
                    CopyTargetIds(),
                    false));

            for (int i = 0; i < _departureBuffer.Count; i++)
            {
                if (audienceRoster.TryRemove(
                        _departureBuffer[i],
                        AudienceDepartureReason.NearbyConcert,
                        out _))
                    departedCount++;

                if (config.DepartureStagger > 0f &&
                    i < _departureBuffer.Count - 1)
                {
                    yield return new WaitForSeconds(
                        config.DepartureStagger);
                }
            }

            int retainedCount = Mathf.Max(
                0,
                _targets.Count - departedCount);
            EventBus.Raise(new AudienceCrisisResolved(
                _targets.Count,
                retainedCount,
                departedCount));

            if (config.DepartureSettleDuration > 0f)
            {
                yield return new WaitForSeconds(
                    config.DepartureSettleDuration);
            }

            EventBus.Raise(new AudienceCrisisDepartureEnded());
            _targets.Clear();
            _departureBuffer.Clear();
            _resolutionRoutine = null;
            _state = AudienceCrisisState.Completed;
        }

        void OnCardResolved(CardResolved e)
        {
            if (_state != AudienceCrisisState.Warning ||
                e.Role != CardRole.Special ||
                e.GainedScore <= 0 ||
                e.Judgement == HypeJudgement.Miss ||
                e.Judgement == HypeJudgement.RiskMiss)
                return;

            int remainingSaves =
                config.SuccessfulSpecialSaveCount;
            while (remainingSaves-- > 0 &&
                   TryShieldLowestUnsecuredTarget())
            {
                _specialSaveCount++;
            }
            RaiseProgress();
        }

        void OnAudienceDeparted(AudienceDeparted e)
        {
            if (_state != AudienceCrisisState.Warning) return;

            for (int i = 0; i < _targets.Count; i++)
            {
                CrisisTarget target = _targets[i];
                if (target.Id != e.Audience.Id) continue;
                target.DepartedDuringWarning = true;
                _targets[i] = target;
                break;
            }

            RaiseProgress();
        }

        bool TryShieldLowestUnsecuredTarget()
        {
            int selectedIndex = -1;
            float selectedEngagement = float.MaxValue;

            for (int i = 0; i < _targets.Count; i++)
            {
                CrisisTarget target = _targets[i];
                if (target.Shielded ||
                    !audienceRoster.TryGet(
                        target.Id,
                        out AudienceSnapshot audience) ||
                    audience.Engagement >=
                    config.RetentionEngagement)
                    continue;

                if (audience.Engagement >= selectedEngagement)
                    continue;
                selectedIndex = i;
                selectedEngagement = audience.Engagement;
            }

            if (selectedIndex < 0) return false;
            CrisisTarget selected = _targets[selectedIndex];
            selected.Shielded = true;
            _targets[selectedIndex] = selected;
            return true;
        }

        bool IsSecured(CrisisTarget target)
        {
            return target.Shielded ||
                   (!target.DepartedDuringWarning &&
                    audienceRoster.TryGet(
                        target.Id,
                        out AudienceSnapshot audience) &&
                    audience.Engagement >=
                    config.RetentionEngagement);
        }

        void RaiseProgress()
        {
            EventBus.Raise(new AudienceCrisisProgressChanged(
                _warningRemaining,
                _targets.Count,
                CountSecuredTargets(),
                _specialSaveCount));
        }

        int CountSecuredTargets()
        {
            int secured = 0;
            for (int i = 0; i < _targets.Count; i++)
            {
                if (IsSecured(_targets[i])) secured++;
            }
            return secured;
        }

        AudienceId[] CopyTargetIds()
        {
            var ids = new AudienceId[_targets.Count];
            for (int i = 0; i < _targets.Count; i++)
                ids[i] = _targets[i].Id;
            return ids;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready ||
                e.Current == GameState.GameOver)
            {
                CancelCrisis(true);
                _wasPlaying = false;
            }
        }

        void CancelCrisis(bool notify)
        {
            bool wasActive =
                _state == AudienceCrisisState.Warning ||
                _state == AudienceCrisisState.Resolving;
            if (_resolutionRoutine != null)
            {
                StopCoroutine(_resolutionRoutine);
                _resolutionRoutine = null;
            }

            if (wasActive)
            {
                EventBus.Raise(
                    new AudienceCrisisTargetsChanged(
                        CopyTargetIds(),
                        false));
                EventBus.Raise(
                    new AudienceCrisisDepartureEnded());
                if (notify)
                    EventBus.Raise(
                        new AudienceCrisisCancelled());
            }

            _targets.Clear();
            _departureBuffer.Clear();
            _warningRemaining = 0f;
            _specialSaveCount = 0;
            _minimumSurvivorsForCurrentCrisis = 0;
            _state = AudienceCrisisState.Dormant;
        }

        bool ValidateDependencies()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[AudienceCrisis] Crisis config is required.",
                    this);
                return false;
            }
            if (!config.TryValidate(out string configError))
            {
                Debug.LogError(
                    $"[AudienceCrisis] Invalid config: {configError}",
                    config);
                return false;
            }
            if (audienceRoster == null ||
                !audienceRoster.IsConfigured)
            {
                Debug.LogError(
                    "[AudienceCrisis] Configured audience roster is required.",
                    this);
                return false;
            }
            return true;
        }

        static int CompareCandidates(
            AudienceSnapshot left,
            AudienceSnapshot right)
        {
            int engagement =
                left.Engagement.CompareTo(right.Engagement);
            if (engagement != 0) return engagement;
            return left.Id.Value.CompareTo(right.Id.Value);
        }

        struct CrisisTarget
        {
            public CrisisTarget(AudienceId id)
            {
                Id = id;
                Shielded = false;
                DepartedDuringWarning = false;
            }

            public AudienceId Id;
            public bool Shielded;
            public bool DepartedDuringWarning;
        }
    }
}
