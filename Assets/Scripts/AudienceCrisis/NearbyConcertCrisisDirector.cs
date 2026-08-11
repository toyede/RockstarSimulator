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
        float _nextAdaptiveCheckAt;
        float _highPerformanceSustain;
        float _lowPerformanceSustain;

        public AudienceCrisisState State => _state;
        public bool IsWarning => _state == AudienceCrisisState.Warning;
        public float WarningRemaining => _warningRemaining;
        public bool CanForceEvent =>
            isActiveAndEnabled &&
            GameManager.HasInstance &&
            GameManager.Instance.IsPlaying &&
            _state != AudienceCrisisState.Warning &&
            _state != AudienceCrisisState.Resolving;

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
            if (TryForceStartCrisis()) return;

            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying)
            {
                Debug.LogWarning(
                    "[AudienceCrisis] Forced crisis requires Playing state.",
                    this);
                return;
            }

            Debug.LogWarning(
                "[AudienceCrisis] Crisis is already active or no removable " +
                "audience member is available.",
                this);
        }

        public bool TryForceStartCrisis()
        {
            if (!CanForceEvent) return false;
            _wasPlaying = true;
            return TryStartCrisis(true);
        }

        [ContextMenu("Debug/Force Audience Comeback")]
        public void ForceStartComeback()
        {
            if (TryForceStartComeback()) return;
            Debug.LogWarning(
                "[AudienceCrisis] Comeback requires Playing state and no " +
                "active crisis.",
                this);
        }

        public bool TryForceStartComeback()
        {
            if (!CanForceEvent || audienceRoster == null) return false;
            _wasPlaying = true;
            _state = AudienceCrisisState.Resolving;
            _resolutionRoutine = StartCoroutine(ResolveComeback());
            return true;
        }

        void UpdateArmed()
        {
            if (config.UseAdaptiveTrigger)
            {
                UpdateAdaptiveArmed();
                return;
            }

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

            _nextAdaptiveCheckAt = 0f;
            _highPerformanceSustain = 0f;
            _lowPerformanceSustain = 0f;

            float duration = PerformanceTimer.Duration;
            if (duration <= 0f)
            {
                _state = AudienceCrisisState.Completed;
                return;
            }

            if (config.UseAdaptiveTrigger)
            {
                _state = AudienceCrisisState.Armed;
                return;
            }

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

            float ratio = Mathf.Lerp(
                config.EarliestPerformanceRatio,
                config.LatestPerformanceRatio,
                (float)random.NextDouble());
            _triggerTime = duration * ratio;
            _latestTriggerTime =
                duration * config.LatestPerformanceRatio;
            _state = AudienceCrisisState.Armed;
        }

        void UpdateAdaptiveArmed()
        {
            float normalized = PerformanceTimer.Normalized;
            if (normalized < config.AdaptiveEvaluationStartRatio)
                return;

            if (normalized > config.AdaptiveEvaluationEndRatio)
            {
                _state = AudienceCrisisState.Completed;
                return;
            }

            if (TutorialFlow.IsRunning ||
                (FeverSystem.HasInstance && FeverSystem.Instance.IsActive))
            {
                ResetAdaptiveSustain();
                return;
            }

            if (Time.unscaledTime < _nextAdaptiveCheckAt) return;
            _nextAdaptiveCheckAt = Time.unscaledTime + config.AdaptiveCheckInterval;

            int targetScore = PerformanceTimer.TargetScore;
            if (targetScore <= 0 || !GameManager.HasInstance)
            {
                _state = AudienceCrisisState.Completed;
                return;
            }

            float expectedScore = Mathf.Max(1f, targetScore * normalized);
            float pace = GameManager.Instance.Score / expectedScore;
            float averageEngagement = ResolveAverageEngagement();
            float step = config.AdaptiveCheckInterval;

            bool highPerformance =
                pace >= config.HighPerformancePace &&
                averageEngagement >= config.HighPerformanceEngagement;
            bool lowPerformance = pace <= config.LowPerformancePace;

            _highPerformanceSustain = highPerformance
                ? _highPerformanceSustain + step
                : 0f;
            _lowPerformanceSustain = lowPerformance
                ? _lowPerformanceSustain + step
                : 0f;

            if (_highPerformanceSustain >= config.AdaptiveSustainDuration)
            {
                StartAdaptiveOutcome(preferCrisis: true);
                return;
            }

            if (_lowPerformanceSustain >= config.AdaptiveSustainDuration)
            {
                StartAdaptiveOutcome(preferCrisis: false);
                return;
            }

            // 중간 성적대에서는 high/low 조건이 끝까지 성립하지 않아 이벤트가
            // 전혀 보이지 않았다. 중반 이후에는 현재 페이스를 기준으로 한 번 확정한다.
            if (normalized >= config.AdaptiveFallbackRatio)
                StartAdaptiveOutcome(pace >= config.FallbackCrisisPace);
        }

        void StartAdaptiveOutcome(bool preferCrisis)
        {
            if (preferCrisis && TryStartCrisis(false))
                return;

            // 관객 수가 위기 이벤트 최소 인원보다 적어도 이벤트를 없애지 않는다.
            // 이 경우에는 관객 지원으로 전환해 한 판에 적어도 한 번은 변화를 준다.
            _state = AudienceCrisisState.Resolving;
            _resolutionRoutine = StartCoroutine(ResolveComeback());
        }

        IEnumerator ResolveComeback()
        {
            EventBus.Raise(new AudienceComebackStarted(config.ComebackWarningDuration));
            if (config.ComebackWarningDuration > 0f)
                yield return new WaitForSecondsRealtime(config.ComebackWarningDuration);

            int joinedCount = 0;
            for (int i = 0; i < config.ComebackAudienceCount; i++)
            {
                CrowdPreference preference = ResolveLeastRepresentedPreference();
                if (!audienceRoster.TryAdd(
                        preference,
                        config.ComebackAudienceEngagement,
                        AudienceJoinReason.RuntimeCommand,
                        out _))
                    break;
                joinedCount++;
            }

            _departureBuffer.Clear();
            IReadOnlyList<AudienceSnapshot> members = audienceRoster.Members;
            for (int i = 0; i < members.Count; i++)
                _departureBuffer.Add(members[i].Id);

            int boostedCount = 0;
            for (int i = 0; i < _departureBuffer.Count; i++)
            {
                if (audienceRoster.TryChangeEngagement(
                        _departureBuffer[i],
                        config.ComebackEngagementBoost,
                        AudienceChangeReason.RuntimeCommand,
                        out _))
                    boostedCount++;
            }

            EventBus.Raise(new AudienceComebackResolved(joinedCount, boostedCount));
            _departureBuffer.Clear();
            _resolutionRoutine = null;
            _state = AudienceCrisisState.Completed;
        }

        CrowdPreference ResolveLeastRepresentedPreference()
        {
            int chill = 0;
            int singalong = 0;
            int mosh = 0;
            IReadOnlyList<AudienceSnapshot> members = audienceRoster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                switch (members[i].Preference)
                {
                    case CrowdPreference.Chill: chill++; break;
                    case CrowdPreference.Singalong: singalong++; break;
                    case CrowdPreference.Mosh: mosh++; break;
                }
            }

            if (chill <= singalong && chill <= mosh) return CrowdPreference.Chill;
            return singalong <= mosh ? CrowdPreference.Singalong : CrowdPreference.Mosh;
        }

        float ResolveAverageEngagement()
        {
            IReadOnlyList<AudienceSnapshot> members = audienceRoster.Members;
            if (members.Count == 0) return 0f;

            float total = 0f;
            for (int i = 0; i < members.Count; i++)
                total += members[i].Engagement;
            return total / members.Count;
        }

        void ResetAdaptiveSustain()
        {
            _highPerformanceSustain = 0f;
            _lowPerformanceSustain = 0f;
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
            ResetAdaptiveSustain();
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
            if (audienceRoster == null)
            {
                Debug.LogError(
                    "[AudienceCrisis] Audience roster reference is required.",
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
