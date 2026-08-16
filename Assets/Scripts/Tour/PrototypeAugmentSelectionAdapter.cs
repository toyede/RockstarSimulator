using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 실제 증강 시스템이 들어오기 전 전체 투어 루프와 UI만 검증하기 위한 교체용 어댑터다.
    /// 증강 확률, 티어 규칙, 효과 적용은 의도적으로 구현하지 않는다.
    /// 장우용의 Coordinator가 준비되면 이 클래스만 제거하고 Popup View는 그대로 사용한다.
    /// </summary>
    internal sealed class PrototypeAugmentSelectionAdapter : IDisposable
    {
        readonly AugmentSelectionPopup _popup;
        readonly PrototypeOffer[] _initialOffers;
        readonly PrototypeOffer[] _rerolledOffers;
        readonly PrototypeOffer[] _currentOffers = new PrototypeOffer[AugmentSelectionPopup.VisibleSlotCount];
        readonly int[] _rerollsRemaining = new int[AugmentSelectionPopup.VisibleSlotCount];

        TourRunManager _manager;
        bool _bound;

        public PrototypeAugmentSelectionAdapter(AugmentSelectionPopup popup)
        {
            _popup = popup ?? throw new ArgumentNullException(nameof(popup));
            _initialOffers = new[]
            {
                new PrototypeOffer("PROTOTYPE_CARD_POWER", "COMMON", "CARD POWER UP", "Increase the score of performance cards.", new Color32(0x30, 0xE1, 0xB9, 0xFF)),
                new PrototypeOffer("PROTOTYPE_FEVER_BOOST", "RARE", "FEVER BOOST", "Make the next Fever Time more rewarding.", new Color32(0x8F, 0xD3, 0xFF, 0xFF)),
                new PrototypeOffer("PROTOTYPE_CROWD_SUPPORT", "COMMON", "CROWD SUPPORT", "Start the next show with a friendlier crowd.", new Color32(0x91, 0xDB, 0x69, 0xFF))
            };
            _rerolledOffers = new[]
            {
                new PrototypeOffer("PROTOTYPE_DRAW_SUPPORT", "COMMON", "QUICK DRAW", "Gain more options when your hand is blocked.", new Color32(0xF9, 0xC2, 0x2B, 0xFF)),
                new PrototypeOffer("PROTOTYPE_COMBO_KEEPER", "RARE", "COMBO KEEPER", "Protect the flow of your next performance.", new Color32(0xA8, 0x84, 0xF3, 0xFF)),
                new PrototypeOffer("PROTOTYPE_SPOTLIGHT", "RARE", "SPOTLIGHT", "Special moments grant a larger score bonus.", new Color32(0xF0, 0x4F, 0x78, 0xFF))
            };
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
            if (_manager == null || _manager.CurrentRun == null)
            {
                _popup.ShowError("No active tour was found.");
                return;
            }

            var model = new AugmentSelectionScreenModel
            {
                title = "CHOOSE AN AUGMENT",
                subtitle = "Choose one reward for the next performance.",
                ownedAugmentCount = _manager.CurrentRun.ownedAugmentIds?.Count ?? 0,
                choices = new List<AugmentChoiceViewModel>(AugmentSelectionPopup.VisibleSlotCount)
            };

            for (int i = 0; i < AugmentSelectionPopup.VisibleSlotCount; i++)
            {
                _currentOffers[i] = _initialOffers[i];
                _rerollsRemaining[i] = 1;
                model.choices.Add(CreateViewModel(i));
            }

            _popup.Show(model);
        }

        public void Dispose()
        {
            Unbind();
        }

        void OnSelectRequested(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || _manager == null)
            {
                _popup.ShowError("This choice is no longer available.");
                return;
            }

            PrototypeOffer offer = _currentOffers[slotIndex];
            if (offer == null || !_manager.SelectAugment(offer.Id))
            {
                _popup.ShowError("The Augment could not be selected.");
                return;
            }

            _popup.Hide();
        }

        void OnRerollRequested(int slotIndex)
        {
            if (!IsValidSlot(slotIndex) || _rerollsRemaining[slotIndex] <= 0)
            {
                _popup.ShowError("No rerolls remain for this choice.");
                return;
            }

            _rerollsRemaining[slotIndex]--;
            _currentOffers[slotIndex] = _rerolledOffers[slotIndex];
            _popup.UpdateChoice(CreateViewModel(slotIndex));
        }

        AugmentChoiceViewModel CreateViewModel(int slotIndex)
        {
            PrototypeOffer offer = _currentOffers[slotIndex];
            return new AugmentChoiceViewModel
            {
                slotIndex = slotIndex,
                tierLabel = offer.Tier,
                tierColor = offer.TierColor,
                displayName = offer.Name,
                description = offer.Description,
                rerollsRemaining = _rerollsRemaining[slotIndex],
                canSelect = true
            };
        }

        static bool IsValidSlot(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < AugmentSelectionPopup.VisibleSlotCount;
        }

        sealed class PrototypeOffer
        {
            public readonly string Id;
            public readonly string Tier;
            public readonly string Name;
            public readonly string Description;
            public readonly Color TierColor;

            public PrototypeOffer(string id, string tier, string name, string description, Color tierColor)
            {
                Id = id;
                Tier = tier;
                Name = name;
                Description = description;
                TierColor = tierColor;
            }
        }
    }
}
