using System.Text;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 4 「큐시트」 (arena_big_screen). 생방송 카메라 큐 — 40·80초에 성향이 순서대로 표시된다 (1회차 3개, 2회차 4개).
    /// 그 순서대로 카드를 쓰면 성공 (틀리면 처음부터). 성공 = 목표 점수의 bonusRatio 가산 + 몰입도 회복.
    /// 실패 = 몰입도가 가장 낮은 관객 1명 이탈 (Stage 4 부터는 벌칙이 있다).
    /// </summary>
    public sealed class BigScreenRule : StageEventRule
    {
        [Header("큐시트")]
        [SerializeField, Tooltip("회차별 순서 길이. 회차가 더 많으면 마지막 값을 쓴다")] int[] sequenceLengthsByOccurrence = { 3, 4 };
        [SerializeField, Range(0f, 1f), Tooltip("성공 시 목표 점수 대비 가산 점수")] float bonusRatio = 0.10f;
        [SerializeField, Min(0f), Tooltip("성공 시 전 관객 몰입도 회복")] float engagementGain = 5f;
        [SerializeField, Min(0), Tooltip("실패 시 떠나는 관객 수")] int failLeaveCount = 1;
        [SerializeField, Tooltip("틀린 카드를 내면 처음부터 다시")] bool resetOnMismatch = true;

        CrowdPreference[] _sequence = new CrowdPreference[0];
        int _progress;
        int _occurrence;
        System.Random _random;

        protected override string Instruction => $"카메라 큐! 큐시트 순서대로 카드를 쓰세요:  {SequenceLabel(0)}";
        protected override string ProgressText => $"{_progress}/{_sequence.Length}  다음 {(_progress < _sequence.Length ? PreferenceLabel(_sequence[_progress]) : "-")}";

        protected override void OnEventRuleActivate(StageRuleContext context)
        {
            _random = new System.Random(unchecked(context.RunSeed ^ 0x5C2EE));
            _occurrence = 0;
        }

        protected override void OnEventBegin()
        {
            int length = 3;
            if (sequenceLengthsByOccurrence != null && sequenceLengthsByOccurrence.Length > 0)
                length = sequenceLengthsByOccurrence[Mathf.Min(_occurrence, sequenceLengthsByOccurrence.Length - 1)];
            _occurrence++;
            _sequence = new CrowdPreference[Mathf.Clamp(length, 2, 6)];
            CrowdPreference last = (CrowdPreference)(-1);
            for (int i = 0; i < _sequence.Length; i++)
            {
                CrowdPreference next;
                do next = (CrowdPreference)_random.Next(3); while (next == last && i > 0);
                _sequence[i] = next;
                last = next;
            }
            _progress = 0;
        }

        protected override void OnCardResolved(CardResolved e)
        {
            if (!IsEventActive || _progress >= _sequence.Length) return;
            if (e.TargetPreference == _sequence[_progress]) _progress++;
            else if (resetOnMismatch) _progress = 0;
        }

        protected override bool CheckSuccess() => _progress >= _sequence.Length;

        protected override string OnEventEnd(bool success)
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (success)
            {
                int bonus = Mathf.RoundToInt(PerformanceTimer.TargetScore * bonusRatio);
                if (bonus > 0 && GameManager.HasInstance) GameManager.Instance.AddScore(bonus);
                if (roster != null) roster.ApplyFeverEngagementPulse(engagementGain);
                return $"카메라가 밴드를 잡았습니다! 보너스 {bonus:N0}점.";
            }

            int left = 0;
            for (int i = 0; i < failLeaveCount; i++)
                if (RemoveLowestEngagement(AudienceDepartureReason.RuntimeRemoval)) left++;
            return left > 0
                ? $"큐를 놓쳤습니다. 방송 사고에 실망한 관객 {left}명이 떠났습니다."
                : "큐를 놓쳤습니다.";
        }

        string SequenceLabel(int from)
        {
            var sb = new StringBuilder();
            for (int i = from; i < _sequence.Length; i++)
            {
                if (i > from) sb.Append(" → ");
                sb.Append(PreferenceLabel(_sequence[i]));
            }
            return sb.ToString();
        }
    }
}
