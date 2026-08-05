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
        }

        void OnEnable()
        {
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
            EventBus.Unsubscribe<HandChanged>(OnHandChanged);
            EventBus.Unsubscribe<CardSelected>(OnCardSelected);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverStateChanged);
            ReleaseDissolvingViews();
            ReleaseViews();
        }

        void OnHandChanged(HandChanged _) => Refresh();

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
            if (!useDissolveOnPlay) return;
            if (e.HandIndex < 0 || e.HandIndex >= _activeViews.Count) return;

            var view = _activeViews[e.HandIndex];
            _activeViews.RemoveAt(e.HandIndex); // Refresh 가 회수하지 않도록 목록에서 제외
            if (view == null) return;

            PlayDissolve(view, e.Judgement);
        }

        void PlayDissolve(CardSlotUI view, HypeJudgement judgement)
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
            effect.SetEdgeColor(EdgeColorFor(judgement));
            effect.PlayDissolve(() => ReleaseDissolvedView(view));
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

                view.Bind(card);
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
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
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
