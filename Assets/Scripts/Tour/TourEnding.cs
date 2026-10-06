using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>엔딩 종류. ID 는 DialogueCatalog 의 시퀀스 ID 와 같다.</summary>
    public enum TourEndingKind
    {
        /// <summary>투어 실패 (공연장에서 쫓겨남)</summary>
        Bad,
        /// <summary>전 스테이지 S 랭크</summary>
        AllS,
        /// <summary>Mosh 관객이 가장 많은 점수를 줌</summary>
        Mosh,
        /// <summary>Singalong 관객이 가장 많은 점수를 줌</summary>
        Singalong,
        /// <summary>Chill 관객이 가장 많은 점수를 줌</summary>
        Chill,
    }

    /// <summary>판정된 엔딩과 그 근거. 결말 화면과 대화 재생이 이 값만 본다.</summary>
    public sealed class TourEndingVerdict
    {
        public TourEndingKind kind;
        /// <summary>DialogueCatalog 시퀀스 ID (배드 엔딩은 합류 전이면 ending_bad_before_join)</summary>
        public string sequenceId;
        public bool failed;
        /// <summary>실패한 노드 순번 (0부터). 성공이면 -1</summary>
        public int failedNodeIndex = -1;
        public bool allS;
        public CrowdPreference topPreference;
        public int topPreferenceScore;
        public int[] scoreByPreference = new int[PerformanceReport.PreferenceCount];
        public int[] cardsByPreference = new int[PerformanceReport.PreferenceCount];
        public int totalScore;
        public int clearedCount;
        public string ranks = "";
    }

    /// <summary>
    /// 엔딩 판정. 확정된 투어 상태(StageResult + PerformanceReport)만 읽는다.
    ///
    ///   1. 투어 실패 → Bad. 스컹크가 합류하기 전(첫 노드에서 실패)이면 ending_bad_before_join, 그 뒤면 ending_bad
    ///   2. 성공 + 모든 공연 S 랭크 → AllS
    ///   3. 그 외 → 투어 전체에서 가장 많은 반응 점수를 준 관객 성향 (Mosh / Singalong / Chill)
    ///      동률이면 그 성향 카드 사용 횟수, 그래도 같으면 Mosh &gt; Singalong &gt; Chill
    ///
    /// 보스전 패배는 목표 점수 미달 = 공연 실패이므로 1번(Bad)으로 간다.
    /// </summary>
    public static class TourEndingSelector
    {
        public const string BadBeforeJoinId = "ending_bad_before_join";

        /// <summary>스컹크가 합류하는 노드 순번(0부터). 이 노드 이전에 실패하면 합류 전 배드 엔딩.</summary>
        public const int SkunkJoinNodeIndex = 1;

        public static TourEndingVerdict Resolve(TourRunState run)
        {
            var v = new TourEndingVerdict();
            if (run == null) { v.kind = TourEndingKind.Bad; v.failed = true; v.sequenceId = SequenceIdFor(TourEndingKind.Bad); return v; }

            v.totalScore = run.totalScore;
            var ranks = new List<string>();
            bool allS = true;
            for (int i = 0; run.stageResults != null && i < run.stageResults.Count; i++)
            {
                StageResult r = run.stageResults[i];
                if (r == null) continue;
                if (r.cleared) v.clearedCount++;
                ranks.Add(string.IsNullOrEmpty(r.rank) ? "F" : r.rank);
                if (!r.cleared || r.rank != "S") allS = false;
                if (r.report == null) continue;
                for (int p = 0; p < PerformanceReport.PreferenceCount; p++)
                {
                    v.scoreByPreference[p] += r.report.ScoreFor((CrowdPreference)p);
                    v.cardsByPreference[p] += r.report.CardsFor((CrowdPreference)p);
                }
            }
            v.ranks = string.Join(" ", ranks);
            PerformanceReport.TopPreference(v.scoreByPreference, v.cardsByPreference, out v.topPreference, out v.topPreferenceScore);

            v.failed = run.phase == RunPhase.Failed || FindFailedNodeIndex(run, out v.failedNodeIndex);
            if (v.failed)
            {
                if (v.failedNodeIndex < 0) FindFailedNodeIndex(run, out v.failedNodeIndex);
                v.kind = TourEndingKind.Bad;
                v.sequenceId = v.failedNodeIndex >= 0 && v.failedNodeIndex < SkunkJoinNodeIndex
                    ? BadBeforeJoinId
                    : SequenceIdFor(TourEndingKind.Bad);
                return v;
            }

            v.allS = allS && ranks.Count > 0;
            if (v.allS)
            {
                v.kind = TourEndingKind.AllS;
            }
            else
            {
                switch (v.topPreference)
                {
                    case CrowdPreference.Chill: v.kind = TourEndingKind.Chill; break;
                    case CrowdPreference.Singalong: v.kind = TourEndingKind.Singalong; break;
                    default: v.kind = TourEndingKind.Mosh; break;
                }
            }
            v.sequenceId = SequenceIdFor(v.kind);
            return v;
        }

        public static string SequenceIdFor(TourEndingKind kind)
        {
            switch (kind)
            {
                case TourEndingKind.AllS: return "ending_all_s";
                case TourEndingKind.Mosh: return "ending_mosh";
                case TourEndingKind.Singalong: return "ending_singalong";
                case TourEndingKind.Chill: return "ending_chill";
                default: return "ending_bad";
            }
        }

        static bool FindFailedNodeIndex(TourRunState run, out int index)
        {
            index = -1;
            if (run?.map?.nodes == null) return false;
            for (int i = 0; i < run.map.nodes.Count; i++)
            {
                if (run.map.nodes[i] != null && run.map.nodes[i].status == RunNodeStatus.Failed)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }
    }

    // 엔딩별 표시 자료는 TourEndingCatalog.cs (ScriptableObject 는 파일 이름과 클래스 이름이 같아야 한다)
}
