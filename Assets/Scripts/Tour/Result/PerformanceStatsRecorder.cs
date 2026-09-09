using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 기록 수집. 공연(Playing) 동안 EventBus 로 통계를 모으고, 종료 시점에 <see cref="Freeze"/> 로 확정한다.
    /// 판정(성공/실패·랭크)은 여기서 하지 않는다 — 랭크는 확정 점수와 공통 랭크 기준으로 <see cref="ComputeRank"/> 가 계산한다.
    /// TourPerformanceBridge 가 Awake 에서 붙여 두므로 씬 배치는 필요 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PerformanceStatsRecorder : MonoBehaviour
    {
        [Header("현재 집계 (읽기 전용)")]
        [SerializeField] int maxCombo;
        [SerializeField] int feverCount;
        [SerializeField] int audiencePeak;
        [SerializeField] int specialSuccess;
        [SerializeField] int specialEnded;
        [SerializeField] int crisisCount;
        [SerializeField] int crisisThreatened;
        [SerializeField] int crisisRetained;
        [SerializeField] BossOutcome bossOutcome;

        bool _feverActive;

        public int MaxCombo => maxCombo;
        public int FeverCount => feverCount;

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Subscribe<AudienceSummaryChanged>(OnAudienceSummary);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Subscribe<SpecialAudienceEnded>(OnSpecialEnded);
            EventBus.Subscribe<AudienceCrisisResolved>(OnCrisisResolved);
            EventBus.Subscribe<BossDefeated>(OnBossDefeated);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Unsubscribe<AudienceSummaryChanged>(OnAudienceSummary);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Unsubscribe<SpecialAudienceEnded>(OnSpecialEnded);
            EventBus.Unsubscribe<AudienceCrisisResolved>(OnCrisisResolved);
            EventBus.Unsubscribe<BossDefeated>(OnBossDefeated);
        }

        /// <summary>공연 시작(Ready) 마다 초기화. 종료·초기화 전에 Freeze 를 먼저 부른다.</summary>
        public void ResetStats()
        {
            maxCombo = 0;
            feverCount = 0;
            audiencePeak = 0;
            specialSuccess = 0;
            specialEnded = 0;
            crisisCount = 0;
            crisisThreatened = 0;
            crisisRetained = 0;
            bossOutcome = BossOutcome.None;
            _feverActive = false;
        }

        // ---------------- 수집 ----------------

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready) ResetStats();
        }

        void OnComboChanged(ComboChanged e) => maxCombo = Mathf.Max(maxCombo, e.CurrentCombo);

        void OnFeverChanged(FeverStateChanged e)
        {
            // 연장으로 true 가 반복 발행될 수 있으므로 false → true 전환만 센다
            if (e.IsActive && !_feverActive) feverCount++;
            _feverActive = e.IsActive;
        }

        void OnAudienceSummary(AudienceSummaryChanged e) => audiencePeak = Mathf.Max(audiencePeak, e.Summary.Count);

        void OnSpecialHit(SpecialHitLanded e) => specialSuccess++;

        void OnSpecialEnded(SpecialAudienceEnded e)
        {
            // 디버그 교체·공연 종료로 끊긴 요청은 "종료된 요청" 으로 세지 않는다
            if (e.Reason == SpecialAudienceEndReason.SpecialHit || e.Reason == SpecialAudienceEndReason.Expired)
                specialEnded++;
        }

        void OnCrisisResolved(AudienceCrisisResolved e)
        {
            crisisCount++;
            crisisThreatened += e.ThreatenedCount;
            crisisRetained += e.RetainedCount;
        }

        void OnBossDefeated(BossDefeated e) => bossOutcome = e.ByStreak ? BossOutcome.StreakWin : BossOutcome.Defeated;

        // ---------------- 확정 ----------------

        /// <summary>종료 시점 값(남은 관객·보스 통계·목표 점수)을 더해 기록을 확정한다.</summary>
        public PerformanceReport Freeze(StageDefinition stage)
        {
            var report = new PerformanceReport
            {
                targetScore = PerformanceTimer.TargetScore,
                duration = PerformanceTimer.Duration,
                isBoss = stage != null && stage.IsBoss,
                maxCombo = maxCombo,
                feverCount = feverCount,
                audiencePeak = audiencePeak,
                specialSuccess = specialSuccess,
                specialEnded = specialEnded,
                crisisCount = crisisCount,
                crisisThreatened = crisisThreatened,
                crisisRetained = crisisRetained,
            };

            if (AudienceRosterSystem.HasInstance)
            {
                AudienceRosterSystem roster = AudienceRosterSystem.Instance;
                report.audienceRemaining = roster.Count;
                report.audienceCapacity = roster.Capacity;
                report.audiencePeak = Mathf.Max(report.audiencePeak, roster.Count);
            }

            BuskingWalkInRule busking = FindFirstObjectByType<BuskingWalkInRule>();
            if (busking != null && busking.IsActive) report.walkIns = busking.WalkInCount;

            BossBattleRule boss = FindFirstObjectByType<BossBattleRule>();
            if (boss != null && boss.IsActive)
            {
                report.isBoss = true;
                report.bossPatternsSucceeded = boss.PatternsSucceeded;
                report.bossPatternsResolved = boss.PatternsResolved;
                report.bossFansRecruited = boss.FansRecruited;
                report.bossFansLost = boss.FansLost;
                report.bossOutcome = bossOutcome != BossOutcome.None
                    ? bossOutcome
                    : (boss.IsDefeated ? BossOutcome.Defeated : BossOutcome.TimeOut);
            }

            return report;
        }

        // ---------------- 랭크 ----------------

        static ScoreRankUI.RankTier[] s_tiers;

        /// <summary>확정 점수와 공통 랭크 기준(ScoreRankUI 와 동일)으로 랭크 라벨을 계산한다.</summary>
        public static string ComputeRank(int score, int targetScore)
        {
            if (s_tiers == null)
            {
                s_tiers = new[]
                {
                    new ScoreRankUI.RankTier { label = "F", minRatio = 0f },
                    new ScoreRankUI.RankTier { label = "D", minRatio = 1f },
                    new ScoreRankUI.RankTier { label = "C", minRatio = 1.6f },
                    new ScoreRankUI.RankTier { label = "B", minRatio = 2.4f },
                    new ScoreRankUI.RankTier { label = "A", minRatio = 3.6f },
                    new ScoreRankUI.RankTier { label = "S", minRatio = 5f },
                };
                ScoreRankUI.ApplyCurrentBalance(s_tiers);
            }

            float ratio = targetScore > 0 ? (float)score / targetScore : 0f;
            int index = ScoreRankUI.GetRankIndex(ratio, s_tiers);
            return index >= 0 && index < s_tiers.Length ? s_tiers[index].label : "F";
        }
    }
}
