using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 투어 맵 선택 화면. (피그마 "맵 선택" 프레임)
    ///
    /// - 지도 위에 노드를 점으로 찍고, 노드 사이를 빗금으로 잇는다
    /// - 클리어한 노드: 핀 + 마이크 + 랭크 글자. 현재 노드: 활성 핀. 다음 노드: 클릭 가능한 회색 점
    /// - 너구리 얼굴은 좌 30° ↔ 우 30° 두 프레임을 천천히 교대, 버스는 살짝 들썩인다
    /// - 다음 노드를 클릭하면 버스가 빗금을 따라 이동하고, 도착하면 핀이 튀어나와 점멸한 뒤
    ///   NodeSelected 를 발행한다 → TourPrototypeUI 가 SelectNode 로 대화를 연다
    ///
    /// <b>씬 배치 우선.</b> Tools/Tour/Setup Tour Map Scene 이 TourHub 씬에 이 계층을 게임오브젝트로 만들어 두면
    /// (노드·핀·아이콘·버스·너구리) 인스펙터에서 위치·크기를 직접 고칠 수 있고, 런타임은 그 참조를 그대로 쓴다.
    /// 씬에 없으면 config 로 같은 계층을 코드 생성한다(폴백).
    /// 노드 위치는 씬의 Node 오브젝트 위치가 기준이고, 빗금은 그 위치를 따라 런타임에 다시 깐다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TourMapView : MonoBehaviour
    {
        /// <summary>노드 하나의 씬 오브젝트 묶음. 셋업 메뉴가 채우고 인스펙터에서 고친다.</summary>
        [Serializable]
        public sealed class NodeSlot
        {
            public RectTransform root;
            public Image dot;
            public RectTransform pinRoot;
            public Image pin;
            public Image icon;
            public Image rank;
            public Button button;

            [NonSerialized] public string NodeId;
            [NonSerialized] public int Index;
            [NonSerialized] public bool IsBoss;
            [NonSerialized] public RunNodeStatus Status;

            public bool IsComplete => root != null && dot != null && pinRoot != null && pin != null && button != null;
        }

        /// <summary>버스가 도착해 핀 점멸까지 끝났을 때. 인자는 nodeId.</summary>
        public event Action<string> NodeSelected;

        /// <summary>좌하단 "타이틀로" 버튼.</summary>
        public event Action ReturnToTitleRequested;

        [Header("설정 (스프라이트 교체·연출 수치·랭크 글자)")]
        [SerializeField] TourMapConfig config;

        [Header("씬 오브젝트 (Tools/Tour/Setup Tour Map Scene 이 채운다)")]
        [SerializeField] Image mapBackground;
        [SerializeField, Tooltip("빗금이 런타임에 이 아래에 깔린다")] RectTransform pathRoot;
        [SerializeField] RectTransform nodeRoot;
        [SerializeField] List<NodeSlot> nodeSlots = new List<NodeSlot>();
        [SerializeField, Tooltip("버스+너구리 묶음. 이 오브젝트가 노드 사이를 이동한다")] RectTransform busRoot;
        [SerializeField] RectTransform busImage;
        [SerializeField] RectTransform raccoonImage;
        [SerializeField] Button titleButton;

        RectTransform _root;
        Vector2 _busImageBase;
        Vector2 _raccoonBase;
        float _busFacing = 1f;

        readonly List<Image> _dashes = new List<Image>();

        int _busIndex;
        bool _traveling;
        bool _bound;
        Coroutine _travelRoutine;
        Coroutine _arrivalRoutine;
        float _frameTimer;
        bool _frameToggle;

        public bool IsBusy => _traveling || _arrivalRoutine != null;
        public int BusNodeIndex => _busIndex;
        public TourMapConfig Config => config;

        /// <summary>씬에 배치된 오브젝트를 쓰는지 (false 면 코드 생성 폴백).</summary>
        public bool IsSceneAuthored => busRoot != null && nodeSlots.Count > 0 && nodeSlots[0].IsComplete;

        // ---------------- 생성 · 연결 ----------------

        void Awake()
        {
            if (config == null) config = TourMapConfig.LoadDefault();
            if (IsSceneAuthored) BindSceneObjects();
        }

        /// <summary>씬에 배치된 오브젝트가 없을 때 config 의 아트로 같은 계층을 코드로 만든다.</summary>
        public void Build(TourMapConfig mapConfig, TMP_FontAsset font)
        {
            if (mapConfig != null) config = mapConfig;
            if (config == null) return;

            _root = (RectTransform)transform;
            Stretch(_root);

            mapBackground = CreateImage(_root, "MapBackground", config.Background, Color.white);
            Stretch(mapBackground.rectTransform);
            mapBackground.preserveAspect = false;
            mapBackground.raycastTarget = false;

            pathRoot = CreateRect(_root, "Path");
            Stretch(pathRoot);
            nodeRoot = CreateRect(_root, "Nodes");
            Stretch(nodeRoot);

            nodeSlots.Clear();
            int nodeCount = Mathf.Max(1, config.NodePositions.Count);
            for (int i = 0; i < nodeCount; i++) nodeSlots.Add(CreateNodeObjects(i));

            BuildBus();
            BuildTitleButton(font);
            BindSceneObjects();
        }

        /// <summary>직렬화된 씬 참조에 런타임 상태(버튼 콜백·기준 위치)를 붙인다.</summary>
        void BindSceneObjects()
        {
            if (_bound) return;
            _bound = true;
            _root = (RectTransform)transform;

            for (int i = 0; i < nodeSlots.Count; i++)
            {
                NodeSlot slot = nodeSlots[i];
                slot.Index = i;
                if (slot.button == null) continue;
                NodeSlot captured = slot;
                slot.button.onClick.AddListener(() => OnNodeClicked(captured));
            }

            if (busImage != null) _busImageBase = busImage.anchoredPosition;
            if (raccoonImage != null) _raccoonBase = raccoonImage.anchoredPosition;
            if (titleButton != null) titleButton.onClick.AddListener(() => ReturnToTitleRequested?.Invoke());

            ApplyFrame();
        }

        NodeSlot CreateNodeObjects(int index)
        {
            float scale = config.PixelScale;
            var slot = new NodeSlot { Index = index };

            slot.root = CreateRect(nodeRoot, $"Node_{index + 1:00}");
            slot.root.anchorMin = slot.root.anchorMax = new Vector2(0.5f, 0.5f);
            slot.root.sizeDelta = new Vector2(150f, 150f);
            slot.root.anchoredPosition = ConfigNodePosition(index);

            // 클릭 영역 (투명)
            Image hit = slot.root.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            slot.button = slot.root.gameObject.AddComponent<Button>();
            slot.button.transition = Selectable.Transition.None;

            slot.dot = CreateImage(slot.root, "Dot", config.Dot, config.FutureDotColor);
            slot.dot.rectTransform.sizeDelta = new Vector2(config.FutureDotSize, config.FutureDotSize);
            slot.dot.raycastTarget = false;

            // 핀: 점 위에 서 있다 (pivot 아래 중앙)
            slot.pinRoot = CreateRect(slot.root, "Pin");
            slot.pinRoot.anchorMin = slot.pinRoot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.pinRoot.pivot = new Vector2(0.5f, 0f);
            slot.pinRoot.anchoredPosition = new Vector2(0f, 6f);
            slot.pinRoot.sizeDelta = SpritePixelSize(config.PinNormal, scale, new Vector2(120f, 180f));

            slot.pin = CreateImage(slot.pinRoot, "PinSprite", config.PinNormal, Color.white);
            Stretch(slot.pin.rectTransform);
            slot.pin.preserveAspect = true;
            slot.pin.raycastTarget = false;

            slot.icon = CreateImage(slot.pinRoot, "Icon", config.MicIcon, Color.white);
            slot.icon.rectTransform.anchorMin = slot.icon.rectTransform.anchorMax = new Vector2(0.5f, 0.67f);
            slot.icon.rectTransform.sizeDelta = SpritePixelSize(config.MicIcon, scale, new Vector2(80f, 80f));
            slot.icon.rectTransform.anchoredPosition = Vector2.zero;
            slot.icon.preserveAspect = true;
            slot.icon.raycastTarget = false;

            slot.rank = CreateImage(slot.root, "Rank", null, Color.white);
            slot.rank.rectTransform.sizeDelta = new Vector2(config.RankSize, config.RankSize);
            slot.rank.rectTransform.anchoredPosition = config.RankOffset;
            slot.rank.preserveAspect = true;
            slot.rank.raycastTarget = false;
            slot.rank.enabled = false;

            slot.pinRoot.gameObject.SetActive(false);
            return slot;
        }

        void BuildBus()
        {
            float scale = config.PixelScale;

            busRoot = CreateRect(_root, "Bus");
            busRoot.anchorMin = busRoot.anchorMax = new Vector2(0.5f, 0.5f);
            busRoot.sizeDelta = Vector2.zero;

            Image bus = CreateImage(busRoot, "BusSprite", config.Bus, Color.white);
            busImage = bus.rectTransform;
            busImage.sizeDelta = SpritePixelSize(config.Bus, scale * config.BusScale, new Vector2(156f, 78f));
            busImage.anchoredPosition = Vector2.zero;
            bus.raycastTarget = false;

            Image raccoon = CreateImage(busRoot, "Raccoon", config.Raccoon, Color.white);
            raccoonImage = raccoon.rectTransform;
            raccoonImage.sizeDelta = new Vector2(config.RaccoonSize, config.RaccoonSize);
            raccoonImage.anchoredPosition = config.RaccoonOffset;
            raccoon.preserveAspect = true;
            raccoon.raycastTarget = false;
            raccoon.enabled = config.Raccoon != null;

            busRoot.anchoredPosition = ConfigNodePosition(0) + config.BusOffset;
        }

        void BuildTitleButton(TMP_FontAsset font)
        {
            var go = new GameObject("TitleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(28f, 24f);
            rect.sizeDelta = new Vector2(200f, 48f);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            titleButton = go.GetComponent<Button>();

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(rect, false);
            var label = textObject.GetComponent<TextMeshProUGUI>();
            Stretch(label.rectTransform);
            if (font != null) label.font = font;
            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = "< 타이틀로";
            label.raycastTarget = false;
        }

        // ---------------- 표시 ----------------

        /// <summary>런 상태를 읽어 노드·버스를 다시 그린다. 이동 중이던 연출은 끊는다.</summary>
        public void Show(TourRunState run, TourRunManager manager)
        {
            gameObject.SetActive(true);
            if (!_bound) BindSceneObjects();
            StopRoutines();
            if (run == null || run.map == null || config == null) return;

            EnsureNodes(run.map.nodes.Count);
            RefreshNodes(run);
            RebuildPath(run.map.nodes.Count);

            _busIndex = ResolveBusIndex(run);
            PlaceBus(NodeLocalPosition(_busIndex));
        }

        public void Hide()
        {
            StopRoutines();
            gameObject.SetActive(false);
        }

        void EnsureNodes(int count)
        {
            // 씬에 배치한 노드보다 스테이지가 많으면 부족한 만큼만 코드로 보충한다
            while (nodeSlots.Count < count)
            {
                NodeSlot slot = CreateNodeObjects(nodeSlots.Count);
                NodeSlot captured = slot;
                slot.button.onClick.AddListener(() => OnNodeClicked(captured));
                nodeSlots.Add(slot);
            }

            for (int i = 0; i < nodeSlots.Count; i++)
                if (nodeSlots[i].root != null) nodeSlots[i].root.gameObject.SetActive(i < count);
        }

        void RefreshNodes(TourRunState run)
        {
            for (int i = 0; i < run.map.nodes.Count && i < nodeSlots.Count; i++)
            {
                RunNodeState state = run.map.nodes[i];
                NodeSlot node = nodeSlots[i];
                node.NodeId = state.nodeId;
                node.Index = i;
                node.Status = state.status;
                node.IsBoss = state.nodeType == RunNodeType.ElitePerformance;
                if (node.icon != null)
                {
                    node.icon.sprite = node.IsBoss ? config.BossIcon : config.MicIcon;
                    node.icon.enabled = node.icon.sprite != null;
                }
                if (node.pinRoot != null) node.pinRoot.localScale = Vector3.one;

                switch (state.status)
                {
                    case RunNodeStatus.Cleared:
                    case RunNodeStatus.Failed:
                        SetDot(node, config.VisitedDotSize, config.VisitedDotColor);
                        SetPin(node, true, config.PinNormal);
                        SetRank(node, FindRank(run, state.nodeId));
                        SetInteractable(node, false);
                        break;

                    case RunNodeStatus.Current:
                        SetDot(node, config.VisitedDotSize, config.VisitedDotColor);
                        SetPin(node, true, config.PinHover);
                        SetRank(node, null);
                        SetInteractable(node, false);
                        break;

                    case RunNodeStatus.Available:
                        SetDot(node, config.FutureDotSize, config.FutureDotColor);
                        SetPin(node, false, config.PinNormal);
                        SetRank(node, null);
                        SetInteractable(node, true);
                        break;

                    default: // Locked
                        SetDot(node, config.FutureDotSize, config.LockedDotColor);
                        SetPin(node, false, config.PinNormal);
                        SetRank(node, null);
                        SetInteractable(node, false);
                        break;
                }
            }
        }

        static void SetDot(NodeSlot node, float size, Color color)
        {
            if (node.dot == null) return;
            node.dot.rectTransform.sizeDelta = new Vector2(size, size);
            node.dot.color = color;
        }

        static void SetPin(NodeSlot node, bool visible, Sprite sprite)
        {
            if (node.pinRoot != null) node.pinRoot.gameObject.SetActive(visible);
            if (node.pin != null && sprite != null) node.pin.sprite = sprite;
        }

        static void SetInteractable(NodeSlot node, bool value)
        {
            if (node.button != null) node.button.interactable = value;
        }

        void SetRank(NodeSlot node, string label)
        {
            if (node.rank == null) return;
            Sprite sprite = config.GetRankSprite(label);
            node.rank.sprite = sprite;
            node.rank.enabled = sprite != null;
        }

        static string FindRank(TourRunState run, string nodeId)
        {
            if (run.stageResults == null) return null;
            for (int i = run.stageResults.Count - 1; i >= 0; i--)
            {
                StageResult result = run.stageResults[i];
                if (result != null && string.Equals(result.nodeId, nodeId, StringComparison.Ordinal))
                    return result.rank;
            }
            return null;
        }

        /// <summary>
        /// 버스가 서 있을 노드: 현재 노드 → 선택 가능한(Available) 노드 → 마지막으로 클리어한 노드 → 첫 노드.
        /// (증강 선택 뒤 Travel 단계에서 이미 다음 노드까지 이동했으므로, Map 단계의 Available 노드에 버스가 서 있다)
        /// </summary>
        static int ResolveBusIndex(TourRunState run)
        {
            int available = -1;
            int last = 0;
            for (int i = 0; i < run.map.nodes.Count; i++)
            {
                RunNodeStatus status = run.map.nodes[i].status;
                if (status == RunNodeStatus.Current) return i;
                if (status == RunNodeStatus.Available && available < 0) available = i;
                if (status == RunNodeStatus.Cleared || status == RunNodeStatus.Failed) last = i;
            }
            return available >= 0 ? available : last;
        }

        /// <summary>
        /// Travel 단계(증강 선택 직후) 연출: travelFrom → travelTo 로 버스가 이동한 뒤 onComplete 를 부른다.
        /// 호출자가 onComplete 에서 TourRunManager.CompleteTravel() 을 불러 Map 단계로 넘긴다.
        /// </summary>
        public void PlayTravel(TourRunState run, Action onComplete)
        {
            StopRoutines();
            int from = FindNodeIndex(run, run.travelFromNodeId);
            int to = FindNodeIndex(run, run.travelToNodeId);
            if (from < 0 || to < 0)
            {
                onComplete?.Invoke();
                return;
            }

            _busIndex = from;
            PlaceBus(NodeLocalPosition(from));
            _travelRoutine = StartCoroutine(TravelRoutine(to, onComplete));
        }

        static int FindNodeIndex(TourRunState run, string nodeId)
        {
            if (run?.map?.nodes == null || string.IsNullOrEmpty(nodeId)) return -1;
            for (int i = 0; i < run.map.nodes.Count; i++)
                if (string.Equals(run.map.nodes[i].nodeId, nodeId, StringComparison.Ordinal)) return i;
            return -1;
        }

        IEnumerator TravelRoutine(int targetIndex, Action onComplete)
        {
            _traveling = true;
            SetNodesInteractable(false);
            if (config.TravelStartDelay > 0f)
                yield return new WaitForSecondsRealtime(config.TravelStartDelay);

            yield return MoveBusTo(targetIndex);

            _traveling = false;
            _travelRoutine = null;
            onComplete?.Invoke();
        }

        IEnumerator MoveBusTo(int targetIndex)
        {
            int step = targetIndex > _busIndex ? 1 : -1;
            while (_busIndex != targetIndex)
            {
                int next = _busIndex + step;
                Vector2 from = NodeLocalPosition(_busIndex);
                Vector2 to = NodeLocalPosition(next);
                _busFacing = to.x >= from.x ? 1f : -1f;

                float duration = Mathf.Max(0.1f, config.TravelSecondsPerSegment);
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    PlaceBus(Vector2.Lerp(from, to, Mathf.Clamp01(elapsed / duration))); // Linear
                    yield return null;
                }

                PlaceBus(to);
                _busIndex = next;
            }
        }

        void RebuildPath(int nodeCount)
        {
            for (int i = 0; i < _dashes.Count; i++)
                if (_dashes[i] != null) Destroy(_dashes[i].gameObject);
            _dashes.Clear();
            if (pathRoot == null) return;

            for (int i = 0; i < nodeCount - 1; i++)
            {
                Vector2 from = NodeLocalPosition(i);
                Vector2 to = NodeLocalPosition(i + 1);
                float length = Vector2.Distance(from, to);
                int count = Mathf.FloorToInt(length / Mathf.Max(4f, config.DashSpacing));
                Vector2 direction = (to - from).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                for (int k = 1; k < count; k++)
                {
                    Image dash = CreateImage(pathRoot, $"Dash_{i}_{k}", config.Dot, config.DashColor);
                    dash.rectTransform.anchorMin = dash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    dash.rectTransform.sizeDelta = config.DashSize;
                    dash.rectTransform.anchoredPosition = from + direction * (k * config.DashSpacing);
                    dash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle); // 경로 방향 빗금
                    dash.raycastTarget = false;
                    _dashes.Add(dash);
                }
            }
        }

        // ---------------- 입력·이동 ----------------

        void OnNodeClicked(NodeSlot node)
        {
            if (IsBusy || node.Status != RunNodeStatus.Available) return;
            _travelRoutine = StartCoroutine(TravelAndSelect(node));
        }

        /// <summary>노드 클릭과 같은 동작을 코드로 일으킨다 (디버그·자동 테스트용).</summary>
        public bool TryTravelTo(string nodeId)
        {
            for (int i = 0; i < nodeSlots.Count; i++)
            {
                NodeSlot node = nodeSlots[i];
                if (!string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)) continue;
                if (IsBusy || node.Status != RunNodeStatus.Available) return false;
                _travelRoutine = StartCoroutine(TravelAndSelect(node));
                return true;
            }
            return false;
        }

        IEnumerator TravelAndSelect(NodeSlot target)
        {
            _traveling = true;
            SetNodesInteractable(false);

            // 버스가 이미 그 노드에 있으면(Travel 단계로 먼저 이동한 경우) 바로 도착 연출로 간다
            if (_busIndex != target.Index)
            {
                if (config.TravelStartDelay > 0f)
                    yield return new WaitForSecondsRealtime(config.TravelStartDelay);
                yield return MoveBusTo(target.Index);
            }

            _traveling = false;
            _travelRoutine = null;
            _arrivalRoutine = StartCoroutine(ArrivalBlink(target));
        }

        IEnumerator ArrivalBlink(NodeSlot node)
        {
            SetDot(node, config.VisitedDotSize, config.VisitedDotColor);
            SetPin(node, true, config.PinHover);

            // 튀어나오기 (Ease out)
            float pop = Mathf.Max(0.01f, config.PinPopDuration);
            float elapsed = 0f;
            while (elapsed < pop)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / pop);
                float eased = 1f - (1f - t) * (1f - t);
                if (node.pinRoot != null) node.pinRoot.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, eased);
                yield return null;
            }
            if (node.pinRoot != null) node.pinRoot.localScale = Vector3.one;

            // 점멸: 활성 핀 ↔ 기본 핀
            float total = config.ArrivalBlinkDuration;
            float interval = Mathf.Max(0.05f, config.PinBlinkInterval);
            float blinked = 0f;
            bool active = true;
            while (blinked < total)
            {
                yield return new WaitForSecondsRealtime(interval);
                blinked += interval;
                active = !active;
                if (node.pin != null) node.pin.sprite = active ? config.PinHover : config.PinNormal;
            }
            if (node.pin != null) node.pin.sprite = config.PinHover;

            _arrivalRoutine = null;
            NodeSelected?.Invoke(node.NodeId);
        }

        void SetNodesInteractable(bool value)
        {
            for (int i = 0; i < nodeSlots.Count; i++)
                SetInteractable(nodeSlots[i], value && nodeSlots[i].Status == RunNodeStatus.Available);
        }

        void StopRoutines()
        {
            if (_travelRoutine != null) StopCoroutine(_travelRoutine);
            if (_arrivalRoutine != null) StopCoroutine(_arrivalRoutine);
            _travelRoutine = null;
            _arrivalRoutine = null;
            _traveling = false;
        }

        // ---------------- 2프레임 애니메이션 ----------------

        /// <summary>너구리 얼굴: 좌 30° ↔ 우 30° 두 프레임을 천천히 반복. 버스는 살짝 들썩인다.</summary>
        void Update()
        {
            if (config == null || busRoot == null) return;

            _frameTimer += Time.unscaledDeltaTime;
            if (_frameTimer < config.FrameInterval) return;
            _frameTimer = 0f;
            _frameToggle = !_frameToggle;
            ApplyFrame();
        }

        void ApplyFrame()
        {
            if (config == null) return;
            float tilt = config.RaccoonTiltDegrees;
            if (raccoonImage != null)
                raccoonImage.localRotation = Quaternion.Euler(0f, 0f, _frameToggle ? -tilt : tilt);

            float bob = config.BobPixels;
            if (busImage != null)
                busImage.anchoredPosition = _busImageBase + new Vector2(0f, _frameToggle ? 0f : bob);
        }

        void PlaceBus(Vector2 localPosition)
        {
            if (busRoot == null) return;
            busRoot.anchoredPosition = localPosition + config.BusOffset;
            if (config.FlipBusByDirection && busImage != null)
                busImage.localScale = new Vector3(_busFacing, 1f, 1f);
        }

        // ---------------- 좌표·헬퍼 ----------------

        /// <summary>노드 위치. 씬에 배치된 노드는 그 오브젝트 위치, 없으면 config 좌표.</summary>
        Vector2 NodeLocalPosition(int index)
        {
            if (index >= 0 && index < nodeSlots.Count && nodeSlots[index].root != null)
                return nodeSlots[index].root.anchoredPosition;
            return ConfigNodePosition(index);
        }

        Vector2 ConfigNodePosition(int index)
        {
            Vector2 normalized = config.GetNodePosition(index);
            Vector2 size = config.ReferenceResolution;
            return new Vector2((normalized.x - 0.5f) * size.x, (normalized.y - 0.5f) * size.y);
        }

        static Vector2 SpritePixelSize(Sprite sprite, float scale, Vector2 fallback)
        {
            if (sprite == null) return fallback;
            return new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
        }

        static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.enabled = sprite != null;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] 씬에 계층을 만들고 직렬화 참조를 채운다. 에디트 모드에서 호출.</summary>
        public void EditorBuildSceneObjects(TourMapConfig mapConfig, TMP_FontAsset font)
        {
            _bound = false;
            Build(mapConfig, font);
            // 에디트 모드에서는 버튼 콜백을 붙이지 않는다 (직렬화되지 않음). Awake 가 다시 붙인다
            _bound = false;
            for (int i = 0; i < nodeSlots.Count; i++)
                if (nodeSlots[i].button != null) nodeSlots[i].button.onClick.RemoveAllListeners();
            if (titleButton != null) titleButton.onClick.RemoveAllListeners();
        }
#endif
    }
}
