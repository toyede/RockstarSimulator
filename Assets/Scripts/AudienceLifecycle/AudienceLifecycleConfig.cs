using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객 유입/이탈 밸런스 수치 모음. (기획서 `.myDox/기획안 변경 아이디어 정리.md` 10장
    /// "핵심 밸런스 조정 항목"에 따라 확정 수치가 아니므로 코드가 아니라 이 에셋에서 조정한다)
    /// </summary>
    [CreateAssetMenu(fileName = "AudienceLifecycleConfig", menuName = "ContextStage/Audience Lifecycle Config")]
    public class AudienceLifecycleConfig : ScriptableObject
    {
        [Header("인원")]
        [Tooltip("공연 시작 시 초기 관객 수")]
        [Min(0)] public int initialCount = 3;

        [Tooltip("동시에 존재할 수 있는 최대 관객 수")]
        [Min(1)] public int maxCount = 10;

        [Header("초기/유입 몰입도 (기획 3.1: 30~50 무작위)")]
        [Tooltip("초기 관객 및 신규 유입 관객의 몰입도 최솟값")]
        [Range(0f, 100f)] public float spawnImmersionMin = 30f;

        [Tooltip("초기 관객 및 신규 유입 관객의 몰입도 최댓값")]
        [Range(0f, 100f)] public float spawnImmersionMax = 50f;

        [Header("일반 신규 유입 (기획 4.2)")]
        [Tooltip("몇 초마다 유입 여부를 확률로 판정할지")]
        [Min(0.1f)] public float inflowCheckInterval = 5f;

        [Tooltip("판정 주기마다 실제로 유입이 발생할 확률 (0~1)")]
        [Range(0f, 1f)] public float inflowProbability = 0.3f;

        public float RollSpawnImmersion() => Random.Range(spawnImmersionMin, spawnImmersionMax);

#if UNITY_EDITOR
        void OnValidate()
        {
            initialCount = Mathf.Clamp(initialCount, 0, maxCount);
            if (spawnImmersionMax < spawnImmersionMin) spawnImmersionMax = spawnImmersionMin;
        }
#endif
    }
}
