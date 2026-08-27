using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 증강 선택 화면의 순수 View. 후보 데이터와 입력 결과만 주고받고,
    /// 리롤/선택의 게임 규칙은 Coordinator가 담당한다.
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
        [SerializeField] AugmentChoiceView[] choiceViews =
            new AugmentChoiceView[VisibleSlotCount];

        [Header("Owned Augments")]
        [SerializeField] Button ownedListButton;
        [SerializeField] GameObject ownedModalBlocker;
        [SerializeField] GameObject ownedModal;
        [SerializeField] Button ownedModalCloseButton;
        [SerializeField] GameObject[] ownedRows = new GameObject[VisibleSlotCount];
        [SerializeField] Image[] ownedIcons = new Image[VisibleSlotCount];
        [SerializeField] Text[] ownedNames = new Text[VisibleSlotCount];
        [SerializeField] Text[] ownedDescriptions = new Text[VisibleSlotCount];

        bool _wired;
        bool _busy;
        Coroutine _selectionRoutine;
        Vector2[] _choiceBasePositions = Array.Empty<Vector2>();

        public bool IsVisible => gameObject.activeSelf;
        public event Action<int> SelectRequested;
        public event Action<int> RerollRequested;

        void Awake()
        {
            WireEvents();
            CaptureChoiceBasePositions();
        }

        void OnDestroy()
        {
            UnwireEvents();
        }

        public void Configure(
            CanvasGroup group,
            Text title,
            Text subtitle,
            Text ownedCount,
            Text feedback,
            AugmentChoiceView[] choices)
        {
            UnwireEvents();
            canvasGroup = group;
            titleText = title;
            subtitleText = subtitle;
            ownedCountText = ownedCount;
            feedbackText = feedback;
            choiceViews = choices ?? Array.Empty<AugmentChoiceView>();
            CaptureChoiceBasePositions();
            WireEvents();
        }

        public void ConfigureOwnedModal(
            Button listButton,
            GameObject modalBlocker,
            GameObject modal,
            Button closeButton,
            GameObject[] rows,
            Image[] icons,
            Text[] names,
            Text[] descriptions)
        {
            UnwireEvents();
            ownedListButton = listButton;
            ownedModalBlocker = modalBlocker;
            ownedModal = modal;
            ownedModalCloseButton = closeButton;
            ownedRows = rows ?? Array.Empty<GameObject>();
            ownedIcons = icons ?? Array.Empty<Image>();
            ownedNames = names ?? Array.Empty<Text>();
            ownedDescriptions = descriptions ?? Array.Empty<Text>();
            WireEvents();
        }

        public void Show(AugmentSelectionScreenModel model)
        {
            if (model == null)
            {
                Debug.LogError("[AugmentSelectionPopup] 표시할 ScreenModel이 없습니다.", this);
                return;
            }

            gameObject.SetActive(true);
            WireEvents();
            ResetChoicePresentation();
            CloseOwnedModal();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            SetText(titleText,
                string.IsNullOrWhiteSpace(model.title) ? "CHOOSE AN AUGMENT" : model.title);
            SetText(subtitleText, model.subtitle ?? "");
            SetText(ownedCountText,
                $"OWNED AUGMENTS  {Mathf.Max(0, model.ownedAugmentCount):00}");
            if (feedbackText != null)
            {
                feedbackText.text = "Each choice can be rerolled independently.";
                feedbackText.color = new Color32(0x9B, 0xAB, 0xB2, 0xFF);
            }

            IReadOnlyList<AugmentChoiceViewModel> choices =
                model.choices ?? new List<AugmentChoiceViewModel>();
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
                Debug.LogWarning(
                    $"[AugmentSelectionPopup] {choices.Count}개 후보 중 화면 슬롯 " +
                    $"{choiceViews.Length}개만 표시합니다.",
                    this);

            BindOwnedAugments(model.ownedAugments);
            SetBusy(false);
        }

        public bool PlayReroll(AugmentChoiceViewModel model)
        {
            if (!TryGetChoice(model, out AugmentChoiceView choice))
            {
                ShowError("The rerolled choice could not be displayed.");
                return false;
            }

            bool started = choice.PlayReroll(model, success =>
            {
                if (!success)
                {
                    ShowError("The reroll animation could not be completed.");
                    return;
                }

                if (feedbackText != null)
                {
                    feedbackText.text = $"SLOT {model.slotIndex + 1} REROLLED";
                    feedbackText.color = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
                }
                SetBusy(false);
            });

            if (!started)
                ShowError("The reroll animation could not be started.");
            return started;
        }

        public bool PlaySelection(int slotIndex, Action<bool> completed)
        {
            if (_selectionRoutine != null ||
                slotIndex < 0 ||
                slotIndex >= choiceViews.Length ||
                choiceViews[slotIndex] == null)
            {
                return false;
            }

            _selectionRoutine = StartCoroutine(PlaySelectionRoutine(slotIndex, completed));
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
            if (ownedListButton != null) ownedListButton.interactable = !busy;
            if (busy && feedbackText != null)
            {
                feedbackText.text = "PROCESSING...";
                feedbackText.color = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
            }
        }

        public void ShowError(string message)
        {
            if (_selectionRoutine == null) ResetChoicePresentation();
            SetBusy(false);
            if (feedbackText == null)
            {
                Debug.LogWarning(
                    $"[AugmentSelectionPopup] " +
                    $"{(string.IsNullOrWhiteSpace(message) ? "REQUEST FAILED" : message)}",
                    this);
                return;
            }

            feedbackText.text = string.IsNullOrWhiteSpace(message)
                ? "REQUEST FAILED"
                : message;
            feedbackText.color = new Color32(0xEA, 0x4F, 0x36, 0xFF);
        }

        public void Hide()
        {
            _busy = false;
            if (_selectionRoutine != null)
            {
                StopCoroutine(_selectionRoutine);
                _selectionRoutine = null;
            }

            CloseOwnedModal();
            ResetChoicePresentation();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        void WireEvents()
        {
            if (_wired || choiceViews == null) return;

            for (int i = 0; i < choiceViews.Length; i++)
            {
                AugmentChoiceView choice = choiceViews[i];
                if (choice == null) continue;
                choice.SelectRequested += OnSelectRequested;
                choice.RerollRequested += OnRerollRequested;
            }

            ownedListButton?.onClick.AddListener(OpenOwnedModal);
            ownedModalCloseButton?.onClick.AddListener(CloseOwnedModal);
            _wired = true;
        }

        void UnwireEvents()
        {
            if (!_wired || choiceViews == null) return;

            for (int i = 0; i < choiceViews.Length; i++)
            {
                AugmentChoiceView choice = choiceViews[i];
                if (choice == null) continue;
                choice.SelectRequested -= OnSelectRequested;
                choice.RerollRequested -= OnRerollRequested;
            }

            ownedListButton?.onClick.RemoveListener(OpenOwnedModal);
            ownedModalCloseButton?.onClick.RemoveListener(CloseOwnedModal);
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

        IEnumerator PlaySelectionRoutine(int slotIndex, Action<bool> completed)
        {
            AugmentChoiceView selected = choiceViews[slotIndex];
            selected.ShowSelectedTransitionFrame();

            const float fadeDuration = 0.5f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);

                for (int i = 0; i < choiceViews.Length; i++)
                {
                    AugmentChoiceView choice = choiceViews[i];
                    if (choice == null) continue;

                    if (i == slotIndex)
                    {
                        choice.SetRerollAlpha(1f - eased);
                        continue;
                    }

                    choice.SetVisualAlpha(1f - eased);
                    Vector2 start = GetChoiceBasePosition(i);
                    choice.RectTransform.anchoredPosition =
                        Vector2.LerpUnclamped(start, start + Vector2.down * 65f, eased);
                }
                yield return null;
            }

            yield return WaitUnscaled(0.3f);

            elapsed = 0f;
            while (elapsed < 0.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / 0.1f);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                selected.SetVisualAlpha(1f - eased);
                yield return null;
            }

            yield return WaitUnscaled(0.2f);
            yield return WaitUnscaled(0.1f);

            _selectionRoutine = null;
            completed?.Invoke(true);
        }

        IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void BindOwnedAugments(IReadOnlyList<AugmentOwnedItemViewModel> models)
        {
            int modelCount = models?.Count ?? 0;
            for (int i = 0; i < ownedRows.Length; i++)
            {
                bool visible = i < modelCount && models[i] != null;
                if (ownedRows[i] != null) ownedRows[i].SetActive(visible);
                if (!visible) continue;

                AugmentOwnedItemViewModel model = models[i];
                if (i < ownedIcons.Length && ownedIcons[i] != null)
                {
                    ownedIcons[i].sprite = model.icon;
                    ownedIcons[i].enabled = model.icon != null;
                }
                if (i < ownedNames.Length) SetText(ownedNames[i], model.displayName ?? "");
                if (i < ownedDescriptions.Length)
                    SetText(ownedDescriptions[i], model.description ?? "");
            }
        }

        void OpenOwnedModal()
        {
            if (_busy || ownedModal == null) return;
            if (ownedModalBlocker != null)
            {
                ownedModalBlocker.SetActive(true);
                ownedModalBlocker.transform.SetAsLastSibling();
            }
            ownedModal.transform.SetAsLastSibling();
            ownedModal.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        void CloseOwnedModal()
        {
            if (ownedModal != null) ownedModal.SetActive(false);
            if (ownedModalBlocker != null) ownedModalBlocker.SetActive(false);
        }

        void CaptureChoiceBasePositions()
        {
            if (choiceViews == null)
            {
                _choiceBasePositions = Array.Empty<Vector2>();
                return;
            }

            _choiceBasePositions = new Vector2[choiceViews.Length];
            for (int i = 0; i < choiceViews.Length; i++)
            {
                if (choiceViews[i] != null)
                    _choiceBasePositions[i] = choiceViews[i].RectTransform.anchoredPosition;
            }
        }

        void ResetChoicePresentation()
        {
            if (choiceViews == null) return;
            if (_choiceBasePositions.Length != choiceViews.Length)
                CaptureChoiceBasePositions();

            for (int i = 0; i < choiceViews.Length; i++)
            {
                AugmentChoiceView choice = choiceViews[i];
                if (choice == null) continue;
                choice.RectTransform.anchoredPosition = GetChoiceBasePosition(i);
                choice.ResetPresentation();
            }
        }

        Vector2 GetChoiceBasePosition(int index) =>
            index >= 0 && index < _choiceBasePositions.Length
                ? _choiceBasePositions[index]
                : Vector2.zero;

        bool TryGetChoice(
            AugmentChoiceViewModel model,
            out AugmentChoiceView choice)
        {
            choice = null;
            if (model == null ||
                model.slotIndex < 0 ||
                model.slotIndex >= choiceViews.Length)
            {
                return false;
            }

            choice = choiceViews[model.slotIndex];
            return choice != null;
        }

        static void SetText(Text target, string value)
        {
            if (target != null) target.text = value;
        }
    }
}
