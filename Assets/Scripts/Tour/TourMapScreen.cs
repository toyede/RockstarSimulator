using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    [Serializable]
    public sealed class TourMapArt
    {
        [SerializeField] private Font font;
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite ownedAugmentIcon;
        [SerializeField] private Sprite pinNormal;
        [SerializeField] private Sprite pinHover;
        [SerializeField] private Sprite mapDot;
        [SerializeField] private Sprite stageIcon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Sprite bus;
        [SerializeField] private Sprite raccoon;
        [SerializeField] private Sprite rankS;
        [SerializeField] private Sprite rankA;
        [SerializeField] private Sprite rankB;
        [SerializeField] private Sprite rankC;
        [SerializeField] private Sprite rankD;
        [SerializeField] private Sprite rankF;

        public Font Font => font;
        public Sprite Background => background;
        public Sprite OwnedAugmentIcon => ownedAugmentIcon;
        public Sprite PinNormal => pinNormal;
        public Sprite PinHover => pinHover;
        public Sprite MapDot => mapDot;
        public Sprite StageIcon => stageIcon;
        public Sprite BossIcon => bossIcon;
        public Sprite Bus => bus;
        public Sprite Raccoon => raccoon;

        public Sprite GetRankSprite(string rank)
        {
            switch ((rank ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "S": return rankS;
                case "A": return rankA;
                case "B": return rankB;
                case "C": return rankC;
                case "D": return rankD;
                case "F": return rankF;
                default: return null;
            }
        }
    }

    public sealed class TourMapScreen : MonoBehaviour
    {
        const int StageCount = 4;
        const float MapEntryDelay = 0.7f;
        const float TravelDuration = 3f;
        const float PinDelay = 0.1f;
        const float PinRevealDuration = 0.2f;
        const float BusPoseDuration = 0.4f;

        static readonly Vector2[] PinTopLeft =
        {
            new Vector2(386f, 468f),
            new Vector2(709f, 335f),
            new Vector2(1033f, 468f),
            new Vector2(1344f, 335f)
        };

        static readonly Vector2[] BusTopLeft =
        {
            new Vector2(438f, 601f),
            new Vector2(759f, 468f),
            new Vector2(1083f, 601f),
            new Vector2(1398f, 468f)
        };

        static readonly Vector2[] DotTopLeft =
        {
            new Vector2(457f, 671f),
            new Vector2(780f, 538f),
            new Vector2(1104f, 671f),
            new Vector2(1398f, 521f)
        };

        static readonly Vector2[] DotSize =
        {
            new Vector2(31f, 31f),
            new Vector2(31f, 31f),
            new Vector2(31f, 31f),
            new Vector2(65f, 65f)
        };

        static readonly Vector2[] RankTopLeft =
        {
            new Vector2(494f, 558f),
            new Vector2(822f, 431f),
            new Vector2(1147f, 555f),
            new Vector2(1457f, 431f)
        };

        TourMapArt _art;
        TourRunManager _manager;
        TourRunState _run;
        RectTransform _root;
        RectTransform _busRoot;
        RectTransform _busImageRect;
        RectTransform _raccoonImageRect;
        Button _ownedButton;
        readonly TourStagePinView[] _pins = new TourStagePinView[StageCount];
        readonly Image[] _dots = new Image[StageCount];
        readonly List<Image>[] _routeSegments = new List<Image>[StageCount - 1];
        Coroutine _travelRoutine;
        Coroutine _busPoseRoutine;
        string _travelKey = string.Empty;

        public event Action OwnedAugmentsRequested;

        public static TourMapScreen Create(Transform parent, TourMapArt art)
        {
            var screenObject = new GameObject(
                "TourMapScreen",
                typeof(RectTransform),
                typeof(TourMapScreen));
            TourMapScreen screen = screenObject.GetComponent<TourMapScreen>();
            screen._art = art ?? new TourMapArt();
            screen.Build(parent);
            screen.gameObject.SetActive(false);
            return screen;
        }

        public void Show(TourRunManager manager, TourRunState run)
        {
            _manager = manager;
            _run = run;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (_busPoseRoutine == null) _busPoseRoutine = StartCoroutine(PlayBusPose());

            if (run == null)
            {
                Hide();
                return;
            }

            _ownedButton.interactable = run.phase == RunPhase.Map;

            if (run.phase == RunPhase.Travel)
            {
                string key = $"{run.travelFromNodeId}->{run.travelToNodeId}";
                if (_travelRoutine != null && string.Equals(_travelKey, key, StringComparison.Ordinal))
                    return;

                StopTravel();
                _travelKey = key;
                _travelRoutine = StartCoroutine(PlayTravel(run));
                return;
            }

            StopTravel();
            ApplyState(run, FindBusIndex(run));
        }

        public void Hide()
        {
            StopTravel();
            if (_busPoseRoutine != null)
            {
                StopCoroutine(_busPoseRoutine);
                _busPoseRoutine = null;
            }

            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        void Build(Transform parent)
        {
            _root = GetComponent<RectTransform>();
            _root.SetParent(parent, false);
            Stretch(_root);

            Image background = CreateImage(_root, "MapBackground", Vector2.zero, new Vector2(1920f, 1080f), _art.Background);
            background.preserveAspect = false;

            BuildRoute();

            for (int i = 0; i < StageCount; i++)
            {
                _pins[i] = new TourStagePinView(
                    _root,
                    PinTopLeft[i],
                    RankTopLeft[i],
                    _art,
                    OnStageClicked);
            }

            BuildBus();
            BuildOwnedButton();
        }

        void BuildRoute()
        {
            for (int i = 0; i < StageCount; i++)
            {
                _dots[i] = CreateImage(
                    _root,
                    $"RouteDot_{i + 1}",
                    DotTopLeft[i],
                    DotSize[i],
                    _art.MapDot);
                _dots[i].preserveAspect = true;
                _dots[i].color = new Color(1f, 1f, 1f, 0.5f);
            }

            for (int i = 0; i < StageCount - 1; i++)
            {
                Vector2 start = DotTopLeft[i] + DotSize[i] * 0.5f;
                Vector2 end = DotTopLeft[i + 1] + DotSize[i + 1] * 0.5f;
                _routeSegments[i] = CreateDashedLine(_root, $"Route_{i + 1}_{i + 2}", start, end);
            }
        }

        void BuildBus()
        {
            _busRoot = CreateRect(_root, "RaccoonBus", BusTopLeft[0], new Vector2(201f, 201f));

            Image busImage = CreateImage(_busRoot, "Bus", new Vector2(2f, 95f), new Vector2(168f, 84f), _art.Bus);
            busImage.preserveAspect = true;
            _busImageRect = busImage.rectTransform;

            Image raccoonImage = CreateImage(_busRoot, "Raccoon", new Vector2(45f, 9f), new Vector2(144f, 144f), _art.Raccoon);
            raccoonImage.preserveAspect = true;
            _raccoonImageRect = raccoonImage.rectTransform;
            ApplyBusPose(false);
        }

        void BuildOwnedButton()
        {
            var buttonObject = new GameObject(
                "OwnedAugmentsButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(_root, false);
            SetTopLeft(rect, new Vector2(49f, 51f), new Vector2(390f, 78f));

            Image hitArea = buttonObject.GetComponent<Image>();
            hitArea.color = Color.clear;

            _ownedButton = buttonObject.GetComponent<Button>();
            _ownedButton.targetGraphic = hitArea;
            _ownedButton.onClick.AddListener(OnOwnedAugmentsClicked);

            Image icon = CreateImage(rect, "Icon", Vector2.zero, new Vector2(78f, 78f), _art.OwnedAugmentIcon);
            icon.preserveAspect = true;

            Text label = CreateText(rect, "Label", _art.Font, 24, TextAnchor.MiddleLeft, Color.white);
            SetTopLeft(label.rectTransform, new Vector2(82f, 0f), new Vector2(308f, 78f));
            label.text = "보유 증강 확인하기";
        }

        IEnumerator PlayTravel(TourRunState run)
        {
            int fromIndex = FindNodeIndex(run, run.travelFromNodeId);
            int toIndex = FindNodeIndex(run, run.travelToNodeId);
            if (fromIndex < 0 || toIndex < 0 || fromIndex >= StageCount || toIndex >= StageCount)
            {
                Debug.LogWarning("[TourMap] 이동 노드가 Figma 지도 범위를 벗어났습니다.", this);
                _travelRoutine = null;
                _manager?.CompleteTravel();
                yield break;
            }

            ApplyState(run, fromIndex);
            StartCoroutine(_pins[fromIndex].RevealRank());
            yield return WaitUnscaled(MapEntryDelay);

            Vector2 start = ToAnchored(BusTopLeft[fromIndex]);
            Vector2 end = ToAnchored(BusTopLeft[toIndex]);
            float elapsed = 0f;
            while (elapsed < TravelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / TravelDuration);
                _busRoot.anchoredPosition = Vector2.LerpUnclamped(start, end, progress);
                yield return null;
            }

            _busRoot.anchoredPosition = end;
            yield return WaitUnscaled(PinDelay);
            yield return _pins[toIndex].RevealAvailable(PinRevealDuration);

            _travelRoutine = null;
            _travelKey = string.Empty;
            _manager?.CompleteTravel();
        }

        IEnumerator PlayBusPose()
        {
            bool alternate = false;
            while (true)
            {
                ApplyBusPose(alternate);
                alternate = !alternate;
                yield return WaitUnscaled(BusPoseDuration);
            }
        }

        void ApplyBusPose(bool alternate)
        {
            if (_busImageRect != null)
                _busImageRect.localEulerAngles = new Vector3(0f, 0f, alternate ? 5f : -5f);
            if (_raccoonImageRect != null)
                _raccoonImageRect.localEulerAngles = new Vector3(0f, 0f, alternate ? -5f : 5f);
        }

        void ApplyState(TourRunState run, int busIndex)
        {
            if (run?.map?.nodes == null) return;

            int visibleCount = Mathf.Min(StageCount, run.map.nodes.Count);
            for (int i = 0; i < StageCount; i++)
            {
                RunNodeState node = i < visibleCount ? run.map.nodes[i] : null;
                StageResult result = node == null ? null : run.FindStageResult(node.nodeId);
                _pins[i].Bind(node, result);

                bool reached = i <= busIndex;
                _dots[i].color = new Color(1f, 1f, 1f, reached ? 1f : 0.5f);
            }

            int completedSegmentCount = Mathf.Max(1, busIndex);
            for (int i = 0; i < _routeSegments.Length; i++)
            {
                float alpha = i < completedSegmentCount ? 0.95f : 0.45f;
                for (int j = 0; j < _routeSegments[i].Count; j++)
                    _routeSegments[i][j].color = new Color(1f, 1f, 1f, alpha);
            }

            int safeBusIndex = Mathf.Clamp(busIndex, 0, StageCount - 1);
            _busRoot.anchoredPosition = ToAnchored(BusTopLeft[safeBusIndex]);
            _busRoot.SetAsLastSibling();
            _ownedButton.transform.SetAsLastSibling();
        }

        int FindBusIndex(TourRunState run)
        {
            if (run?.map?.nodes == null) return 0;

            for (int i = 0; i < run.map.nodes.Count && i < StageCount; i++)
            {
                if (run.map.nodes[i].status == RunNodeStatus.Available)
                    return i;
            }

            for (int i = Mathf.Min(StageCount, run.map.nodes.Count) - 1; i >= 0; i--)
            {
                if (run.map.nodes[i].status == RunNodeStatus.Cleared)
                    return i;
            }

            return 0;
        }

        static int FindNodeIndex(TourRunState run, string nodeId)
        {
            if (run?.map?.nodes == null || string.IsNullOrWhiteSpace(nodeId)) return -1;

            for (int i = 0; i < run.map.nodes.Count; i++)
            {
                if (string.Equals(run.map.nodes[i]?.nodeId, nodeId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        void OnStageClicked(string nodeId)
        {
            if (_run?.phase != RunPhase.Map || _manager == null) return;
            _manager.SelectNode(nodeId);
        }

        void OnOwnedAugmentsClicked()
        {
            if (_run?.phase != RunPhase.Map) return;
            OwnedAugmentsRequested?.Invoke();
        }

        void StopTravel()
        {
            if (_travelRoutine != null)
            {
                StopCoroutine(_travelRoutine);
                _travelRoutine = null;
            }
            _travelKey = string.Empty;
        }

        static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        static List<Image> CreateDashedLine(
            Transform parent,
            string name,
            Vector2 start,
            Vector2 end)
        {
            RectTransform root = CreateRect(parent, name, Vector2.zero, new Vector2(1920f, 1080f));
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;

            const float dashLength = 9f;
            const float gapLength = 12f;
            Vector2 delta = end - start;
            float distance = delta.magnitude;
            Vector2 direction = distance <= Mathf.Epsilon ? Vector2.right : delta / distance;
            float angle = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg;
            var images = new List<Image>();

            for (float offset = 26f; offset < distance - 26f; offset += dashLength + gapLength)
            {
                float length = Mathf.Min(dashLength, distance - 26f - offset);
                Vector2 center = start + direction * (offset + length * 0.5f);
                var dashObject = new GameObject(
                    $"Dash_{images.Count + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform dashRect = dashObject.GetComponent<RectTransform>();
                dashRect.SetParent(root, false);
                dashRect.anchorMin = new Vector2(0f, 1f);
                dashRect.anchorMax = new Vector2(0f, 1f);
                dashRect.pivot = new Vector2(0.5f, 0.5f);
                dashRect.anchoredPosition = ToAnchored(center);
                dashRect.sizeDelta = new Vector2(length, 2f);
                dashRect.localEulerAngles = new Vector3(0f, 0f, angle);

                Image image = dashObject.GetComponent<Image>();
                image.raycastTarget = false;
                images.Add(image);
            }

            return images;
        }

        static Image CreateImage(
            Transform parent,
            string name,
            Vector2 topLeft,
            Vector2 size,
            Sprite sprite)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, topLeft, size);

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            var textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = font != null
                ? font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 topLeft,
            Vector2 size)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeft(rect, topLeft, size);
            return rect;
        }

        static void SetTopLeft(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = ToAnchored(topLeft);
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        static Vector2 ToAnchored(Vector2 topLeft) => new Vector2(topLeft.x, -topLeft.y);

        sealed class TourStagePinView
        {
            readonly TourMapArt _art;
            readonly RectTransform _visual;
            readonly Image _pinImage;
            readonly Image _iconImage;
            readonly Image _rankImage;
            readonly CanvasGroup _rankGroup;
            readonly Button _button;
            readonly Action<string> _onClick;
            string _nodeId = string.Empty;

            public TourStagePinView(
                Transform parent,
                Vector2 pinTopLeft,
                Vector2 rankTopLeft,
                TourMapArt art,
                Action<string> onClick)
            {
                _art = art;
                _onClick = onClick;

                var rootObject = new GameObject(
                    "StagePin",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                RectTransform root = rootObject.GetComponent<RectTransform>();
                root.SetParent(parent, false);
                SetTopLeft(root, pinTopLeft, new Vector2(173f, 253f));

                Image hitArea = rootObject.GetComponent<Image>();
                hitArea.color = Color.clear;

                _button = rootObject.GetComponent<Button>();
                _button.transition = Selectable.Transition.SpriteSwap;
                _button.onClick.AddListener(HandleClick);

                _visual = CreateRect(root, "PinVisual", Vector2.zero, new Vector2(173f, 219f));
                _visual.anchorMin = new Vector2(0f, 1f);
                _visual.anchorMax = new Vector2(0f, 1f);
                _visual.pivot = new Vector2(0.5f, 1f);
                _visual.anchoredPosition = new Vector2(86.5f, 0f);

                _pinImage = CreateImage(_visual, "Pin", new Vector2(20f, 19f), new Vector2(133f, 200f), _art.PinNormal);
                _pinImage.preserveAspect = true;
                _iconImage = CreateImage(_visual, "Icon", new Vector2(50.5f, 47f), new Vector2(72f, 72f), _art.StageIcon);
                _iconImage.preserveAspect = true;

                Image dot = CreateImage(root, "Dot", new Vector2(71f, 203f), new Vector2(31f, 31f), _art.MapDot);
                dot.preserveAspect = true;

                var rankObject = new GameObject(
                    "StageRank",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(CanvasGroup));
                RectTransform rankRect = rankObject.GetComponent<RectTransform>();
                rankRect.SetParent(parent, false);
                SetTopLeft(rankRect, rankTopLeft, new Vector2(90f, 90f));
                _rankImage = rankObject.GetComponent<Image>();
                _rankImage.preserveAspect = true;
                _rankImage.raycastTarget = false;
                _rankGroup = rankObject.GetComponent<CanvasGroup>();

                _button.targetGraphic = _pinImage;
                SpriteState spriteState = _button.spriteState;
                spriteState.highlightedSprite = _art.PinHover;
                spriteState.pressedSprite = _art.PinHover;
                spriteState.selectedSprite = _art.PinHover;
                _button.spriteState = spriteState;
            }

            public void Bind(RunNodeState node, StageResult result)
            {
                _nodeId = node?.nodeId ?? string.Empty;
                bool hasNode = node != null;
                bool showPin = hasNode &&
                               (node.status == RunNodeStatus.Available ||
                                node.status == RunNodeStatus.Current ||
                                node.status == RunNodeStatus.Cleared);

                _visual.gameObject.SetActive(showPin);
                _visual.localScale = Vector3.one;
                _button.interactable = hasNode && node.status == RunNodeStatus.Available;
                _iconImage.sprite = hasNode && node.nodeType == RunNodeType.ElitePerformance
                    ? _art.BossIcon
                    : _art.StageIcon;

                Sprite rankSprite = result != null && result.cleared
                    ? _art.GetRankSprite(result.rank)
                    : null;
                _rankImage.sprite = rankSprite;
                _rankImage.gameObject.SetActive(
                    hasNode && node.status == RunNodeStatus.Cleared && rankSprite != null);
                _rankGroup.alpha = 1f;
                _rankImage.rectTransform.localScale = Vector3.one;
            }

            public IEnumerator RevealAvailable(float duration)
            {
                _visual.gameObject.SetActive(true);
                _button.interactable = false;
                _visual.localScale = Vector3.one * 0.11f;

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float eased = 1f - Mathf.Pow(1f - progress, 3f);
                    _visual.localScale = Vector3.one * Mathf.LerpUnclamped(0.11f, 1f, eased);
                    yield return null;
                }

                _visual.localScale = Vector3.one;
            }

            public IEnumerator RevealRank()
            {
                if (!_rankImage.gameObject.activeSelf) yield break;

                _rankGroup.alpha = 0f;
                _rankImage.rectTransform.localScale = Vector3.one * 0.2f;
                float elapsed = 0f;
                const float revealDuration = 0.2f;
                while (elapsed < revealDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / revealDuration);
                    float eased = 1f - Mathf.Pow(1f - progress, 3f);
                    _rankGroup.alpha = eased;
                    _rankImage.rectTransform.localScale =
                        Vector3.one * Mathf.LerpUnclamped(0.2f, 1.1f, eased);
                    yield return null;
                }

                elapsed = 0f;
                const float settleDuration = 0.1f;
                while (elapsed < settleDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / settleDuration);
                    _rankImage.rectTransform.localScale =
                        Vector3.one * Mathf.LerpUnclamped(1.1f, 1f, progress);
                    yield return null;
                }

                _rankGroup.alpha = 1f;
                _rankImage.rectTransform.localScale = Vector3.one;
            }

            void HandleClick()
            {
                if (_button.interactable && !string.IsNullOrWhiteSpace(_nodeId))
                    _onClick?.Invoke(_nodeId);
            }
        }
    }
}
