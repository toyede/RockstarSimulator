using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 보상 테이블과 보유 상태로 선택지 세 개를 만들고 View의 슬롯 요청을 처리한다.
    /// View에는 ID를 노출하지 않으며 선택이 확정될 때만 TourRunState를 변경한다.
    /// </summary>
    internal sealed class AugmentSelectionCoordinator : IDisposable
    {
        readonly AugmentSelectionPopup _popup;
        readonly AugmentOffer[] _currentOffers =
            new AugmentOffer[AugmentSelectionPopup.VisibleSlotCount];
        readonly int[] _rerollsRemaining =
            new int[AugmentSelectionPopup.VisibleSlotCount];

        TourRunManager _manager;
        AugmentCatalog _catalog;
        AugmentRewardTable _rewardTable;
        System.Random _random;
        bool _bound;

        public AugmentSelectionCoordinator(AugmentSelectionPopup popup)
        {
            _popup = popup ?? throw new ArgumentNullException(nameof(popup));
        }

        public void Bind(TourRunManager manager)
        {
            if (_bound && ReferenceEquals(_manager, manager)) return;
            Unbind();
            _manager = manager;
            _popup.SelectRequested += OnSelectRequested;
            _popup.RerollRequested += OnRerollRequested;
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;
            _popup.SelectRequested -= OnSelectRequested;
            _popup.RerollRequested -= OnRerollRequested;
            _manager = null;
            _bound = false;
        }

        public void Show()
        {
            TourRunState run = _manager == null ? null : _manager.CurrentRun;
            if (run == null || run.phase != RunPhase.Reward)
            {
                _popup.ShowError("No Augment reward is currently available.");
                return;
            }

            _catalog = AugmentCatalog.LoadDefault();
            if (_catalog == null)
            {
                _popup.ShowError("The Augment catalog could not be loaded.");
                return;
            }

            if (!_catalog.TryValidate(out string catalogError))
            {
                _popup.ShowError(catalogError);
                return;
            }

            string rewardTableId = _manager.CurrentStageDefinition == null
                ? string.Empty
                : _manager.CurrentStageDefinition.RewardTableId;
            _rewardTable = AugmentRewardTable.Load(rewardTableId);
            if (_rewardTable == null)
            {
                _popup.ShowError($"Reward table '{rewardTableId}' could not be loaded.");
                return;
            }

            _random = new System.Random(CreateOfferSeed(run));
            List<AugmentOffer> candidates = BuildAvailableOffers(run);
            if (candidates.Count < AugmentSelectionPopup.VisibleSlotCount)
            {
                _popup.ShowError("There are not enough unique Augments remaining.");
                return;
            }

            var model = new AugmentSelectionScreenModel
            {
                title = "CHOOSE AN AUGMENT",
                subtitle = "The same Augment and tier will not appear again.",
                ownedAugmentCount = run.ownedAugments?.Count ?? 0,
                choices = new List<AugmentChoiceViewModel>(
                    AugmentSelectionPopup.VisibleSlotCount)
            };

            var displayedDefinitions = new HashSet<string>(StringComparer.Ordinal);
            for (int slotIndex = 0; slotIndex < AugmentSelectionPopup.VisibleSlotCount; slotIndex++)
            {
                AugmentOffer offer = TakeOffer(candidates, displayedDefinitions);
                _currentOffers[slotIndex] = offer;
                _rerollsRemaining[slotIndex] = _rewardTable.RerollsPerSlot;
                displayedDefinitions.Add(offer.Definition.AugmentId);
                model.choices.Add(CreateViewModel(slotIndex));
            }

            _popup.Show(model);
        }

        public void Dispose() => Unbind();

        void OnSelectRequested(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || _manager == null)
            {
                _popup.ShowError("This choice is no longer available.");
                return;
            }

            AugmentOffer offer = _currentOffers[slotIndex];
            if (offer == null ||
                !_manager.SelectAugment(offer.Definition.AugmentId, offer.TierData.Tier))
            {
                _popup.ShowError("The Augment could not be selected.");
                return;
            }

            _popup.Hide();
        }

        void OnRerollRequested(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) ||
                _manager == null ||
                _manager.CurrentRun == null ||
                _rerollsRemaining[slotIndex] <= 0)
            {
                _popup.ShowError("No rerolls remain for this choice.");
                return;
            }

            List<AugmentOffer> candidates = BuildAvailableOffers(_manager.CurrentRun);
            ExcludeDisplayedOffers(candidates);
            if (candidates.Count == 0)
            {
                _popup.ShowError("No alternative Augment is available.");
                return;
            }

            var displayedDefinitions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _currentOffers.Length; i++)
            {
                if (i == slotIndex || _currentOffers[i] == null) continue;
                displayedDefinitions.Add(_currentOffers[i].Definition.AugmentId);
            }

            _rerollsRemaining[slotIndex]--;
            _currentOffers[slotIndex] = TakeOffer(candidates, displayedDefinitions);
            _popup.UpdateChoice(CreateViewModel(slotIndex));
        }

        List<AugmentOffer> BuildAvailableOffers(TourRunState run)
        {
            var offers = new List<AugmentOffer>();
            IReadOnlyList<AugmentDefinition> definitions = _catalog.Definitions;
            for (int definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                AugmentDefinition definition = definitions[definitionIndex];
                if (definition == null) continue;
                if (definition.TierOwnershipPolicy ==
                        AugmentTierOwnershipPolicy.OneTierPerRun &&
                    run.HasAugmentDefinition(definition.AugmentId))
                    continue;

                IReadOnlyList<AugmentTierData> tiers = definition.Tiers;
                for (int tierIndex = 0; tierIndex < tiers.Count; tierIndex++)
                {
                    AugmentTierData tierData = tiers[tierIndex];
                    if (tierData == null ||
                        !tierData.Enabled ||
                        run.HasAugment(definition.AugmentId, tierData.Tier))
                        continue;

                    offers.Add(new AugmentOffer(definition, tierData));
                }
            }

            return offers;
        }

        void ExcludeDisplayedOffers(List<AugmentOffer> candidates)
        {
            for (int candidateIndex = candidates.Count - 1; candidateIndex >= 0; candidateIndex--)
            {
                AugmentOffer candidate = candidates[candidateIndex];
                for (int slotIndex = 0; slotIndex < _currentOffers.Length; slotIndex++)
                {
                    AugmentOffer displayed = _currentOffers[slotIndex];
                    if (displayed != null && displayed.Key.Equals(candidate.Key))
                    {
                        candidates.RemoveAt(candidateIndex);
                        break;
                    }
                }
            }
        }

        AugmentOffer TakeOffer(
            List<AugmentOffer> candidates,
            HashSet<string> displayedDefinitions)
        {
            var preferred = new List<AugmentOffer>();
            for (int i = 0; i < candidates.Count; i++)
            {
                AugmentOffer candidate = candidates[i];
                if (!displayedDefinitions.Contains(candidate.Definition.AugmentId))
                    preferred.Add(candidate);
            }

            List<AugmentOffer> pool = preferred.Count > 0 ? preferred : candidates;
            AugmentOffer selected = PickWeighted(pool);
            candidates.Remove(selected);
            return selected;
        }

        AugmentOffer PickWeighted(List<AugmentOffer> pool)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
                totalWeight += _rewardTable.GetTierWeight(pool[i].TierData.Tier);

            if (totalWeight <= 0f)
                return pool[_random.Next(pool.Count)];

            double roll = _random.NextDouble() * totalWeight;
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= _rewardTable.GetTierWeight(pool[i].TierData.Tier);
                if (roll <= 0d) return pool[i];
            }

            return pool[pool.Count - 1];
        }

        AugmentChoiceViewModel CreateViewModel(int slotIndex)
        {
            AugmentOffer offer = _currentOffers[slotIndex];
            CardDefinition grantedCard = offer.TierData.GrantedCard;
            return new AugmentChoiceViewModel
            {
                slotIndex = slotIndex,
                icon = offer.Definition.Icon != null
                    ? offer.Definition.Icon
                    : grantedCard?.Artwork,
                tierLabel = offer.TierData.Tier.ToString(),
                tierColor = ResolveTierColor(offer.TierData.Tier),
                displayName = offer.Definition.DisplayName,
                description = grantedCard != null
                    ? grantedCard.Description
                    : offer.TierData.Description,
                rerollsRemaining = _rerollsRemaining[slotIndex],
                canSelect = true
            };
        }

        static int CreateOfferSeed(TourRunState run)
        {
            unchecked
            {
                int ownedCount = run.ownedAugments?.Count ?? 0;
                int resultCount = run.stageResults?.Count ?? 0;
                return run.seed ^ (ownedCount * 73856093) ^ (resultCount * 19349663);
            }
        }

        static Color ResolveTierColor(AugmentTier tier)
        {
            switch (tier)
            {
                case AugmentTier.Bronze:
                    return new Color32(0xCD, 0x7F, 0x32, 0xFF);
                case AugmentTier.Silver:
                    return new Color32(0xC0, 0xC0, 0xC0, 0xFF);
                case AugmentTier.Gold:
                    return new Color32(0xFF, 0xD7, 0x00, 0xFF);
                default:
                    return Color.white;
            }
        }

        static bool IsValidSlot(int slotIndex) =>
            slotIndex >= 0 && slotIndex < AugmentSelectionPopup.VisibleSlotCount;

        sealed class AugmentOffer
        {
            public AugmentOffer(AugmentDefinition definition, AugmentTierData tierData)
            {
                Definition = definition;
                TierData = tierData;
                Key = new AugmentKey(definition.AugmentId, tierData.Tier);
            }

            public AugmentDefinition Definition { get; }
            public AugmentTierData TierData { get; }
            public AugmentKey Key { get; }
        }
    }
}
