using System.Collections.Generic;

namespace ContextStage
{
    internal static class AugmentOwnedViewModelBuilder
    {
        public static List<AugmentOwnedItemViewModel> Build(
            TourRunState run,
            AugmentCatalog catalog)
        {
            var models = new List<AugmentOwnedItemViewModel>();
            if (run?.ownedAugments == null || catalog == null) return models;

            for (int i = 0; i < run.ownedAugments.Count; i++)
            {
                OwnedAugmentState owned = run.ownedAugments[i];
                if (owned == null ||
                    !catalog.TryGetDefinition(
                        owned.definitionId,
                        out AugmentDefinition definition) ||
                    !definition.TryGetTierData(
                        owned.tier,
                        out AugmentTierData tierData))
                {
                    continue;
                }

                CardUpgradeData upgrade = tierData.CardUpgrade;
                CardDefinition previewCard = tierData.GrantedCard != null
                    ? tierData.GrantedCard
                    : upgrade?.TargetCard;
                string description = tierData.GrantedCard != null
                    ? tierData.GrantedCard.Description
                    : upgrade != null && !string.IsNullOrWhiteSpace(upgrade.DescriptionOverride)
                        ? upgrade.DescriptionOverride
                        : tierData.Description;
                models.Add(new AugmentOwnedItemViewModel
                {
                    tier = owned.tier,
                    icon = definition.Icon != null
                        ? definition.Icon
                        : upgrade?.Artwork != null
                            ? upgrade.Artwork
                            : previewCard?.Artwork,
                    displayName = AugmentEnglish.Name(definition),
                    description = AugmentEnglish.Description(definition, tierData, description, previewCard != null)
                });
            }

            return models;
        }
    }
}
