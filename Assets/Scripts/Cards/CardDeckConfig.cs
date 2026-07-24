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
        [SerializeField, Range(1, 4)] int maximumHandSize = 4;

        public IReadOnlyList<CardPoolEntry> CardPool => cardPool;
        public int GeneratedDeckSize => Mathf.Max(1, generatedDeckSize);
        public int PreparedDeckCount => Mathf.Clamp(preparedDeckCount, 2, 4);
        public int MinimumHandSize => Mathf.Clamp(minimumHandSize, 1, 4);
        public int MaximumHandSize => Mathf.Clamp(maximumHandSize, MinimumHandSize, 4);

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
