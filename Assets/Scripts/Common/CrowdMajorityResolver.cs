using System.Collections.Generic;

namespace ContextStage
{
    /// <summary>
    /// 객석에서 "가장 많은 성향"을 뽑는다.
    ///
    /// 조명·연출이 열기(HeatStage) 대신 관객 성향을 따라가야 할 때 쓴다.
    /// 동점 처리 규칙을 한 곳에 모아 두어, 여러 시스템이 서로 다른 답을 내지 않게 한다.
    ///
    /// 관객 목록 자체는 <see cref="AudienceRosterSystem"/> 이 소유한다 —
    /// 여기서는 개수만 받아 판정하므로 어떤 관객 시스템이든 붙일 수 있다.
    /// </summary>
    public static class CrowdMajorityResolver
    {
        /// <summary>열거 순서 고정. 우선순위 목록이 비어 있을 때의 최종 폴백이다.</summary>
        static readonly CrowdPreference[] AllPreferences =
        {
            CrowdPreference.Chill,
            CrowdPreference.Singalong,
            CrowdPreference.Mosh,
        };

        /// <summary>
        /// 현재 관객 명단에서 최다 성향을 구한다.
        /// </summary>
        /// <returns>관객이 한 명도 없으면 false. 이때 majority 는 keepOnTie 그대로다.</returns>
        public static bool TryResolveMajority(
            IReadOnlyList<AudienceSnapshot> members,
            IList<CrowdPreference> tieBreakPriority,
            CrowdPreference keepOnTie,
            out CrowdPreference majority)
        {
            majority = keepOnTie;
            if (members == null || members.Count == 0) return false;

            int chill = 0, singalong = 0, mosh = 0;
            for (int i = 0; i < members.Count; i++)
            {
                switch (members[i].Preference)
                {
                    case CrowdPreference.Chill: chill++; break;
                    case CrowdPreference.Singalong: singalong++; break;
                    case CrowdPreference.Mosh: mosh++; break;
                }
            }

            return TryResolveMajority(
                chill, singalong, mosh, tieBreakPriority, keepOnTie, out majority);
        }

        /// <summary>
        /// 성향별 인원수에서 최다 성향을 구한다.
        ///
        /// 동점이면 <paramref name="keepOnTie"/>(보통 현재 적용 중인 성향)를 그대로 유지한다.
        /// 그래야 5:5 근처에서 인원이 한 명씩 드나들 때 조명이 깜빡이지 않는다.
        /// 현재 성향이 최다가 아니면 <paramref name="tieBreakPriority"/> 순서로 고른다.
        /// </summary>
        public static bool TryResolveMajority(
            int chillCount,
            int singalongCount,
            int moshCount,
            IList<CrowdPreference> tieBreakPriority,
            CrowdPreference keepOnTie,
            out CrowdPreference majority)
        {
            majority = keepOnTie;
            if (chillCount + singalongCount + moshCount <= 0) return false;

            int topCount = chillCount;
            if (singalongCount > topCount) topCount = singalongCount;
            if (moshCount > topCount) topCount = moshCount;

            // 현재 성향이 이미 최다면 바꾸지 않는다 (동점 진동 방지)
            if (CountOf(keepOnTie, chillCount, singalongCount, moshCount) == topCount) return true;

            if (tieBreakPriority != null)
            {
                for (int i = 0; i < tieBreakPriority.Count; i++)
                {
                    if (CountOf(tieBreakPriority[i], chillCount, singalongCount, moshCount) != topCount)
                        continue;
                    majority = tieBreakPriority[i];
                    return true;
                }
            }

            for (int i = 0; i < AllPreferences.Length; i++)
            {
                if (CountOf(AllPreferences[i], chillCount, singalongCount, moshCount) != topCount)
                    continue;
                majority = AllPreferences[i];
                return true;
            }

            return false;
        }

        static int CountOf(CrowdPreference preference, int chill, int singalong, int mosh)
        {
            switch (preference)
            {
                case CrowdPreference.Singalong: return singalong;
                case CrowdPreference.Mosh: return mosh;
                default: return chill;
            }
        }

        /// <summary>
        /// 성향 → 연출 단계. 두 열거형은 이름이 1:1로 대응하지만 의미가 다르므로
        /// (성향 = 누가 왔는가, HeatStage = 지금 얼마나 달아올랐는가) 캐스팅하지 않고 여기서 명시한다.
        /// </summary>
        public static HeatStage ToHeatStage(this CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Singalong: return HeatStage.Singalong;
                case CrowdPreference.Mosh: return HeatStage.Mosh;
                default: return HeatStage.Chill;
            }
        }
    }
}
