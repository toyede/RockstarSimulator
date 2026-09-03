using System;
using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Boss — 라이벌의 관객 쟁탈 (rival_crowd_challenge). (기획서 §9)
    ///
    /// 정해진 시각(30/65/100초)에 라이벌이 특정 팬층을 노린다:
    ///   1차 현재 인원이 가장 많은 성향 · 2차 평균 호응도가 가장 낮은 성향 · 3차 앞과 겹치지 않는 성향
    /// 공격 7초 전에 성향을 공개하고 그 성향 중 호응도가 낮은 관객 최대 2명을 위협 대상으로 지정한다.
    /// 방어: 대상 호응도 60 이상, 또는 경고 중 해당 성향 Special Hit(남은 대상 전원 안전).
    /// 실패: 안전하지 못한 대상만 라이벌 무대 쪽으로 퇴장. 공격당 최대 이탈 2명.
    ///
    /// 관객 외곽선·이탈 연출·경고 오버레이는 기존 AudienceCrisis* 이벤트를 그대로 발행해 재사용하고,
    /// 보스 전용 문구는 RivalAttackWarningStarted 를 구독하는 RivalAttackNoticeUI 가 그린다.
    /// 점수·UI·증강은 직접 건드리지 않는다.
    /// </summary>
    public sealed class RivalCrowdChallengeRule : StageRuleBehaviour
    {
        public enum TargetMode
        {
            MostPopulated,
            LowestAverageEngagement,
            LeastUsedPreference
        }

        [Serializable]
        public struct AttackPlan
        {
            [Tooltip("공연 경과 시간(초). 이 시각에 이탈이 확정되고, warningDuration 만큼 앞서 경고가 뜬다")]
            [Min(0f)] public float triggerTime;
            public TargetMode targetMode;
        }

        enum Phase
        {
            Idle,
            Warning,
            Resolving,
            Done
        }

        struct Target
        {
            public AudienceId Id;
            public bool Shielded;
            public bool DepartedDuringWarning;
        }

        [Header("라이벌")]
        [SerializeField, Tooltip("경고 문구에 쓰는 라이벌 이름 (가칭). 시스템 ID 에는 쓰지 않는다")]
        string rivalName = "LUX//FAUNA";

        [Header("공격 계획")]
        [SerializeField]
        List<AttackPlan> attacks = new List<AttackPlan>
        {
            new AttackPlan { triggerTime = 30f, targetMode = TargetMode.MostPopulated },
            new AttackPlan { triggerTime = 65f, targetMode = TargetMode.LowestAverageEngagement },
            new AttackPlan { triggerTime = 100f, targetMode = TargetMode.LeastUsedPreference },
        };

        [SerializeField, Min(0.5f), Tooltip("공격 몇 초 전에 성향을 공개할지")]
        float warningDuration = 7f;

        [SerializeField, Min(1), Tooltip("한 공격이 위협하는 최대 인원")]
        int maxTargetsPerAttack = 2;

        [SerializeField, Min(1), Tooltip("공격 후에도 반드시 남겨 둘 최소 관객 수")]
        int minimumSurvivorCount = 1;

        [Header("방어")]
        [SerializeField, Min(0f), Tooltip("이 호응도 이상이면 대상이 스스로 남는다")]
        float safeEngagement = 60f;

        [Header("연출")]
        [SerializeField, Min(0f)] float departureStagger = 0.08f;
        [SerializeField, Min(0f)] float departureSettleDuration = 0.55f;
        [SerializeField, Min(0f), Tooltip("공격 직후 카드 입력을 잠그는 시간(초). 튜토리얼이 입력을 잡고 있으면 건너뛴다")]
        float inputLockAfterAttack = 1f;

        [Header("통계 (읽기 전용)")]
        [SerializeField] int totalStolen;
        [SerializeField] int totalDefended;
        [SerializeField] int attacksResolved;

        readonly List<Target> _targets = new List<Target>(4);
        readonly List<CrowdPreference> _usedPreferences = new List<CrowdPreference>(3);
        readonly List<AudienceId> _departureBuffer = new List<AudienceId>(4);
        readonly int[] _counts = new int[3];
        readonly float[] _engagementSums = new float[3];

        Phase _phase = Phase.Idle;
        int _attackIndex;
        float _remaining;
        CrowdPreference _targetPreference;
        bool _specialDefended;
        Coroutine _resolveRoutine;
        Coroutine _inputLockRoutine;
        System.Random _random;

        public int TotalStolen => totalStolen;
        public int TotalDefended => totalDefended;
        public int AttacksResolved => attacksResolved;
        public bool IsWarning => _phase == Phase.Warning;
        public CrowdPreference CurrentTargetPreference => _targetPreference;
        public float WarningRemaining => _remaining;

        // ---------------- 룰 수명 ----------------

        protected override void OnActivate(StageRuleContext context)
        {
            ResetState();
            _random = new System.Random(unchecked(context.RunSeed ^ 0x5A17B055));
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<AudienceDeparted>(OnAudienceDeparted);
        }

        protected override void OnDeactivate()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<AudienceDeparted>(OnAudienceDeparted);
            CancelAttack(notify: true);
            ReleaseInputLock();
        }

        void ResetState()
        {
            _phase = Phase.Idle;
            _attackIndex = 0;
            _remaining = 0f;
            _specialDefended = false;
            _targets.Clear();
            _usedPreferences.Clear();
            _departureBuffer.Clear();
            totalStolen = 0;
            totalDefended = 0;
            attacksResolved = 0;
        }

        void Update()
        {
            if (!IsActive || _phase == Phase.Done || _phase == Phase.Resolving) return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;
            if (Context.AudienceRoster == null) return;

            if (_phase == Phase.Idle)
            {
                if (_attackIndex >= attacks.Count)
                {
                    _phase = Phase.Done;
                    return;
                }

                float warnAt = Mathf.Max(0f, attacks[_attackIndex].triggerTime - warningDuration);
                if (PerformanceTimer.Elapsed >= warnAt) StartWarning();
                return;
            }

            // Warning
            _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
            RaiseProgress();
            if (_remaining <= 0f) _resolveRoutine = StartCoroutine(ResolveAttack());
        }

        // ---------------- 경고 ----------------

        void StartWarning()
        {
            AttackPlan plan = attacks[_attackIndex];
            if (!TryChoosePreference(plan.targetMode, out CrowdPreference preference) ||
                !BuildTargets(preference))
            {
                // 노릴 관객이 없으면 이번 공격은 조용히 지나간다 (관객이 거의 없는 상황)
                _attackIndex++;
                return;
            }

            _targetPreference = preference;
            _specialDefended = false;
            _remaining = warningDuration;
            _phase = Phase.Warning;

            AudienceId[] ids = CopyTargetIds();
            EventBus.Raise(new RivalAttackWarningStarted(
                _attackIndex,
                preference,
                ids,
                warningDuration,
                safeEngagement,
                rivalName));
            EventBus.Raise(new AudienceCrisisWarningStarted(ids, warningDuration, safeEngagement));
            EventBus.Raise(new AudienceCrisisTargetsChanged(ids, true));
            RaiseProgress();
        }

        bool TryChoosePreference(TargetMode mode, out CrowdPreference preference)
        {
            preference = CrowdPreference.Chill;
            if (!CountMembers()) return false;

            switch (mode)
            {
                case TargetMode.MostPopulated:
                    return TryPickMostPopulated(null, out preference);

                case TargetMode.LowestAverageEngagement:
                    return TryPickLowestAverage(out preference);

                case TargetMode.LeastUsedPreference:
                    if (TryPickMostPopulated(_usedPreferences, out preference)) return true;
                    return TryPickLowestAverage(out preference);
            }

            return false;
        }

        /// <summary>현재 관객을 성향별로 센다. 관객이 하나도 없으면 false.</summary>
        bool CountMembers()
        {
            Array.Clear(_counts, 0, _counts.Length);
            Array.Clear(_engagementSums, 0, _engagementSums.Length);

            IReadOnlyList<AudienceSnapshot> members = Context.AudienceRoster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                int index = (int)members[i].Preference;
                if (index < 0 || index >= _counts.Length) continue;
                _counts[index]++;
                _engagementSums[index] += members[i].Engagement;
            }

            return members.Count > 0;
        }

        bool TryPickMostPopulated(List<CrowdPreference> exclude, out CrowdPreference preference)
        {
            int best = -1;
            var tied = new List<int>(3);
            for (int i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] <= 0) continue;
                if (exclude != null && exclude.Contains((CrowdPreference)i)) continue;

                if (_counts[i] > best)
                {
                    best = _counts[i];
                    tied.Clear();
                    tied.Add(i);
                }
                else if (_counts[i] == best)
                {
                    tied.Add(i);
                }
            }

            if (tied.Count == 0)
            {
                preference = CrowdPreference.Chill;
                return false;
            }

            preference = (CrowdPreference)tied[_random.Next(tied.Count)];
            return true;
        }

        bool TryPickLowestAverage(out CrowdPreference preference)
        {
            float best = float.PositiveInfinity;
            var tied = new List<int>(3);
            for (int i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] <= 0) continue;
                float average = _engagementSums[i] / _counts[i];
                if (average < best - 0.001f)
                {
                    best = average;
                    tied.Clear();
                    tied.Add(i);
                }
                else if (Mathf.Abs(average - best) <= 0.001f)
                {
                    tied.Add(i);
                }
            }

            if (tied.Count == 0)
            {
                preference = CrowdPreference.Chill;
                return false;
            }

            preference = (CrowdPreference)tied[_random.Next(tied.Count)];
            return true;
        }

        bool BuildTargets(CrowdPreference preference)
        {
            _targets.Clear();

            IReadOnlyList<AudienceSnapshot> members = Context.AudienceRoster.Members;
            var candidates = new List<AudienceSnapshot>(members.Count);
            for (int i = 0; i < members.Count; i++)
                if (members[i].Preference == preference) candidates.Add(members[i]);

            candidates.Sort((left, right) =>
            {
                int byEngagement = left.Engagement.CompareTo(right.Engagement);
                return byEngagement != 0 ? byEngagement : left.Id.Value.CompareTo(right.Id.Value);
            });

            int removable = Mathf.Max(0, members.Count - minimumSurvivorCount);
            int count = Mathf.Min(maxTargetsPerAttack, Mathf.Min(candidates.Count, removable));
            for (int i = 0; i < count; i++)
                _targets.Add(new Target { Id = candidates[i].Id });

            return _targets.Count > 0;
        }

        // ---------------- 방어 판정 ----------------

        void OnCardResolved(CardResolved e)
        {
            if (_phase != Phase.Warning) return;
            if (!e.IsSpecialHit || e.Role != CardRole.Special) return;
            if (e.TargetPreference != _targetPreference) return;

            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                target.Shielded = true;
                _targets[i] = target;
            }

            _specialDefended = true;
            RaiseProgress();
        }

        void OnAudienceDeparted(AudienceDeparted e)
        {
            if (_phase != Phase.Warning) return;

            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].Id != e.Audience.Id) continue;
                Target target = _targets[i];
                target.DepartedDuringWarning = true;
                _targets[i] = target;
                break;
            }

            RaiseProgress();
        }

        bool IsSecured(Target target)
        {
            if (target.Shielded) return true;
            if (target.DepartedDuringWarning) return false;
            return Context.AudienceRoster.TryGet(target.Id, out AudienceSnapshot audience) &&
                   audience.Engagement >= safeEngagement;
        }

        int CountSecured()
        {
            int secured = 0;
            for (int i = 0; i < _targets.Count; i++)
                if (IsSecured(_targets[i])) secured++;
            return secured;
        }

        void RaiseProgress()
        {
            EventBus.Raise(new AudienceCrisisProgressChanged(
                _remaining,
                _targets.Count,
                CountSecured(),
                _specialDefended ? _targets.Count : 0));
        }

        // ---------------- 결과 ----------------

        IEnumerator ResolveAttack()
        {
            _phase = Phase.Resolving;
            _departureBuffer.Clear();

            int departedCount = 0;
            for (int i = 0; i < _targets.Count; i++)
            {
                Target target = _targets[i];
                if (target.DepartedDuringWarning)
                {
                    departedCount++;
                    continue;
                }
                if (IsSecured(target)) continue;
                _departureBuffer.Add(target.Id);
            }

            int maximumDeparture = Mathf.Max(0, Context.AudienceRoster.Count - minimumSurvivorCount);
            if (_departureBuffer.Count > maximumDeparture)
                _departureBuffer.RemoveRange(maximumDeparture, _departureBuffer.Count - maximumDeparture);

            EventBus.Raise(new AudienceCrisisDepartureStarted(departedCount + _departureBuffer.Count));
            EventBus.Raise(new AudienceCrisisTargetsChanged(CopyTargetIds(), false));

            BeginInputLock();

            for (int i = 0; i < _departureBuffer.Count; i++)
            {
                if (Context.AudienceRoster.TryRemove(
                        _departureBuffer[i],
                        AudienceDepartureReason.NearbyConcert,
                        out _))
                    departedCount++;

                if (departureStagger > 0f && i < _departureBuffer.Count - 1)
                    yield return new WaitForSeconds(departureStagger);
            }

            int retained = Mathf.Max(0, _targets.Count - departedCount);
            totalStolen += departedCount;
            totalDefended += retained;
            attacksResolved++;

            EventBus.Raise(new AudienceCrisisResolved(_targets.Count, retained, departedCount));
            EventBus.Raise(new RivalAttackResolved(
                _attackIndex,
                _targetPreference,
                _targets.Count,
                retained,
                departedCount,
                _specialDefended));

            if (departureSettleDuration > 0f)
                yield return new WaitForSeconds(departureSettleDuration);

            EventBus.Raise(new AudienceCrisisDepartureEnded());

            _usedPreferences.Add(_targetPreference);
            _targets.Clear();
            _departureBuffer.Clear();
            _resolveRoutine = null;
            _attackIndex++;
            _phase = _attackIndex >= attacks.Count ? Phase.Done : Phase.Idle;
        }

        void CancelAttack(bool notify)
        {
            bool wasActive = _phase == Phase.Warning || _phase == Phase.Resolving;
            if (_resolveRoutine != null)
            {
                StopCoroutine(_resolveRoutine);
                _resolveRoutine = null;
            }

            if (wasActive)
            {
                EventBus.Raise(new AudienceCrisisTargetsChanged(CopyTargetIds(), false));
                EventBus.Raise(new AudienceCrisisDepartureEnded());
                if (notify) EventBus.Raise(new AudienceCrisisCancelled());
            }

            _targets.Clear();
            _departureBuffer.Clear();
            _remaining = 0f;
            _phase = Phase.Idle;
        }

        AudienceId[] CopyTargetIds()
        {
            var ids = new AudienceId[_targets.Count];
            for (int i = 0; i < _targets.Count; i++) ids[i] = _targets[i].Id;
            return ids;
        }

        // ---------------- 입력 잠금 ----------------

        void BeginInputLock()
        {
            if (inputLockAfterAttack <= 0f) return;
            if (CardInput.UseFilter != null) return; // 튜토리얼 등 다른 주인이 있으면 건드리지 않는다

            ReleaseInputLock();
            CardInput.UseFilter = BlockAllCards;
            _inputLockRoutine = StartCoroutine(InputLockRoutine());
        }

        IEnumerator InputLockRoutine()
        {
            yield return new WaitForSecondsRealtime(inputLockAfterAttack);
            _inputLockRoutine = null;
            ReleaseInputLock();
        }

        void ReleaseInputLock()
        {
            if (_inputLockRoutine != null)
            {
                StopCoroutine(_inputLockRoutine);
                _inputLockRoutine = null;
            }

            if (CardInput.UseFilter == (Func<int, bool>)BlockAllCards) CardInput.UseFilter = null;
        }

        static bool BlockAllCards(int handIndex) => false;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (attacks == null) return;
            for (int i = 0; i < attacks.Count; i++)
            {
                AttackPlan plan = attacks[i];
                plan.triggerTime = Mathf.Max(0f, plan.triggerTime);
                attacks[i] = plan;
            }
        }

        [ContextMenu("Debug/Force Next Attack Now")]
        void DebugForceNextAttack()
        {
            if (!Application.isPlaying || !IsActive || _phase != Phase.Idle) return;
            if (_attackIndex >= attacks.Count) return;
            StartWarning();
        }
#endif
    }
}
