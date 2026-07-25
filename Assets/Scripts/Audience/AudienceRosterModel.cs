using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Scene-independent authority for audience identity and engagement.
    /// Unity lifecycle, events, and game-over decisions stay in AudienceRosterSystem.
    /// </summary>
    public sealed class AudienceRosterModel
    {
        readonly AudienceEngagementRules _engagementRules;
        readonly AudienceFlowRules _flowRules;
        readonly List<AudienceSnapshot> _members;

        System.Random _random;
        // 유입 판정 전용 난수. _random과 공유하면 판정 횟수만큼 성향·초기 몰입도
        // 추첨 시퀀스가 밀려 시드 재현성이 깨지므로 별도로 둔다.
        System.Random _arrivalRandom;
        int _nextId;
        float _arrivalTimer;

        public AudienceRosterModel(
            AudienceEngagementRules engagementRules,
            AudienceFlowRules flowRules)
        {
            if (!engagementRules.TryValidate(out string engagementError))
                throw new ArgumentException(engagementError, nameof(engagementRules));
            if (!flowRules.TryValidate(out string flowError))
                throw new ArgumentException(flowError, nameof(flowRules));

            _engagementRules = engagementRules;
            _flowRules = flowRules;
            _members = new List<AudienceSnapshot>(flowRules.MaximumAudienceCount);
            ResetRandom();
        }

        public IReadOnlyList<AudienceSnapshot> Members => _members;
        public int Count => _members.Count;
        public int Capacity => _flowRules.MaximumAudienceCount;
        public bool IsFull => Count >= Capacity;
        public AudienceEngagementRules EngagementRules => _engagementRules;
        public AudienceFlowRules FlowRules => _flowRules;
        public float SecondsUntilArrivalCheck =>
            Mathf.Max(0f, _flowRules.ArrivalCheckInterval - _arrivalTimer);

        public void Reset(
            float joinedAt,
            List<AudienceSnapshot> removed,
            List<AudienceSnapshot> added)
        {
            removed?.Clear();
            added?.Clear();

            if (removed != null) removed.AddRange(_members);
            _members.Clear();
            ResetRandom();

            for (int i = 0; i < _flowRules.InitialAudienceCount; i++)
            {
                if (TryAddRandom(joinedAt, out AudienceSnapshot audience))
                    added?.Add(audience);
            }
        }

        public bool TryAddRandom(float joinedAt, out AudienceSnapshot audience)
        {
            if (IsFull)
            {
                audience = default;
                return false;
            }

            return TryAdd(
                _flowRules.DrawPreference(_random),
                _engagementRules.DrawInitialEngagement(_random),
                joinedAt,
                out audience);
        }

        public bool TryAdd(
            CrowdPreference preference,
            float engagement,
            float joinedAt,
            out AudienceSnapshot audience)
        {
            if (IsFull)
            {
                audience = default;
                return false;
            }

            float clamped = _engagementRules.Clamp(engagement);
            if (clamped <= 0f)
            {
                audience = default;
                return false;
            }

            audience = new AudienceSnapshot(
                new AudienceId(_nextId++),
                preference,
                clamped,
                _engagementRules.ResolveStage(clamped),
                Mathf.Max(0f, joinedAt));
            _members.Add(audience);
            return true;
        }

        public bool TryGet(AudienceId id, out AudienceSnapshot audience)
        {
            int index = FindIndex(id);
            if (index >= 0)
            {
                audience = _members[index];
                return true;
            }

            audience = default;
            return false;
        }

        public bool TryChangeEngagement(
            AudienceId id,
            float delta,
            out AudienceStateChange change,
            out bool departed)
        {
            int index = FindIndex(id);
            if (index < 0)
            {
                change = default;
                departed = false;
                return false;
            }

            return SetEngagementAt(index, _members[index].Engagement + delta, out change, out departed);
        }

        public bool TrySetEngagement(
            AudienceId id,
            float engagement,
            out AudienceStateChange change,
            out bool departed)
        {
            int index = FindIndex(id);
            if (index < 0)
            {
                change = default;
                departed = false;
                return false;
            }

            return SetEngagementAt(index, engagement, out change, out departed);
        }

        public bool TryRemove(AudienceId id, out AudienceSnapshot removed)
        {
            int index = FindIndex(id);
            if (index < 0)
            {
                removed = default;
                return false;
            }

            removed = _members[index];
            _members.RemoveAt(index);
            return true;
        }

        public bool ApplyNaturalDecay(
            float deltaTime,
            List<AudienceStateChange> changes,
            List<AudienceSnapshot> departed)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            if (departed == null) throw new ArgumentNullException(nameof(departed));

            changes.Clear();
            departed.Clear();

            float decay = _engagementRules.NaturalDecayPerSecond * Mathf.Max(0f, deltaTime);
            if (decay <= 0f || _members.Count == 0) return false;

            for (int i = _members.Count - 1; i >= 0; i--)
            {
                if (!SetEngagementAt(
                        i,
                        _members[i].Engagement - decay,
                        out AudienceStateChange change,
                        out bool wasRemoved))
                    continue;

                if (wasRemoved) departed.Add(change.Current);
                else changes.Add(change);
            }

            return changes.Count > 0 || departed.Count > 0;
        }

        /// <summary>
        /// 일정 주기마다 신규 관객 유입 여부를 확률로 판정한다. (기획서 §4.2)
        /// </summary>
        public bool TryTickArrival(float deltaTime, float joinedAt, out AudienceSnapshot audience)
        {
            audience = default;

            // 0이면 유입 기능 자체를 끈 것으로 취급한다.
            if (_flowRules.ArrivalCheckInterval <= 0f || _flowRules.ArrivalChance <= 0f)
                return false;

            // 만석이면 타이머를 쌓지 않는다. 자리가 나는 순간부터 최대 1주기만 기다리면 된다.
            if (IsFull) return false;

            _arrivalTimer += Mathf.Max(0f, deltaTime);
            if (_arrivalTimer < _flowRules.ArrivalCheckInterval) return false;

            // 프레임 드랍으로 여러 주기가 몰려도 이번 프레임엔 한 번만 판정한다.
            _arrivalTimer -= _flowRules.ArrivalCheckInterval;

            if (_arrivalRandom.NextDouble() >= _flowRules.ArrivalChance) return false;

            return TryAddRandom(joinedAt, out audience);
        }

        public AudienceSummary CreateSummary()
        {
            if (_members.Count == 0) return default;

            float totalEngagement = 0f;
            int calm = 0;
            int middle = 0;
            int excited = 0;

            for (int i = 0; i < _members.Count; i++)
            {
                AudienceSnapshot member = _members[i];
                totalEngagement += member.Engagement;
                switch (member.Stage)
                {
                    case AudienceEngagementStage.Calm: calm++; break;
                    case AudienceEngagementStage.Middle: middle++; break;
                    case AudienceEngagementStage.Excited: excited++; break;
                }
            }

            return new AudienceSummary(
                _members.Count,
                totalEngagement / _members.Count,
                calm,
                middle,
                excited);
        }

        bool SetEngagementAt(
            int index,
            float engagement,
            out AudienceStateChange change,
            out bool departed)
        {
            AudienceSnapshot previous = _members[index];
            float clamped = _engagementRules.Clamp(engagement);
            AudienceSnapshot current = previous.WithEngagement(
                clamped,
                _engagementRules.ResolveStage(clamped));

            if (Mathf.Approximately(previous.Engagement, current.Engagement))
            {
                change = default;
                departed = false;
                return false;
            }

            change = new AudienceStateChange(previous, current);
            departed = clamped <= 0f;
            if (departed) _members.RemoveAt(index);
            else _members[index] = current;
            return true;
        }

        int FindIndex(AudienceId id)
        {
            if (!id.IsValid) return -1;

            // The design caps this collection at ten members. Linear lookup avoids
            // maintaining a second mutable index when members enter and leave.
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i].Id == id) return i;
            }

            return -1;
        }

        void ResetRandom()
        {
            _random = new System.Random(_flowRules.RandomSeed);
            _arrivalRandom = new System.Random(_flowRules.RandomSeed + 1);
            _nextId = 1;
            _arrivalTimer = 0f;
        }
    }
}
