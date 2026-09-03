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

        Text _titleText;
        Text _phaseText;
        Text _bodyText;
        RectTransform _mainPanel;
        RectTransform _buttonRoot;
        TourRunManager _manager;
        AugmentSelectionPopup _augmentPopup;
        AugmentSelectionCoordinator _augmentCoordinator;
        Image _background;
        TourMapView _mapView;
        bool _dialoguePlaying;

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
        }

        void OnDestroy()
        {
            _augmentCoordinator?.Dispose();
            if (_mapView != null)
            {
                _mapView.NodeSelected -= OnMapNodeSelected;
                _mapView.ReturnToTitleRequested -= ReturnToTitle;
            }
        }

        void BuildUI()
        {
            Canvas canvas = TourPrototypeUIFactory.CreateCanvas("TourPrototypeCanvas", 100);
            _background = TourPrototypeUIFactory.CreateFullscreenImage(
                canvas.transform,
                new Color32(0x16, 0x12, 0x1C, 0xFF));

            // 아트 맵 화면: 씬에 배치된 TourMapView(Tools/Tour/Setup Tour Map Scene)를 우선 쓰고,
            // 없으면 Resources/Tour/TourMapConfig 로 코드 생성한다
            _mapView = FindFirstObjectByType<TourMapView>(FindObjectsInactive.Include);
            if (_mapView == null)
            {
                TourMapConfig mapConfig = TourMapConfig.LoadDefault();
                if (mapConfig != null && mapConfig.HasArt)
                {
                    var mapObject = new GameObject("TourMap", typeof(RectTransform));
                    mapObject.transform.SetParent(canvas.transform, false);
                    _mapView = mapObject.AddComponent<TourMapView>();
                    DialogueCatalog dialogueCatalog = DialogueCatalog.LoadDefault();
                    _mapView.Build(
                        mapConfig,
                        dialogueCatalog != null && dialogueCatalog.Style != null ? dialogueCatalog.Style.Font : null);
                }
            }

            if (_mapView != null)
            {
                _mapView.NodeSelected += OnMapNodeSelected;
                _mapView.ReturnToTitleRequested += ReturnToTitle;
                _mapView.Hide();
            }

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
            _augmentCoordinator = new AugmentSelectionCoordinator(_augmentPopup);
        }

        void Refresh()
        {
            CancelStageDialogueIfLeft();
            if (_mainPanel != null) _mainPanel.gameObject.SetActive(true);
            _augmentPopup?.Hide();
            ClearButtons();

            if (_manager == null || _manager.CurrentRun == null)
            {
                SetHeader("NO ACTIVE TOUR", "NO RUN");
                _bodyText.text = "Start a new tour from the Title scene.";
                AddButton("RETURN TO TITLE", ReturnToTitle);
                return;
            }

            TourRunState run = _manager.CurrentRun;
            UpdateMapVisibility(run);

            switch (run.phase)
            {
                case RunPhase.Map:
                    if (_mapView != null)
                    {
                        // 아트 맵: 노드 클릭 → 버스 이동 → 핀 점멸 → OnMapNodeSelected
                        if (_mainPanel != null) _mainPanel.gameObject.SetActive(false);
                        _mapView.Show(run, _manager);
                    }
                    else ShowMap(run);
                    break;
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

        /// <summary>맵은 Map·Dialogue 단계에만 보인다 (대화창은 맵 위에 블러로 뜬다).</summary>
        void UpdateMapVisibility(TourRunState run)
        {
            if (_mapView == null) return;

            bool showMap = run != null && (run.phase == RunPhase.Map || run.phase == RunPhase.Dialogue);
            if (_background != null) _background.enabled = !showMap;
            if (!showMap) _mapView.Hide();
            else if (run.phase == RunPhase.Dialogue && !_mapView.gameObject.activeSelf) _mapView.Show(run, _manager);
        }

        void OnMapNodeSelected(string nodeId)
        {
            if (_manager == null) return;
            _manager.SelectNode(nodeId);
        }

        void ShowMap(TourRunState run)
        {
            SetHeader("TOUR MAP", run.phase.ToString());
            _bodyText.text = "Select the next available performance.";

            for (int i = 0; i < run.map.nodes.Count; i++)
            {
                RunNodeState node = run.map.nodes[i];
                StageDefinition stage = _manager.FindStageDefinition(node.stageId);
                string displayName = stage == null ? node.stageId : stage.DisplayName;
                string prefix = node.nodeType == RunNodeType.ElitePerformance ? "ELITE" : $"STAGE {i + 1}";
                string label = $"{prefix}  ·  {displayName}  [{node.status}]";
                string selectedNodeId = node.nodeId;
                Button button = AddButton(label, () => _manager.SelectNode(selectedNodeId));
                button.interactable = node.status == RunNodeStatus.Available;
                if (!button.interactable)
                    button.GetComponent<Image>().color = new Color32(0x3E, 0x35, 0x46, 0xFF);
            }

            AddButton("ABANDON TOUR", ReturnToTitle, danger: true);
        }

        void ShowDialogue()
        {
            StageDefinition stage = _manager.CurrentStageDefinition;
            string displayName = stage == null ? "UNKNOWN STAGE" : stage.DisplayName;
            string dialogueId = stage == null ? "" : stage.PreDialogueId;

            // 실제 대화 시퀀스(DialogueCatalog)가 있으면 대화창으로 진행한다.
            if (!_dialoguePlaying && TryPlayStageDialogue(stage))
            {
                if (_mainPanel != null) _mainPanel.gameObject.SetActive(false);
                return;
            }

            // 시퀀스가 없을 때의 임시 폴백 (Tools/Dialogue/Setup Dialogue Data 실행 전)
            SetHeader(displayName, RunPhase.Dialogue.ToString());
            _bodyText.text =
                $"The band arrives at {displayName}.\n" +
                $"Temporary dialogue: {dialogueId}";
            AddButton("CONTINUE TO PERFORMANCE", StartSelectedPerformance);
        }

        bool TryPlayStageDialogue(StageDefinition stage)
        {
            if (stage == null) return false;

            var context = new DialoguePresentationContext
            {
                venueName = stage.DisplayName,
                confirmLabel = "공연 시작",
            };

            if (StageVisualCatalog.TryResolve(stage.StageId, out StageVisualEntry visual))
            {
                context.backdrop = visual.backgroundBase;
                context.ruleIcon = visual.ruleIcon;
                context.ruleTitle = visual.ruleTitle;
                context.ruleBody = visual.ruleBody;
            }

            if (!Dialogue.TryPlay(stage.PreDialogueId, context, OnStageDialogueFinished)) return false;
            _dialoguePlaying = true;
            return true;
        }

        void OnStageDialogueFinished()
        {
            _dialoguePlaying = false;
            StartSelectedPerformance();
        }

        /// <summary>Dialogue 단계가 아닌데 대화창이 남아 있으면(디버그 진행 등) 조용히 닫는다.</summary>
        void CancelStageDialogueIfLeft()
        {
            TourRunState run = _manager == null ? null : _manager.CurrentRun;
            if (run != null && run.phase == RunPhase.Dialogue) return;
            _dialoguePlaying = false;
            Dialogue.HideImmediate();
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
