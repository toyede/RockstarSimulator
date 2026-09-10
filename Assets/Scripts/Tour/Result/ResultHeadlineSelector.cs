using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>다음 진행 버튼이 가리키는 곳.</summary>
    public enum ResultNextAction
    {
        /// <summary>일반 스테이지 성공 → 증강 선택</summary>
        Augment,
        /// <summary>최종 스테이지 성공 → 투어 결말</summary>
        Ending,
        /// <summary>실패 → 투어 결과 정리</summary>
        Failed,
    }

    /// <summary>결과창이 표시할 내용 전부. 기사 선택(ResultHeadlineSelector)이 만들고 결과 화면(ResultNewspaperView)은 표시만 한다.</summary>
    public sealed class ResultPresentation
    {
        public ResultNewspaperCatalog.Skin skin;
        public string mastheadInfo;     // 공연장 · 스테이지 번호
        public string headline;
        public string subtitle;
        public Sprite photoBackground;
        public IReadOnlyList<Sprite> bandFrames;
        public float bandFrameInterval = 0.4f;
        public float bandSpriteScale = 3f;
        public string caption;

        public bool success;
        public bool isBoss;
        public string stampText;
        public Color stampColor;
        public bool showRank;
        public string rankLabel;
        public Sprite rankIcon;
        public int score;
        public int targetScore;
        public int achievedPercent;

        public string statCombo;
        public string statFever;
        public string statAudience;

        public string articleTitle;
        public string articleBody;
        public string outcomeLine;      // 보스전만

        public ResultNextAction nextAction;
        public string buttonLabel;
    }

    /// <summary>
    /// 기사 선택. 확정 기록(StageResult + PerformanceReport)으로 헤드라인·부제·사진·대표 기록·버튼 문구를 고른다.
    ///
    ///   1. 일반 공연인지 보스전인지 → 2. 실제 성공·실패(result.cleared, 보스는 룰이 확정한 값) →
    ///   3. 성공이면 등급별 문구 → 4. 특징적인 기록 하나만 부제로.
    /// 보스전은 점수 등급이 낮아도 승리했으면 승리 기사이며, 등급 대신 도장과 점수만 보여준다.
    /// </summary>
    public static class ResultHeadlineSelector
    {
        public static ResultPresentation Compose(
            StageResult result,
            StageDefinition stage,
            int stageNumber,
            ResultNextAction nextAction,
            ResultNewspaperCatalog catalog)
        {
            PerformanceReport report = result.report ?? new PerformanceReport { targetScore = stage != null ? stage.TargetScore : 0 };
            bool isBoss = report.isBoss || (stage != null && stage.IsBoss);
            bool success = result.cleared;
            string rank = string.IsNullOrEmpty(result.rank) ? "F" : result.rank;
            int target = report.targetScore > 0 ? report.targetScore : (stage != null ? stage.TargetScore : 0);
            int percent = target > 0 ? Mathf.RoundToInt(100f * result.score / target) : 0;

            var p = new ResultPresentation
            {
                skin = catalog != null ? catalog.ResolveSkin(result.stageId) : null,
                mastheadInfo = stage != null ? $"{stage.DisplayName}\nSTAGE {stageNumber}" : $"STAGE {stageNumber}",
                success = success,
                isBoss = isBoss,
                score = result.score,
                targetScore = target,
                achievedPercent = percent,
                showRank = !isBoss,
                rankLabel = rank,
                rankIcon = catalog != null ? catalog.RankIconFor(rank) : null,
                nextAction = nextAction,
                bandSpriteScale = catalog != null ? catalog.BandSpriteScale : 3f,
                bandFrameInterval = catalog != null ? catalog.BandFrameInterval : 0.4f,
            };

            if (StageVisualCatalog.TryResolve(result.stageId, out StageVisualEntry visual) && visual != null)
                p.photoBackground = visual.backgroundBase;

            if (catalog == null)
            {
                p.headline = success ? "공연 성공" : "공연 실패";
                p.subtitle = "";
                p.caption = "";
                p.stampText = success ? "SUCCESS" : "FAILED";
                p.stampColor = Color.black;
                p.buttonLabel = "CONTINUE";
                p.statCombo = $"최고 콤보  {report.maxCombo}";
                p.statFever = $"피버  {report.feverCount}회";
                p.statAudience = $"남은 관객  {report.audienceRemaining} / {report.audienceCapacity}";
                return p;
            }

            Color accent = p.skin != null ? p.skin.accent : new Color32(0xB3, 0x38, 0x31, 0xFF);

            // ---------------- 헤드라인 · 도장 · 사진 ----------------
            if (isBoss)
            {
                p.headline = success ? catalog.HeadlineBossWin : catalog.HeadlineBossLose;
                p.stampText = success ? catalog.StampBossWin : catalog.StampBossLose;
                p.caption = success ? catalog.CaptionBossWin : catalog.CaptionBossLose;
                if (!success) p.subtitle = string.Format(catalog.BossLoseSubtitle, report.bossPatternsSucceeded);
                else if (report.bossFansRecruited > 0) p.subtitle = string.Format(catalog.BossWinSubtitle, report.bossFansRecruited);
                else p.subtitle = string.Format(catalog.BossWinSubtitleNoRecruit, report.bossPatternsSucceeded);
                p.outcomeLine = OutcomeLine(report.bossOutcome, success, catalog);
            }
            else if (success)
            {
                p.headline = catalog.HeadlineForRank(rank);
                p.stampText = catalog.StampSuccess;
                p.caption = rank == "S" || rank == "A" ? catalog.CaptionHigh : catalog.CaptionNormal;
                p.subtitle = PickRecordSubtitle(report, percent, catalog);
            }
            else
            {
                p.headline = catalog.HeadlineFail;
                p.stampText = catalog.StampFail;
                p.caption = catalog.CaptionFail;
                int shortfall = Mathf.Max(0, target - result.score);
                // 실패해도 잘한 기록이 있으면 부제에 같이 보여준다
                string record = PickRecordSubtitle(report, percent, catalog, allowDefault: false);
                p.subtitle = record ?? string.Format(catalog.FailSubtitle, shortfall.ToString("N0"));
            }

            p.stampColor = success ? accent : catalog.StampFailColor;
            p.bandFrames = success ? catalog.BandClearFrames : catalog.BandFailFrames;

            // ---------------- 요약 기록 (라벨 / 값 두 줄) ----------------
            p.statCombo = Stat("최고 콤보", report.maxCombo.ToString());
            p.statFever = Stat("피버", $"{report.feverCount}회");
            p.statAudience = report.audienceCapacity > 0
                ? Stat("남은 관객 / 정원", $"{report.audienceRemaining} / {report.audienceCapacity}")
                : Stat("남은 관객", report.audienceRemaining.ToString());

            // ---------------- 기믹 기사 (스테이지에 맞는 것 하나) ----------------
            if (isBoss && report.HasBoss)
            {
                p.articleTitle = catalog.ArticleBossTitle;
                p.articleBody = string.Format(catalog.ArticleBossBody,
                    report.bossPatternsSucceeded, report.bossPatternsResolved, report.bossFansRecruited, report.bossFansLost);
            }
            else if (report.HasCrisis)
            {
                p.articleTitle = catalog.ArticleCrisisTitle;
                p.articleBody = string.Format(catalog.ArticleCrisisBody, report.crisisRetained, report.crisisThreatened);
            }
            else if (report.HasSpecialAudience)
            {
                p.articleTitle = catalog.ArticleSpecialTitle;
                p.articleBody = string.Format(catalog.ArticleSpecialBody, report.specialSuccess, report.specialEnded);
            }
            else if (report.walkIns > 0)
            {
                p.articleTitle = catalog.ArticleBuskingTitle;
                p.articleBody = string.Format(catalog.ArticleBuskingBody, report.walkIns);
            }
            else
            {
                p.articleTitle = catalog.ArticleDefaultTitle;
                p.articleBody = string.Format(catalog.ArticleDefaultBody, report.audiencePeak);
            }
            if (report.HasStageEvents)
                p.articleBody += $" · 기믹 이벤트 {report.stageEventsSucceeded} / {report.stageEventsTotal}회 성공";

            // ---------------- 버튼 ----------------
            switch (nextAction)
            {
                case ResultNextAction.Ending: p.buttonLabel = catalog.ButtonEnding; break;
                case ResultNextAction.Failed: p.buttonLabel = catalog.ButtonFailed; break;
                default: p.buttonLabel = catalog.ButtonAugment; break;
            }

            return p;
        }

        /// <summary>통계 칸 한 줄: 작은 라벨 위, 큰 값 아래. 폰트를 줄이지 않고 두 줄로 칸에 맞춘다.</summary>
        static string Stat(string label, string value) => $"<size=22>{label}</size>\n<size=34>{value}</size>";

        /// <summary>대표 기록 하나만 고른다. 우선순위: 콤보 → 피버 → 특별 관객 → 위기 방어 → (기본) 달성률.</summary>
        static string PickRecordSubtitle(PerformanceReport r, int percent, ResultNewspaperCatalog c, bool allowDefault = true)
        {
            if (r.maxCombo >= c.ComboSubtitleMin) return string.Format(c.ComboSubtitle, r.maxCombo);
            if (r.feverCount >= c.FeverSubtitleMin) return string.Format(c.FeverSubtitle, r.feverCount);
            if (r.specialSuccess >= c.SpecialSubtitleMin) return string.Format(c.SpecialSubtitle, r.specialSuccess);
            if (r.crisisRetained >= c.CrisisSubtitleMin) return string.Format(c.CrisisSubtitle, r.crisisRetained);
            return allowDefault ? string.Format(c.DefaultSubtitle, percent) : null;
        }

        static string OutcomeLine(BossOutcome outcome, bool success, ResultNewspaperCatalog c)
        {
            switch (outcome)
            {
                case BossOutcome.Defeated: return c.OutcomeDefeated;
                case BossOutcome.StreakWin: return c.OutcomeStreak;
                case BossOutcome.TimeOut: return c.OutcomeTimeOut;
                default: return success ? c.OutcomeDefeated : c.OutcomeTimeOut;
            }
        }
    }
}
