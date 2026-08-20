using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Builds one complete generated deck batch from the configured card pool.
    /// Every usable card is included once, then remaining slots are filled from
    /// normal cards with replacement before the batch is shuffled.
    /// </summary>
    public static class CardDeckBatchBuilder
    {
        public static bool TryBuild(
            CardDeckConfig config,
            out List<CardDefinition> cards,
            out string error,
            Func<float> randomValue = null)
        {
            return TryBuild(
                config,
                null,
                out cards,
                out error,
                randomValue);
        }

        public static bool TryBuild(
            CardDeckConfig config,
            IReadOnlyList<CardDefinition> addedCards,
            out List<CardDefinition> cards,
            out string error,
            Func<float> randomValue = null)
        {
            cards = null;
            error = string.Empty;

            if (config == null)
            {
                error = "CardDeckConfig is missing.";
                return false;
            }

            var baseCards = new List<CardDefinition>(config.CardPool.Count);
            var bonusCandidates = new List<CardPoolEntry>(config.CardPool.Count);
            var seenCards = new HashSet<CardDefinition>();

            for (int i = 0; i < config.CardPool.Count; i++)
            {
                CardPoolEntry entry = config.CardPool[i];
                if (entry == null || !entry.IsUsable) continue;

                CardDefinition card = entry.Prefab;
                if (!seenCards.Add(card))
                {
                    error = $"Card '{card.Id}' is registered more than once in the pool.";
                    return false;
                }

                baseCards.Add(card);
                if (card.Role == CardRole.Normal)
                    bonusCandidates.Add(entry);
            }

            if (baseCards.Count == 0)
            {
                error = "The card pool has no usable cards.";
                return false;
            }

            if (config.GeneratedDeckSize < baseCards.Count)
            {
                error =
                    $"Generated deck size {config.GeneratedDeckSize} is smaller than " +
                    $"the {baseCards.Count} usable card types.";
                return false;
            }

            int addedCardCount = addedCards?.Count ?? 0;
            for (int i = 0; i < addedCardCount; i++)
            {
                if (addedCards[i] != null) continue;

                error = $"Run-added card {i} is missing.";
                return false;
            }

            int bonusCount = config.GeneratedDeckSize - baseCards.Count;
            if (bonusCount > 0 && bonusCandidates.Count == 0)
            {
                error =
                    "Generated deck has bonus slots, but there are no usable " +
                    "normal cards.";
                return false;
            }

            cards = new List<CardDefinition>(
                config.GeneratedDeckSize + addedCardCount);
            cards.AddRange(baseCards);
            for (int i = 0; i < addedCardCount; i++)
                cards.Add(addedCards[i]);

            for (int i = 0; i < bonusCount; i++)
            {
                CardDefinition bonusCard = PickWeightedBonus(bonusCandidates, randomValue);
                if (bonusCard == null)
                {
                    cards = null;
                    error = "Failed to select a normal bonus card.";
                    return false;
                }

                cards.Add(bonusCard);
            }

            Shuffle(cards, randomValue);
            return true;
        }

        static CardDefinition PickWeightedBonus(
            IReadOnlyList<CardPoolEntry> candidates,
            Func<float> randomValue)
        {
            float totalWeight = 0f;
            for (int i = 0; i < candidates.Count; i++)
                totalWeight += candidates[i].Weight;

            if (totalWeight <= 0f) return null;

            float roll = NextValue(randomValue) * totalWeight;
            for (int i = 0; i < candidates.Count; i++)
            {
                CardPoolEntry candidate = candidates[i];
                if (roll < candidate.Weight) return candidate.Prefab;
                roll -= candidate.Weight;
            }

            return candidates[candidates.Count - 1].Prefab;
        }

        static void Shuffle(List<CardDefinition> cards, Func<float> randomValue)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int selectedIndex = Mathf.Min(
                    Mathf.FloorToInt(NextValue(randomValue) * (i + 1)),
                    i);
                (cards[i], cards[selectedIndex]) = (cards[selectedIndex], cards[i]);
            }
        }

        static float NextValue(Func<float> randomValue)
            => Mathf.Clamp01(randomValue != null ? randomValue() : UnityEngine.Random.value);
    }
}
