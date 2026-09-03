using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "AugmentCatalog",
        menuName = "Context Stage/Augments/Augment Catalog")]
    public sealed class AugmentCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Augments/AugmentCatalog";

        [SerializeField] List<AugmentDefinition> definitions =
            new List<AugmentDefinition>();

        public IReadOnlyList<AugmentDefinition> Definitions => definitions;

        public static AugmentCatalog LoadDefault() =>
            Resources.Load<AugmentCatalog>(ResourcesPath);

        public bool TryGetDefinition(string augmentId, out AugmentDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(augmentId) && definitions != null)
            {
                for (int i = 0; i < definitions.Count; i++)
                {
                    AugmentDefinition candidate = definitions[i];
                    if (candidate != null &&
                        string.Equals(candidate.AugmentId, augmentId, StringComparison.Ordinal))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            if (definitions == null || definitions.Count == 0)
            {
                error = "The Augment catalog is empty.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                AugmentDefinition definition = definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.AugmentId))
                {
                    error = $"Augment definition {i} is missing or has no ID.";
                    return false;
                }

                if (!ids.Add(definition.AugmentId))
                {
                    error = $"Duplicate Augment ID: {definition.AugmentId}";
                    return false;
                }

                var enabledTiers = new HashSet<AugmentTier>();
                IReadOnlyList<AugmentTierData> tiers = definition.Tiers;
                for (int tierIndex = 0; tierIndex < tiers.Count; tierIndex++)
                {
                    AugmentTierData tier = tiers[tierIndex];
                    if (tier == null || !tier.Enabled) continue;

                    if (!enabledTiers.Add(tier.Tier))
                    {
                        error = $"{definition.AugmentId} has duplicate {tier.Tier} data.";
                        return false;
                    }

                    if (definition.EffectType == AugmentEffectType.GrantCard &&
                        tier.GrantedCard == null)
                    {
                        error =
                            $"{definition.AugmentId}:{tier.Tier} has no granted card.";
                        return false;
                    }

                    if (definition.EffectType == AugmentEffectType.CardUpgrade &&
                        (tier.CardUpgrade == null || !tier.CardUpgrade.IsConfigured))
                    {
                        error =
                            $"{definition.AugmentId}:{tier.Tier} has no card upgrade target.";
                        return false;
                    }
                }

                if (enabledTiers.Count == 0)
                {
                    error = $"{definition.AugmentId} has no enabled tiers.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
