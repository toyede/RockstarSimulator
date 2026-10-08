using System.Collections.Generic;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>현재 손패의 카드 프리팹을 CardHand 아래에 동적으로 배치한다.</summary>
    public sealed class CardHandUI : MonoBehaviour
    {
        [SerializeField] Transform cardContainer;
        [SerializeField] Text hintText;
        [SerializeField] RectTransform dragLayer;
        [SerializeField] CardInput cardInput;

        [Header("많은 손패 배치")]
        [SerializeField, Min(1f), Tooltip("겹칠 때도 다음 카드가 드러나는 최소 가로 폭")]
        float minimumVisibleCardStep = 48f;

        [SerializeField, Min(0f), Tooltip("화면 양쪽에 남겨 둘 여백")]
        float horizontalScreenMargin = 40f;

        [Header("사용 연출")]
        [SerializeField, Tooltip("카드를 사용하면 픽셀 디졸브로 사라지게 한다")]
        bool useDissolveOnPlay = true;

        [SerializeField, Tooltip("Perfect 판정 때 디졸브 경계 색")]
        Color perfectEdgeColor = new Color32(0xFF, 0xD1, 0x66, 0xFF);

        [SerializeField, Tooltip("Good 판정 때 디졸브 경계 색")]
        Color goodEdgeColor = new Color32(0x31, 0xDF, 0xEA, 0xFF);

        [SerializeField, Tooltip("Miss 판정 때 디졸브 경계 색")]
        Color missEdgeColor = new Color32(0xF0, 0x1F, 0x1F, 0xFF);

        readonly List<CardSlotUI> _activeViews = new List<CardSlotUI>();

        /// <summary>디졸브 중이라 손패에서 빠졌지만 아직 풀에 반납하지 않은 카드들.</summary>
        readonly List<CardSlotUI> _dissolvingViews = new List<CardSlotUI>();
        bool _feverActive;
        HorizontalLayoutGroup _handLayout;
        float _baseHandSpacing;
        bool _capturedHandSpacing;
        Coroutine _stowRoutine;
        Vector2 _baseAnchoredPosition;
        bool _capturedBasePosition;
        AugmentCardVFX _augmentVfx;
        PerformanceTimerUI _timerUI;
        CardAugmentVisual _pendingHandVisual;
        Vector2 _pendingSource;
        int _pendingDrawStart;
        int _pendingBonusStart;
        readonly List<CardDefinition> _knownHand = new List<CardDefinition>();
        bool _consumedSinceRefresh;
        UIPixelBurstEmitter _arrivalPixels;
        int _handVisualVersion;

        /// <summary>연출 때문에 손패가 화면 아래로 내려가 있는지.</summary>
        public bool IsStowed { get; private set; }

        public void Configure(
            Transform container,
            Text hintLabel,
            RectTransform dragRoot,
            CardInput input)
        {
            cardContainer = container;
            hintText = hintLabel;
            dragLayer = dragRoot;
            cardInput = input;
            _capturedHandSpacing = false;
            ResolveHandLayout();
        }

        void OnEnable()
        {
            _timerUI = FindFirstObjectByType<PerformanceTimerUI>();
            EventBus.Subscribe<GameStateChanged>(OnVisualStateChanged);
            EventBus.Subscribe<HandChanged>(OnHandChanged);
            // CardSelected 는 HandChanged 보다 먼저 발행된다. 그 사이에 사용된 카드 뷰를
            // 손패 목록에서 빼내야 Refresh 가 그것을 곧바로 풀에 반납하지 않는다.
            EventBus.Subscribe<CardSelected>(OnCardSelected);
            EventBus.Subscribe<FeverStateChanged>(OnFeverStateChanged);
            _feverActive = FeverSystem.HasInstance && FeverSystem.Instance.IsActive;
            Refresh();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            _stowRoutine = null;
            if (_capturedBasePosition && transform is RectTransform handRect)
                handRect.anchoredPosition = _baseAnchoredPosition;
            IsStowed = false;
            _handVisualVersion++;
            _knownHand.Clear();
            _consumedSinceRefresh = false;
            if (_arrivalPixels != null) _arrivalPixels.enabled = false;
            EventBus.Unsubscribe<GameStateChanged>(OnVisualStateChanged);
            _pendingHandVisual = CardAugmentVisual.None;
            if (_augmentVfx != null) _augmentVfx.Clear();
            EventBus.Unsubscribe<HandChanged>(OnHandChanged);
            EventBus.Unsubscribe<CardSelected>(OnCardSelected);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverStateChanged);
            ReleaseDissolvingViews();
            ReleaseViews();
            ResetHandSpacing();
        }

        void OnHandChanged(HandChanged _)
        {
            _handVisualVersion++; // Discard delayed bursts for an already-replaced layout.
            // Multiset comparison: duplicate card types must not all sparkle as "new".
            var remaining = new List<CardDefinition>(_knownHand);
            var arrivals = new List<int>();
            if (CardSystem.HasInstance && (_knownHand.Count > 0 || _consumedSinceRefresh) && _pendingHandVisual == CardAugmentVisual.None &&
                GameManager.HasInstance && GameManager.Instance.IsPlaying)
            {
                var hand = CardSystem.Instance;
                for (int i = 0; i < hand.HandCount; i++)
                {
                    CardDefinition card = hand.GetCard(i);
                    int old = remaining.IndexOf(card);
                    if (old >= 0) remaining.RemoveAt(old);
                    else if (card != null) arrivals.Add(i);
                }
            }
            _consumedSinceRefresh = false;
            Refresh();
            PlayPendingHandVisual();
            if (arrivals.Count > 0) StartCoroutine(SparkleNewCards(arrivals, _handVisualVersion));
        }

        System.Collections.IEnumerator SparkleNewCards(List<int> arrivals, int version)
        {
            yield return null; // Layout first: avoid an emission at the previous pooled position.
            if (version != _handVisualVersion || IsStowed || !isActiveAndEnabled ||
                !GameManager.HasInstance || !GameManager.Instance.IsPlaying) yield break;
            if (_arrivalPixels == null) _arrivalPixels = GetComponent<UIPixelBurstEmitter>();
            if (_arrivalPixels == null) _arrivalPixels = gameObject.AddComponent<UIPixelBurstEmitter>();
            _arrivalPixels.enabled = true;
            foreach (int index in arrivals)
            {
                if (version != _handVisualVersion || IsStowed || !isActiveAndEnabled) yield break;
                if (index >= 0 && index < _activeViews.Count && _activeViews[index] != null)
                {
                    var view = _activeViews[index];
                    var tint = new Color32(0xC7, 0xDC, 0xD0, 255);
                    view.PlayArrivalFeedback(tint);
                    var rect = view.transform as RectTransform;
                    _arrivalPixels.EmitBurstAtNormalizedPoint(rect, new Vector2(0, 1), tint, 4, 35f, 85f, 3f, 5f, 0.2f, 0.35f);
                    _arrivalPixels.EmitBurstAtNormalizedPoint(rect, new Vector2(1, 1), tint, 4, 35f, 85f, 3f, 5f, 0.2f, 0.35f);
                }
                yield return new WaitForSeconds(0.06f);
            }
        }

        void OnVisualStateChanged(GameStateChanged e)
        {
            if (e.Current != GameState.Ready && e.Current != GameState.GameOver) return;
            _handVisualVersion++;
            _consumedSinceRefresh = false;
            if (_arrivalPixels != null) _arrivalPixels.enabled = false;
            _pendingHandVisual = CardAugmentVisual.None;
            if (_augmentVfx != null) _augmentVfx.Clear();
            ReleaseDissolvingViews();
        }

        void OnDestroy()
        {
            if (_augmentVfx != null) Destroy(_augmentVfx.gameObject);
        }

        void PlayPendingHandVisual()
        {
            CardAugmentVisual visual = _pendingHandVisual;
            _pendingHandVisual = CardAugmentVisual.None;
            if (_augmentVfx == null || visual == CardAugmentVisual.None) return;
            if (visual == CardAugmentVisual.Reroll)
                _augmentVfx.SweepHand((cardContainer != null ? cardContainer : transform) as RectTransform);
            for (int i = _pendingDrawStart; i < _activeViews.Count; i++)
                if (_activeViews[i] != null)
                    _augmentVfx.SplitToCard(_pendingSource, _activeViews[i].transform as RectTransform,
                        i >= _pendingBonusStart, i - _pendingDrawStart);
        }

        // ---------------- 손패 하강 (보스 연출) ----------------

        /// <summary>
        /// [연출 전용] 손패 루트를 화면 아래로 내리거나(true) 제자리로 올린다(false).
        /// 카드 입력은 CardInput.Locked 가 막고, 여기서는 위치만 움직인다. duration 0 이면 즉시.
        /// </summary>
        public void SetStowed(bool stowed, float duration, float distance)
        {
            var rect = transform as RectTransform;
            if (rect == null) return;
            if (!_capturedBasePosition)
            {
                _baseAnchoredPosition = rect.anchoredPosition;
                _capturedBasePosition = true;
            }

            IsStowed = stowed;
            Vector2 target = stowed
                ? _baseAnchoredPosition + Vector2.down * Mathf.Max(0f, distance)
                : _baseAnchoredPosition;

            if (_stowRoutine != null) StopCoroutine(_stowRoutine);
            if (duration <= 0f || !isActiveAndEnabled)
            {
                rect.anchoredPosition = target;
                _stowRoutine = null;
                return;
            }
            _stowRoutine = StartCoroutine(MoveHand(rect, target, duration));
        }

        System.Collections.IEnumerator MoveHand(RectTransform rect, Vector2 target, float duration)
        {
            Vector2 from = rect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                rect.anchoredPosition = Vector2.Lerp(from, target, t);
                yield return null;
            }
            rect.anchoredPosition = target;
            _stowRoutine = null;
        }

        void OnFeverStateChanged(FeverStateChanged e)
        {
            _feverActive = e.IsActive;
            for (int i = 0; i < _activeViews.Count; i++)
            {
                if (_activeViews[i] != null)
                    _activeViews[i].SetFeverVisual(_feverActive);
            }
        }

        // ---------------- 사용 연출 ----------------

        /// <summary>
        /// 사용된 카드를 손패 목록에서 떼어내 디졸브시킨다.
        /// 카드의 소비·드로우는 CardSystem 이 이미 처리했으므로 여기서는 <b>연출만</b> 담당한다.
        /// </summary>
        void OnCardSelected(CardSelected e)
        {
            // Card definitions are shared assets, so an identical replacement would
            // otherwise look unchanged. Remove only the consumed slot before diffing.
            if (e.HandIndex >= 0 && e.HandIndex < _knownHand.Count)
            {
                _knownHand.RemoveAt(e.HandIndex);
                _consumedSinceRefresh = true;
            }
            if (!useDissolveOnPlay) return;
            if (e.HandIndex < 0 || e.HandIndex >= _activeViews.Count) return;

            var view = _activeViews[e.HandIndex];
            int handCountBefore = _activeViews.Count;
            _activeViews.RemoveAt(e.HandIndex); // Refresh 가 회수하지 않도록 목록에서 제외
            if (view == null) return;

            var card = view.GetComponent<CardDefinition>();
            var upgrade = AugmentRuntime.Current.ResolveCardUpgrade(e.CardId);
            CardAugmentVisual visual = AugmentCardVFX.Resolve(card, upgrade);
            if (visual != CardAugmentVisual.None)
            {
                if (_augmentVfx == null) _augmentVfx = AugmentCardVFX.Create(transform);
                Vector2 source = AugmentCardVFX.ScreenCenter((RectTransform)view.transform);
                if (_augmentVfx != null)
                {
                    _augmentVfx.PlayCollapse(source, visual);
                    if (visual == CardAugmentVisual.Encore)
                    {
                        if (_timerUI == null) _timerUI = FindFirstObjectByType<PerformanceTimerUI>();
                        if (_timerUI != null) _augmentVfx.FlyToTimer(source, _timerUI.VisualAnchor);
                    }
                    else if (visual == CardAugmentVisual.Draw || visual == CardAugmentVisual.Reroll)
                    {
                        _pendingSource = source;
                        _pendingHandVisual = visual;
                        _pendingDrawStart = visual == CardAugmentVisual.Draw ? handCountBefore - 1 : 0;
                        _pendingBonusStart = visual == CardAugmentVisual.Draw
                            ? _pendingDrawStart + card.DrawCount : handCountBefore;
                    }
                }
            }
            PlayDissolve(view, e.Judgement, visual);
        }

        void PlayDissolve(CardSlotUI view, HypeJudgement judgement, CardAugmentVisual visual)
        {
            var go = view.gameObject;

            // 손패 레이아웃에서 빼내 화면 위치를 유지한 채 드래그 레이어로 옮긴다.
            // (그대로 두면 레이아웃 그룹이 남은 카드들을 밀어버린다)
            if (dragLayer != null) go.transform.SetParent(dragLayer, worldPositionStays: true);

            // 연출 중에 다시 잡히지 않도록 입력만 끈다. 오브젝트는 그대로 살려둔다.
            var dragHandler = go.GetComponent<CardDragHandler>();
            if (dragHandler != null) dragHandler.enabled = false;

            var effect = go.GetComponent<CardDissolveEffect>();
            if (effect == null) effect = go.AddComponent<CardDissolveEffect>();

            _dissolvingViews.Add(view);
            view.PlayUpgradeUseFlash();
            effect.SetEdgeColor(visual == CardAugmentVisual.None ? EdgeColorFor(judgement) : AugmentCardVFX.Gold);
            effect.PlayDissolve(() => ReleaseDissolvedView(view), visual);
        }

        Color EdgeColorFor(HypeJudgement judgement)
        {
            switch (judgement)
            {
                case HypeJudgement.Perfect: return perfectEdgeColor;
                case HypeJudgement.Good:    return goodEdgeColor;
                default:                    return missEdgeColor; // Miss / RiskMiss
            }
        }

        /// <summary>디졸브가 끝난 카드를 풀에 반납한다. (다음 사용을 위해 상태를 되돌린 뒤)</summary>
        void ReleaseDissolvedView(CardSlotUI view)
        {
            _dissolvingViews.Remove(view);
            if (view == null) return;

            var effect = view.GetComponent<CardDissolveEffect>();
            if (effect != null) effect.ResetEffect();

            var dragHandler = view.GetComponent<CardDragHandler>();
            if (dragHandler != null) dragHandler.enabled = true;

            if (!IsTearingDown) PoolManager.Despawn(view);
        }

        void ReleaseDissolvingViews()
        {
            for (int i = _dissolvingViews.Count - 1; i >= 0; i--) ReleaseDissolvedView(_dissolvingViews[i]);
            _dissolvingViews.Clear();
        }

        void Refresh()
        {
            _handVisualVersion++;
            _knownHand.Clear();
            ReleaseViews();

            if (!CardSystem.HasInstance)
            {
                if (hintText != null) hintText.text = "카드 시스템을 찾을 수 없음";
                return;
            }

            var system = CardSystem.Instance;
            var parent = cardContainer != null ? cardContainer : transform;

            for (int i = 0; i < system.HandCount; i++)
            {
                var card = system.GetCard(i);
                if (card == null) continue;
                _knownHand.Add(card);

                var instance = PoolManager.Spawn(card.gameObject, Vector3.zero, Quaternion.identity, parent);
                if (instance == null) continue;

                var rect = instance.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.zero;
                    rect.localRotation = Quaternion.identity;
                    rect.localScale = Vector3.one;
                }
                instance.transform.SetAsLastSibling();

                var view = instance.GetComponent<CardSlotUI>();
                if (view == null)
                {
                    Debug.LogWarning($"[CardHandUI] {card.name} 프리팹에 CardSlotUI가 없습니다.");
                    PoolManager.Despawn(instance);
                    continue;
                }

                var dragHandler = instance.GetComponent<CardDragHandler>();
                if (dragHandler == null)
                {
                    Debug.LogWarning($"[CardHandUI] {card.name} 프리팹에 CardDragHandler가 없습니다.");
                    PoolManager.Despawn(instance);
                    continue;
                }

                // 풀에서 나온 카드는 이전 디졸브 상태(Material·크기·알파)가 남아 있을 수 있다
                var effect = instance.GetComponent<CardDissolveEffect>();
                if (effect != null) effect.ResetEffect();
                dragHandler.enabled = true;

                view.Bind(card, i);
                view.SetFeverVisual(_feverActive);
                dragHandler.Bind(i, cardInput, dragLayer, parent as RectTransform);
                _activeViews.Add(view);
            }

            if (hintText != null)
            {
                hintText.text = system.HandCount > 0
                    ? "카드를 위로 드래그해 사용"
                    : "카드를 준비하는 중";
            }

            if (parent is RectTransform parentRect)
            {
                UpdateAdaptiveSpacing(parentRect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }

        void ResolveHandLayout()
        {
            Transform container = cardContainer != null ? cardContainer : transform;
            _handLayout = container.GetComponent<HorizontalLayoutGroup>();
            if (_handLayout == null) return;

            _baseHandSpacing = _handLayout.spacing;
            _capturedHandSpacing = true;
        }

        void UpdateAdaptiveSpacing(RectTransform handRect)
        {
            if (!_capturedHandSpacing) ResolveHandLayout();
            if (_handLayout == null || _activeViews.Count <= 1)
            {
                ResetHandSpacing();
                return;
            }

            RectTransform availableRect = handRect.parent as RectTransform;
            if (availableRect == null || availableRect.rect.width <= 0f)
            {
                ResetHandSpacing();
                return;
            }

            float cardWidth = 160f;
            RectTransform firstCard = _activeViews[0] == null
                ? null
                : _activeViews[0].transform as RectTransform;
            if (firstCard != null && firstCard.rect.width > 0f)
                cardWidth = firstCard.rect.width;

            float availableWidth = Mathf.Max(
                cardWidth,
                availableRect.rect.width -
                horizontalScreenMargin * 2f -
                _handLayout.padding.horizontal);
            float naturalStep = cardWidth + _baseHandSpacing;
            float fittedStep =
                (availableWidth - cardWidth) / (_activeViews.Count - 1);
            float actualStep = Mathf.Clamp(
                fittedStep,
                Mathf.Min(minimumVisibleCardStep, naturalStep),
                naturalStep);
            _handLayout.spacing = actualStep - cardWidth;
        }

        void ResetHandSpacing()
        {
            if (_capturedHandSpacing && _handLayout != null)
                _handLayout.spacing = _baseHandSpacing;
        }

        /// <summary>
        /// 씬이 언로드되는 중인가. 이때 PoolManager.Despawn 을 부르면 파괴 중인 부모에
        /// 리페어런트를 시도해 "Cannot set the parent ... while deactivating" 경고가 쏟아진다.
        /// Unity 가 어차피 오브젝트를 정리하므로 이럴 때는 목록만 비운다.
        /// </summary>
        bool IsTearingDown =>
            SingletonRuntime.IsQuitting ||
            !PoolManager.HasInstance ||
            !gameObject.scene.isLoaded;

        void ReleaseViews()
        {
            if (IsTearingDown)
            {
                _activeViews.Clear();
                return;
            }

            for (int i = 0; i < _activeViews.Count; i++)
            {
                if (_activeViews[i] != null)
                    PoolManager.Despawn(_activeViews[i]);
            }
            _activeViews.Clear();
        }
    }
}
