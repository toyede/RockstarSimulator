using System;
using GameJamKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 아트 화면이 준비되기 전 전체 투어 루프를 검증하기 위한 런타임 임시 UI.
    /// TourRunManager의 상태를 표시하고 버튼 입력만 전달한다.
    /// </summary>
    public sealed class TourPrototypeUI : MonoBehaviour
    {
        const string MainSceneName = "Main";

        [Header("Figma Tour Map")]
        [SerializeField] private TourMapArt mapArt = new TourMapArt();

        Text _titleText;
        Text _phaseText;
        Text _bodyText;
        RectTransform _mainPanel;
        RectTransform _buttonRoot;
        TourRunManager _manager;
        AugmentSelectionPopup _augmentPopup;
        AugmentSelectionCoordinator _augmentCoordinator;
        TourMapScreen _mapScreen;

        void Awake()
        {
            TourPrototypeUIFactory.EnsureEventSystem();
            BuildUI();
        }

        void OnEnable()
        {
            _manager = TourRunManager.Instance;
            if (_manager != null) _manager.StateChanged += Refresh;
            _augmentCoordinator?.Bind(_manager);
            Refresh();
        }

        void OnDisable()
        {
            if (_manager != null) _manager.StateChanged -= Refresh;
            _augmentCoordinator?.Unbind();
            if (_augmentPopup != null) _augmentPopup.HideOwnedModal();
            if (_mapScreen != null) _mapScreen.Hide();
        }

        void OnDestroy()
        {
            if (_mapScreen != null)
                _mapScreen.OwnedAugmentsRequested -= ShowOwnedAugments;
            _augmentCoordinator?.Dispose();
        }

        void BuildUI()
        {
            Canvas canvas = TourPrototypeUIFactory.CreateCanvas("TourPrototypeCanvas", 100);
            TourPrototypeUIFactory.CreateFullscreenImage(
                canvas.transform,
                new Color32(0x16, 0x12, 0x1C, 0xFF));

            RectTransform panel = TourPrototypeUIFactory.CreatePanel(
                canvas.transform,
                "TourPanel",
                new Vector2(1120f, 820f),
                Vector2.zero,
                new Color32(0x2E, 0x22, 0x2F, 0xF8));
            _mainPanel = panel;

            _titleText = TourPrototypeUIFactory.CreateText(
                panel,
                "Title",
                44,
                TextAnchor.MiddleCenter,
                new Color32(0xF9, 0xC2, 0x2B, 0xFF));
            TourPrototypeUIFactory.SetRect(_titleText.rectTransform, new Vector2(0f, 335f), new Vector2(980f, 80f));

            _phaseText = TourPrototypeUIFactory.CreateText(
                panel,
                "Phase",
                22,
                TextAnchor.MiddleCenter,
                new Color32(0x9B, 0xAB, 0xB2, 0xFF));
            TourPrototypeUIFactory.SetRect(_phaseText.rectTransform, new Vector2(0f, 282f), new Vector2(980f, 42f));

            _bodyText = TourPrototypeUIFactory.CreateText(
                panel,
                "Body",
                26,
                TextAnchor.MiddleCenter,
                Color.white);
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            TourPrototypeUIFactory.SetRect(_bodyText.rectTransform, new Vector2(0f, 190f), new Vector2(940f, 130f));

            var buttonRootObject = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            _buttonRoot = buttonRootObject.GetComponent<RectTransform>();
            _buttonRoot.SetParent(panel, false);
            TourPrototypeUIFactory.SetRect(_buttonRoot, new Vector2(0f, -100f), new Vector2(900f, 430f));

            VerticalLayoutGroup layout = buttonRootObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 12f;
            layout.padding = new RectOffset(40, 40, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            _augmentPopup = PrototypeAugmentSelectionUIFactory.Create(canvas.transform);
            _augmentPopup.SetOwnedModalHost(canvas.transform);
            _augmentCoordinator = new AugmentSelectionCoordinator(_augmentPopup);
            _mapScreen = TourMapScreen.Create(canvas.transform, mapArt);
            _mapScreen.OwnedAugmentsRequested += ShowOwnedAugments;
            _mapScreen.transform.SetSiblingIndex(1);
        }

        void Refresh()
        {
            _augmentPopup?.Hide();
            ClearButtons();

            if (_manager == null || _manager.CurrentRun == null)
            {
                _mapScreen?.Hide();
                if (_mainPanel != null) _mainPanel.gameObject.SetActive(true);
                SetHeader("NO ACTIVE TOUR", "NO RUN");
                _bodyText.text = "Start a new tour from the Title scene.";
                AddButton("RETURN TO TITLE", ReturnToTitle);
                return;
            }

            TourRunState run = _manager.CurrentRun;

            if (run.phase == RunPhase.Map || run.phase == RunPhase.Travel)
            {
                if (_mainPanel != null) _mainPanel.gameObject.SetActive(false);
                _mapScreen?.Show(_manager, run);
                return;
            }

            _mapScreen?.Hide();
            if (_mainPanel != null) _mainPanel.gameObject.SetActive(true);

            switch (run.phase)
            {
                case RunPhase.Dialogue:
                    ShowDialogue();
                    break;
                case RunPhase.Performance:
                    SetHeader("PERFORMANCE READY", run.phase.ToString());
                    _bodyText.text = "The selected performance is ready.";
                    AddButton("ENTER PERFORMANCE", EnterPerformance);
                    break;
                case RunPhase.Result:
                    ShowResultFallback(run);
                    break;
                case RunPhase.Reward:
                    ShowReward();
                    break;
                case RunPhase.Completed:
                    ShowCompleted(run);
                    break;
                case RunPhase.Failed:
                    ShowFailed(run);
                    break;
            }
        }

        void ShowDialogue()
        {
            StageDefinition stage = _manager.CurrentStageDefinition;
            string displayName = stage == null ? "UNKNOWN STAGE" : stage.DisplayName;
            string dialogueId = stage == null ? "" : stage.PreDialogueId;

            SetHeader(displayName, RunPhase.Dialogue.ToString());
            _bodyText.text =
                $"The band arrives at {displayName}.\n" +
                $"Temporary dialogue: {dialogueId}";
            AddButton("CONTINUE TO PERFORMANCE", StartSelectedPerformance);
        }

        void ShowResultFallback(TourRunState run)
        {
            StageResult result = run.LatestStageResult;
            SetHeader("STAGE RESULT", run.phase.ToString());
            _bodyText.text = result == null
                ? "No stage result was found."
                : $"{result.rank}  ·  SCORE {result.score:N0}  ·  MAX COMBO {result.maxCombo}";
            AddButton("CONFIRM RESULT", ConfirmResultFromHub);
        }

        void ShowReward()
        {
            if (_mainPanel != null) _mainPanel.gameObject.SetActive(false);
            _augmentCoordinator?.Show();
        }

        void ShowOwnedAugments()
        {
            TourRunState run = _manager == null ? null : _manager.CurrentRun;
            if (run == null || run.phase != RunPhase.Map || _augmentPopup == null) return;

            AugmentCatalog catalog = AugmentCatalog.LoadDefault();
            _augmentPopup.ShowOwnedModal(AugmentOwnedViewModelBuilder.Build(run, catalog));
        }

        void ShowCompleted(TourRunState run)
        {
            SetHeader("TOUR COMPLETE", run.phase.ToString());
            _bodyText.text =
                $"TOTAL SCORE  {run.totalScore:N0}\n" +
                $"CLEARED PERFORMANCES  {run.stageResults.Count}";
            AddButton("RETURN TO TITLE", ReturnToTitle);
        }

        void ShowFailed(TourRunState run)
        {
            SetHeader("TOUR FAILED", run.phase.ToString());
            _bodyText.text = $"TOTAL SCORE  {run.totalScore:N0}\nTry the tour again from the title.";
            AddButton("RETURN TO TITLE", ReturnToTitle, danger: true);
        }

        void StartSelectedPerformance()
        {
            if (_manager.CompleteDialogue()) EnterPerformance();
        }

        void EnterPerformance()
        {
            if (!SceneLoader.IsLoading) SceneLoader.Load(MainSceneName);
        }

        void ConfirmResultFromHub()
        {
            _manager.ConfirmResult();
        }

        void ReturnToTitle()
        {
            TitleReturn.Go();
        }

        Button AddButton(string label, Action onClick, bool danger = false)
        {
            Color32 color = danger
                ? new Color32(0x9E, 0x45, 0x39, 0xFF)
                : new Color32(0x0B, 0x8A, 0x8F, 0xFF);
            return TourPrototypeUIFactory.CreateButton(_buttonRoot, label, onClick, color);
        }

        void SetHeader(string title, string phase)
        {
            _titleText.text = title;
            TourRunState run = _manager == null ? null : _manager.CurrentRun;
            _phaseText.text = run == null
                ? $"PHASE: {phase}"
                : $"PHASE: {phase}   ·   TOTAL SCORE: {run.totalScore:N0}";
        }

        void ClearButtons()
        {
            if (_buttonRoot == null) return;
            for (int i = _buttonRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = _buttonRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }

    internal static class TourPrototypeUIFactory
    {
        static Font s_font;

        static Font RuntimeFont => s_font != null
            ? s_font
            : s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;

            var eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var canvasObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static Image CreateFullscreenImage(Transform parent, Color color)
        {
            var imageObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static RectTransform CreatePanel(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position,
            Color color)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, position, size);
            panelObject.GetComponent<Image>().color = color;
            return rect;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = RuntimeFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Action onClick, Color color)
        {
            var buttonObject = new GameObject(
                "Button",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(820f, 62f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = color;

            LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 62f;
            layout.minHeight = 54f;

            Button button = buttonObject.GetComponent<Button>();
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text text = CreateText(rect, "Label", 25, TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(18f, 4f);
            text.rectTransform.offsetMax = new Vector2(-18f, -4f);
            return button;
        }

        public static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
