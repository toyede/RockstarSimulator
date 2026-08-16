using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 증강 선택 화면의 순수 View.
    /// 외부 Presenter/Coordinator가 ViewModel을 공급하고 슬롯 입력을 처리한다.
    /// </summary>
    public sealed class AugmentSelectionPopup : MonoBehaviour
    {
        public const int VisibleSlotCount = 3;

        [Header("Popup")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Text ownedCountText;
        [SerializeField] Text feedbackText;

        [Header("Choices")]
        [SerializeField] AugmentChoiceView[] choiceViews = new AugmentChoiceView[VisibleSlotCount];

        bool _wired;
        bool _busy;

        public bool IsVisible => gameObject.activeSelf;
        public event Action<int> SelectRequested;
        public event Action<int> RerollRequested;

        void Awake()
        {
            WireChoiceEvents();
        }

        void OnDestroy()
        {
            UnwireChoiceEvents();
        }

        public void Configure(
            CanvasGroup group,
            Text title,
            Text subtitle,
            Text ownedCount,
            Text feedback,
            AugmentChoiceView[] choices)
        {
            UnwireChoiceEvents();
            canvasGroup = group;
            titleText = title;
            subtitleText = subtitle;
            ownedCountText = ownedCount;
            feedbackText = feedback;
            choiceViews = choices ?? Array.Empty<AugmentChoiceView>();
            WireChoiceEvents();
        }

        public void Show(AugmentSelectionScreenModel model)
        {
            if (model == null)
            {
                Debug.LogError("[AugmentSelectionPopup] 표시할 ScreenModel이 없습니다.", this);
                return;
            }

            gameObject.SetActive(true);
            WireChoiceEvents();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            titleText.text = string.IsNullOrWhiteSpace(model.title) ? "CHOOSE AN AUGMENT" : model.title;
            subtitleText.text = model.subtitle ?? "";
            ownedCountText.text = $"OWNED AUGMENTS  {Mathf.Max(0, model.ownedAugmentCount):00}";
            feedbackText.text = "Each choice can be rerolled independently.";
            feedbackText.color = new Color32(0x9B, 0xAB, 0xB2, 0xFF);

            IReadOnlyList<AugmentChoiceViewModel> choices = model.choices ?? new List<AugmentChoiceViewModel>();
            for (int i = 0; i < choiceViews.Length; i++)
            {
                if (i < choices.Count && choices[i] != null)
                {
                    AugmentChoiceViewModel choice = choices[i].Clone();
                    choice.slotIndex = i;
                    choiceViews[i].Bind(choice);
                }
                else
                {
                    choiceViews[i].Clear();
                }
            }

            if (choices.Count > choiceViews.Length)
                Debug.LogWarning($"[AugmentSelectionPopup] {choices.Count}개 후보 중 화면 슬롯 {choiceViews.Length}개만 표시합니다.", this);

            SetBusy(false);
        }

        public bool UpdateChoice(AugmentChoiceViewModel model)
        {
            if (model == null || model.slotIndex < 0 || model.slotIndex >= choiceViews.Length)
            {
                ShowError("The rerolled choice could not be displayed.");
                return false;
            }

            choiceViews[model.slotIndex].Bind(model);
            SetBusy(false);
            feedbackText.text = $"SLOT {model.slotIndex + 1} REROLLED";
            feedbackText.color = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
            return true;
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            for (int i = 0; i < choiceViews.Length; i++)
            {
                if (choiceViews[i] != null) choiceViews[i].SetInputEnabled(!busy);
            }

            if (canvasGroup != null) canvasGroup.interactable = !busy;
            if (busy && feedbackText != null)
            {
                feedbackText.text = "PROCESSING...";
                feedbackText.color = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
            }
        }

        public void ShowError(string message)
        {
            SetBusy(false);
            if (feedbackText == null) return;
            feedbackText.text = string.IsNullOrWhiteSpace(message) ? "REQUEST FAILED" : message;
            feedbackText.color = new Color32(0xEA, 0x4F, 0x36, 0xFF);
        }

        public void Hide()
        {
            _busy = false;
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        void WireChoiceEvents()
        {
            if (_wired || choiceViews == null) return;

            for (int i = 0; i < choiceViews.Length; i++)
            {
                AugmentChoiceView choice = choiceViews[i];
                if (choice == null) continue;
                choice.SelectRequested += OnSelectRequested;
                choice.RerollRequested += OnRerollRequested;
            }

            _wired = true;
        }

        void UnwireChoiceEvents()
        {
            if (!_wired || choiceViews == null) return;

            for (int i = 0; i < choiceViews.Length; i++)
            {
                AugmentChoiceView choice = choiceViews[i];
                if (choice == null) continue;
                choice.SelectRequested -= OnSelectRequested;
                choice.RerollRequested -= OnRerollRequested;
            }

            _wired = false;
        }

        void OnSelectRequested(int slotIndex)
        {
            if (_busy) return;
            SetBusy(true);
            SelectRequested?.Invoke(slotIndex);
        }

        void OnRerollRequested(int slotIndex)
        {
            if (_busy) return;
            SetBusy(true);
            RerollRequested?.Invoke(slotIndex);
        }
    }
}
