using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace ContextStage
{
    /// <summary>
    /// 카드 덱 구성과 손패 수 규칙. 같은 CardData를 여러 번 넣어 카드의 장수를 표현한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CardDeckConfig", menuName = "ContextStage/Card Deck Config")]
    public sealed class CardDeckConfig : ScriptableObject
    {
        [SerializeField] List<CardData> cards = new List<CardData>();
        [FormerlySerializedAs("baseHandSize")]
        [SerializeField, Range(1, 9)] int minimumHandSize = 3;
        [SerializeField, Range(1, 9)] int maximumHandSize = 9;
        [SerializeField] bool shuffleOnStart = true;

        public IReadOnlyList<CardData> Cards => cards;
        public int MinimumHandSize => Mathf.Clamp(minimumHandSize, 1, 9);
        public int MaximumHandSize => Mathf.Clamp(maximumHandSize, MinimumHandSize, 9);
        public bool ShuffleOnStart => shuffleOnStart;
    }
}
