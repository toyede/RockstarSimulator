using System;
using UnityEngine;

namespace ContextStage
{
    /// <summary>보스전 결말. 보스 시스템이 확정한 값을 그대로 담는다 (결과창은 다시 계산하지 않는다).</summary>
    public enum BossOutcome
    {
        None = 0,
        /// <summary>(예전 체력제) 체력 0 으로 격파</summary>
        Defeated = 1,
        /// <summary>(예전 체력제) 연속 패턴 성공으로 승리</summary>
        StreakWin = 2,
        /// <summary>(예전 체력제) 제한 시간 종료 — 꺾지 못함</summary>
        TimeOut = 3,
        /// <summary>시간 종료 시 점수 ≥ 목표 — 대결 승리</summary>
        Won = 4,
        /// <summary>시간 종료 시 점수 &lt; 목표 — 대결 패배</summary>
        Lost = 5,
    }

    /// <summary>
    /// 한 공연의 확정 기록. 공연 중 PerformanceStatsRecorder 가 모으고 종료 시점에 고정한다.
    /// StageResult.report 로 투어 런에 저장되어 결과창(신문)과 이후 통계에 쓰인다.
    /// </summary>
    [Serializable]
    public sealed class PerformanceReport
    {
        [Header("공연")]
        public int targetScore;
        public float duration;
        public bool isBoss;

        [Header("주요 기록")]
        public int maxCombo;
        public int feverCount;

        [Header("관객")]
        public int audienceRemaining;
        public int audienceCapacity;
        public int audiencePeak;
        public int walkIns;

        [Header("특별 관객 (요청 성공 / 종료된 요청)")]
        public int specialSuccess;
        public int specialEnded;

        [Header("위기 이벤트 (발생 횟수 · 대상 · 지킨 수)")]
        public int crisisCount;
        public int crisisThreatened;
        public int crisisRetained;

        [Header("기믹 이벤트 (성공 / 전체)")]
        public int stageEventsSucceeded;
        public int stageEventsTotal;

        [Header("보스전")]
        public BossOutcome bossOutcome;
        public int bossPatternsSucceeded;
        public int bossPatternsResolved;
        public int bossFansRecruited;
        public int bossFansLost;
        public int bossDrainTotal;

        [Header("성향별 (인덱스 = CrowdPreference: Chill · Singalong · Mosh)")]
        [Tooltip("관객 반응값 합계. 카드 한 장에 반응한 관객 각각의 반응값을 그 관객의 성향에 더한다 (음수 포함)")]
        public int[] scoreByPreference = new int[PreferenceCount];
        [Tooltip("그 성향을 겨냥한 공연 카드를 낸 횟수 (유틸리티 카드 제외)")]
        public int[] cardsByPreference = new int[PreferenceCount];

        public const int PreferenceCount = 3;

        public bool HasSpecialAudience => specialEnded > 0;

        public int ScoreFor(CrowdPreference p) => scoreByPreference != null && (int)p < scoreByPreference.Length ? scoreByPreference[(int)p] : 0;
        public int CardsFor(CrowdPreference p) => cardsByPreference != null && (int)p < cardsByPreference.Length ? cardsByPreference[(int)p] : 0;

        /// <summary>가장 많은 반응 점수를 준 성향. 동률이면 카드 사용 횟수, 그래도 같으면 Mosh &gt; Singalong &gt; Chill.</summary>
        public bool TryGetTopPreference(out CrowdPreference top, out int score)
        {
            return TopPreference(scoreByPreference, cardsByPreference, out top, out score);
        }

        public static bool TopPreference(int[] scores, int[] cards, out CrowdPreference top, out int score)
        {
            top = CrowdPreference.Mosh;
            score = 0;
            if (scores == null) return false;
            bool any = false;
            int bestCards = 0;
            for (int i = scores.Length - 1; i >= 0; i--) // 뒤(Mosh)부터 돌아 동률에서 Mosh 우선
            {
                int cardCount = cards != null && i < cards.Length ? cards[i] : 0;
                if (!any || scores[i] > score || (scores[i] == score && cardCount > bestCards))
                {
                    any = true;
                    top = (CrowdPreference)i;
                    score = scores[i];
                    bestCards = cardCount;
                }
            }
            return any && (score != 0 || bestCards > 0);
        }
        public bool HasCrisis => crisisCount > 0;
        public bool HasStageEvents => stageEventsTotal > 0;
        public bool HasBoss => isBoss && bossOutcome != BossOutcome.None;

        /// <summary>목표 대비 달성 비율 (1.0 = 목표 정확히 달성).</summary>
        public float AchievedRatio(int score) => targetScore > 0 ? (float)score / targetScore : 0f;
    }
}
