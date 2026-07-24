using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 카드 덱 구성과 최초 손패 규칙. 같은 CardData를 여러 번 넣어 카드의 장수를 표현한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CardDeckConfig", menuName = "ContextStage/Card Deck Config")]
    public sealed class CardDeckConfig : ScriptableObject
    {
        [SerializeField] List<CardData> cards = new List<CardData>();
        [SerializeField, Range(1, 8)] int baseHandSize = 4;
        [SerializeField] bool shuffleOnStart = true;

        public IReadOnlyList<CardData> Cards => cards;
        public int BaseHandSize => Mathf.Clamp(baseHandSize, 1, 8);
        public bool ShuffleOnStart => shuffleOnStart;
    }
}
