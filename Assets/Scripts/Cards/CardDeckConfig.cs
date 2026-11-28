using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    [System.Serializable]
    public sealed class CardPoolEntry
    {
        [SerializeField] CardDefinition prefab;
        [SerializeField, Min(0f)] float weight = 1f;

        public CardDefinition Prefab => prefab;
        public float Weight => Mathf.Max(0f, weight);
        public bool IsUsable => prefab != null && Weight > 0f;
    }

    /// <summary>
    /// 카드 프리팹 풀과 런타임 덱 생성 규칙.
    /// 각 덱 묶음은 카드 풀에서 가중치 복원 추출로 만들며 현재/대기 묶음을 함께 준비한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CardDeckConfig", menuName = "ContextStage/Card Deck Config")]
    public sealed class CardDeckConfig : ScriptableObject
    {
        [SerializeField] List<CardPoolEntry> cardPool = new List<CardPoolEntry>();
        [SerializeField, Min(1)] int generatedDeckSize = 10;
        [SerializeField, Range(2, 4)] int preparedDeckCount = 2;
        [SerializeField, Range(1, 4)] int minimumHandSize = 3;
        [SerializeField, Min(4)] private int maximumHandSize = 10;

        [Header("관객 반응 표 거리 배율 (GridDistance 카드)")]
        [SerializeField, Tooltip("정답 칸에서 0칸, 1칸, 2칸, 3칸 떨어졌을 때 (성향 점수 + 몰입도 점수)에 곱하는 값")]
        float[] audienceDistanceMultipliers = { 1f, 0.5f, -0.25f, -1f };
        [SerializeField, Tooltip("이 성향의 관객은 어떤 카드를 받아도 반응이 0 미만으로 내려가지 않는다 (감점·몰입도 하락 없음)")]
        List<CrowdPreference> nonNegativeAudiencePreferences = new List<CrowdPreference>();

        public IReadOnlyList<CardPoolEntry> CardPool => cardPool;

        /// <summary>관객 성향이 0 하한 목록에 있으면 음수 반응을 0으로 올린다.</summary>
        public int ApplyAudienceReactionFloor(CrowdPreference audiencePreference, int reactionValue)
        {
            if (reactionValue >= 0 || nonNegativeAudiencePreferences == null) return reactionValue;
            return nonNegativeAudiencePreferences.Contains(audiencePreference) ? 0 : reactionValue;
        }
        public IReadOnlyList<float> AudienceDistanceMultipliers => audienceDistanceMultipliers;
        public int GeneratedDeckSize => Mathf.Max(1, generatedDeckSize);
        public int PreparedDeckCount => Mathf.Clamp(preparedDeckCount, 2, 4);
        public int MinimumHandSize => Mathf.Clamp(minimumHandSize, 1, 4);
        public int MaximumHandSize => Mathf.Max(4, maximumHandSize);

        public bool HasUsableCards
        {
            get
            {
                if (cardPool == null) return false;

                for (int i = 0; i < cardPool.Count; i++)
                {
                    if (cardPool[i] != null && cardPool[i].IsUsable) return true;
                }

                return false;
            }
        }
    }
}
