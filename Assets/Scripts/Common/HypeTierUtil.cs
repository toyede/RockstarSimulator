using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 호응도 구간(티어)을 가진 데이터가 구현하는 인터페이스.
    /// 사운드(CrowdAmbienceTier)·비주얼(CrowdMoodTier)이 같은 규칙으로 단계를 나누기 위해 공유한다.
    /// </summary>
    public interface IHypeTier
    {
        /// <summary>호응도 비율(0~1)이 이 값 이상이면 이 티어.</summary>
        float MinNormalized { get; }
    }

    /// <summary>
    /// 호응도 비율 → 티어 인덱스 변환의 단일 구현.
    /// 새로운 반응 시스템(조명·카메라·BGM 레이어 등)을 추가할 때도 이 함수만 쓰면
    /// 사운드/관객과 정확히 같은 타이밍에 단계가 바뀐다.
    /// </summary>
    public static class HypeTierUtil
    {
        /// <summary>
        /// currentIndex 를 넘기면 히스테리시스가 적용되어 경계에서 단계가 진동하지 않는다.
        /// (처음 결정할 때는 -1)
        /// </summary>
        public static int Resolve<T>(IReadOnlyList<T> tiers, float normalized, int currentIndex = -1, float hysteresis = 0f)
            where T : class, IHypeTier
        {
            if (tiers == null || tiers.Count == 0) return -1;

            int result = -1;
            for (int i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                if (tier == null) continue;

                // 위로 올라갈 때는 경계보다 조금 더, 내려갈 때는 조금 덜 가야 바뀐다
                float threshold = tier.MinNormalized;
                if (currentIndex >= 0)
                    threshold += i > currentIndex ? hysteresis : -hysteresis;

                if (normalized >= threshold) result = i;
            }
            return result;
        }

        /// <summary>인덱스를 리스트 범위 안으로 자른다. 비어 있으면 -1.</summary>
        public static int ClampIndex<T>(IReadOnlyList<T> tiers, int index) =>
            tiers == null || tiers.Count == 0 ? -1 : Mathf.Clamp(index, 0, tiers.Count - 1);

        /// <summary>이름으로 티어 인덱스를 찾는다 (대소문자 무시). 없으면 -1.</summary>
        public static int IndexOfName(IReadOnlyList<string> names, string target)
        {
            if (names == null || string.IsNullOrEmpty(target)) return -1;
            for (int i = 0; i < names.Count; i++)
                if (string.Equals(names[i], target, System.StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
