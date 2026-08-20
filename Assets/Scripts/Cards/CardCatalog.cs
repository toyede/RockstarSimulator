using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "CardCatalog",
        menuName = "ContextStage/Cards/Card Catalog")]
    public sealed class CardCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Cards/CardCatalog";

        [SerializeField] List<CardDefinition> definitions =
            new List<CardDefinition>();

        public IReadOnlyList<CardDefinition> Definitions => definitions;

        public static CardCatalog LoadDefault() =>
            Resources.Load<CardCatalog>(ResourcesPath);

        public bool TryGetCard(string cardId, out CardDefinition card)
        {
            if (!string.IsNullOrWhiteSpace(cardId) && definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    CardDefinition candidate = definitions[i];
                    if (candidate != null &&
                        string.Equals(candidate.Id, cardId, StringComparison.Ordinal))
                    {
                        card = candidate;
                        return true;
                    }
                }
            }

            card = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (definitions == null || definitions.Count == 0)
            {
                error = "The card catalog is empty.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                CardDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                {
                    error = $"Card definition {i} is missing or has no ID.";
                    return false;
                }

                if (!ids.Add(definition.Id))
                {
                    error = $"Duplicate card ID: {definition.Id}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
