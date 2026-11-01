using System.Collections.Generic;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 패시브 증강이 발동하면 화면 중앙에 문구를 튀어나오게 했다가 위로 올리며 지운다.
    /// 게임 수치에는 관여하지 않고 이벤트만 구독한다. 텍스트 뷰는 런타임에 만들어 돌려 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PassiveAugmentNoticeUI : MonoBehaviour
    {
        [System.Serializable]
        public sealed class NoticeEntry
        {
            public AugmentNoticeKind kind;
            public string title;
            [Tooltip("{0} = 이벤트 값. 비워두면 제목만 뜬다")]
            public string detailFormat;
            public Color color = Color.white;

            public NoticeEntry(AugmentNoticeKind kind, string title, string detailFormat, Color color)
            {
                this.kind = kind;
                this.title = title;
                this.detailFormat = detailFormat;
                this.color = color;
            }
        }

        [Header("문구 (UI 표시 텍스트)")]
        [SerializeField] List<NoticeEntry> entries = new List<NoticeEntry>
        {
            new NoticeEntry(AugmentNoticeKind.BandmateCover, "동료 커버!", "남은 {0}회", Color.white),
            new NoticeEntry(AugmentNoticeKind.FeverExtension, "피버 연장", "+{0}초", Color.white),
            new NoticeEntry(AugmentNoticeKind.BreathingRoom, "숨 고르기", "", Color.white),
            new NoticeEntry(AugmentNoticeKind.PreAdvertising, "사전 홍보!", "", Color.white),
            new NoticeEntry(AugmentNoticeKind.PreferenceInsight, "취향 간파!", "", Color.white),
            new NoticeEntry(AugmentNoticeKind.ExtraHand, "한 장의 여유!", "", Color.white),
            new NoticeEntry(AugmentNoticeKind.HighRiskTempo, "무모한 템포!", "", Color.white),
            new NoticeEntry(AugmentNoticeKind.ShortSet, "전력 질주!", "", Color.white),
            new NoticeEntry(AugmentNoticeKind.PerfectClear, "완벽주의 성공!", "점수 ×{0}", Color.white),
        };
        [SerializeField, Tooltip("숨 고르기가 카드를 쓰기 전까지 반복 발동해도 한 번만 띄운다")]
        bool breathingRoomOncePerIdle;

        [Header("외형")]
        [SerializeField] Font font;
        [SerializeField, Min(8)] int titleFontSize = 64;
        [SerializeField, Min(8)] int detailFontSize = 34;
        [SerializeField, Tooltip("화면 중앙 기준 위치")] Vector2 centerOffset = new Vector2(0f, 120f);
        [SerializeField] Color outlineColor = new Color(0f, 0f, 0f, 0.92f);
        [SerializeField] Vector2 outlineDistance = new Vector2(3f, -3f);

        [Header("연출 시간 (초)")]
        [SerializeField, Min(0.01f)] float popDuration = 0.22f;
        [SerializeField, Range(1f, 2f)] float popOvershoot = 1.25f;
        [SerializeField, Min(0f)] float holdDuration = 0.45f;
        [SerializeField, Min(0.01f)] float exitDuration = 0.7f;
        [SerializeField, Min(0f)] float exitRise = 90f;
        [SerializeField, Min(0f), Tooltip("여러 문구가 겹칠 때 다음 문구까지의 최소 간격")]
        float startInterval = 0.3f;
        [SerializeField, Range(1, 6)] int maxVisible = 3;
        [SerializeField, Min(0f), Tooltip("새 문구가 뜨면 먼저 뜬 문구를 이만큼 위로 민다")]
        float stackSpacing = 110f;
        [SerializeField, Min(0.01f)] float stackShiftDuration = 0.15f;
        [SerializeField, Tooltip("비워두면 소리 없음")] string sfxId = "";

        sealed class NoticeView
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Text Title, Detail;
            public float Age = -1f;
            public float Shift, TargetShift;
        }

        readonly List<NoticeView> _views = new List<NoticeView>(6);
        readonly Queue<AugmentNoticeRequested> _pending = new Queue<AugmentNoticeRequested>(8);
        float _sinceLastStart = float.MaxValue;
        bool _breathingShown;

        /// <summary>문구 하나가 등장부터 사라질 때까지 걸리는 시간.</summary>
        public float NoticeDuration => popDuration + holdDuration + exitDuration;

        void OnEnable()
        {
            EventBus.Subscribe<AugmentNoticeRequested>(OnNoticeRequested);
            EventBus.Subscribe<ComboLossPrevented>(OnComboLossPrevented);
            EventBus.Subscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Subscribe<BreathingRoomCardAdded>(OnBreathingRoomCardAdded);
            EventBus.Subscribe<CardSelected>(OnCardSelected);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AugmentNoticeRequested>(OnNoticeRequested);
            EventBus.Unsubscribe<ComboLossPrevented>(OnComboLossPrevented);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Unsubscribe<BreathingRoomCardAdded>(OnBreathingRoomCardAdded);
            EventBus.Unsubscribe<CardSelected>(OnCardSelected);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            Clear();
        }

        void OnNoticeRequested(AugmentNoticeRequested e) => _pending.Enqueue(e);

        void OnComboLossPrevented(ComboLossPrevented e)
        {
            if (e.ProtectedCombo <= 0) return;
            _pending.Enqueue(new AugmentNoticeRequested(
                AugmentNoticeKind.BandmateCover, e.RemainingPreventions));
        }

        void OnFeverStateChanged(FeverStateChanged e)
        {
            if (!e.IsActive) return;
            float bonus = AugmentRuntime.Current.FeverDurationBonus;
            if (bonus <= 0f) return;
            _pending.Enqueue(new AugmentNoticeRequested(
                AugmentNoticeKind.FeverExtension, bonus));
        }

        void OnBreathingRoomCardAdded(BreathingRoomCardAdded _)
        {
            if (breathingRoomOncePerIdle && _breathingShown) return;
            _breathingShown = true;
            _pending.Enqueue(new AugmentNoticeRequested(AugmentNoticeKind.BreathingRoom));
        }

        void OnCardSelected(CardSelected _) => _breathingShown = false;

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Clear();
        }

        void Update()
        {
            if (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused) return;
            float dt = Time.unscaledDeltaTime;
            _sinceLastStart += dt;

            if (_pending.Count > 0 && _sinceLastStart >= startInterval)
            {
                NoticeView view = AcquireView();
                if (view != null)
                {
                    Begin(view, _pending.Dequeue());
                    _sinceLastStart = 0f;
                }
            }

            for (int i = 0; i < _views.Count; i++)
            {
                NoticeView view = _views[i];
                if (view.Age < 0f) continue;
                view.Age += dt;
                view.Shift = Mathf.MoveTowards(view.Shift, view.TargetShift,
                    stackSpacing / stackShiftDuration * dt);
                Animate(view);
            }
        }

        void Begin(NoticeView view, AugmentNoticeRequested request)
        {
            NoticeEntry entry = FindEntry(request.Kind);
            if (entry == null || string.IsNullOrEmpty(entry.title)) return;

            // 아직 떠 있는 문구는 위로 밀어 새 문구와 겹치지 않게 한다.
            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != view && _views[i].Age >= 0f)
                    _views[i].TargetShift += stackSpacing;
            view.Shift = view.TargetShift = 0f;

            view.Title.text = entry.title;
            view.Title.color = entry.color;
            bool hasDetail = !string.IsNullOrEmpty(entry.detailFormat);
            view.Detail.gameObject.SetActive(hasDetail);
            if (hasDetail)
                view.Detail.text = string.Format(entry.detailFormat, request.Value.ToString("0.#"));
            view.Root.SetAsLastSibling();
            view.Root.gameObject.SetActive(true);
            view.Age = 0f;
            Animate(view);
            if (!string.IsNullOrEmpty(sfxId)) Sound.Play(sfxId);
        }

        void Animate(NoticeView view)
        {
            float age = view.Age;
            float scale = 1f, rise = 0f, alpha = 1f;
            if (age < popDuration)
            {
                // 0 → 오버슈트 → 1 로 '뾰잉' 튀어나온다.
                float t = age / popDuration;
                scale = t < 0.6f
                    ? Mathf.SmoothStep(0f, popOvershoot, t / 0.6f)
                    : Mathf.SmoothStep(popOvershoot, 1f, (t - 0.6f) / 0.4f);
            }
            else if (age >= popDuration + holdDuration)
            {
                float t = Mathf.Clamp01((age - popDuration - holdDuration) / exitDuration);
                float eased = 1f - (1f - t) * (1f - t);
                rise = exitRise * eased;
                alpha = 1f - t;
            }

            view.Root.localScale = Vector3.one * scale;
            view.Root.anchoredPosition = centerOffset + Vector2.up * (rise + view.Shift);
            view.Group.alpha = alpha;

            if (age >= NoticeDuration) Release(view);
        }

        NoticeEntry FindEntry(AugmentNoticeKind kind)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].kind == kind) return entries[i];
            return null;
        }

        NoticeView AcquireView()
        {
            int active = 0;
            for (int i = 0; i < _views.Count; i++)
            {
                if (_views[i].Age < 0f) return _views[i];
                active++;
            }
            return active < maxVisible ? CreateView() : null;
        }

        NoticeView CreateView()
        {
            var root = new GameObject("AugmentNotice", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)root.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, titleFontSize + detailFontSize + 24f);

            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var view = new NoticeView
            {
                Root = rect,
                Group = group,
                Title = CreateText(rect, "Title", titleFontSize, new Vector2(0f, detailFontSize * 0.5f)),
                Detail = CreateText(rect, "Detail", detailFontSize, new Vector2(0f, -titleFontSize * 0.5f - 4f))
            };
            view.Detail.color = Color.white;
            root.SetActive(false);
            _views.Add(view);
            return view;
        }

        Text CreateText(RectTransform parent, string name, int size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(900f, size + 12f);

            var text = go.GetComponent<Text>();
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var outline = go.GetComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = outlineDistance;
            outline.useGraphicAlpha = true;
            return text;
        }

        void Release(NoticeView view)
        {
            view.Age = -1f;
            if (view.Root != null) view.Root.gameObject.SetActive(false);
        }

        void Clear()
        {
            _pending.Clear();
            _sinceLastStart = float.MaxValue;
            _breathingShown = false;
            for (int i = 0; i < _views.Count; i++) Release(_views[i]);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i].Root != null) Destroy(_views[i].Root.gameObject);
            _views.Clear();
        }
    }
}
