using System;
using UnityEngine;

namespace ContextStage
{
    /// <summary>보스전 결말. 보스 시스템이 확정한 값을 그대로 담는다 (결과창은 다시 계산하지 않는다).</summary>
    public enum BossOutcome
    {
        None = 0,
        /// <summary>체력 0 으로 격파</summary>
        Defeated = 1,
        /// <summary>연속 패턴 성공으로 승리</summary>
        StreakWin = 2,
        /// <summary>제한 시간 종료 — 꺾지 못함</summary>
        TimeOut = 3,
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

        public bool HasSpecialAudience => specialEnded > 0;
        public bool HasCrisis => crisisCount > 0;
        public bool HasStageEvents => stageEventsTotal > 0;
        public bool HasBoss => isBoss && bossOutcome != BossOutcome.None;

        /// <summary>목표 대비 달성 비율 (1.0 = 목표 정확히 달성).</summary>
        public float AchievedRatio(int score) => targetScore > 0 ? (float)score / targetScore : 0f;
    }
}
