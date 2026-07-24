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

        readonly List<CardSlotUI> _activeViews = new List<CardSlotUI>();

        public void Configure(Transform container, Text hintLabel)
        {
            cardContainer = container;
            hintText = hintLabel;
        }

        void OnEnable()
        {
            EventBus.Subscribe<HandChanged>(OnHandChanged);
            Refresh();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<HandChanged>(OnHandChanged);
            ReleaseViews();
        }

        void OnHandChanged(HandChanged _) => Refresh();

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

                view.Bind(card, i);
                _activeViews.Add(view);
            }

            if (hintText != null)
            {
                hintText.text = system.HandCount > 0
                    ? $"숫자키 1~{system.HandCount}로 카드 선택"
                    : "카드를 준비하는 중";
            }

            if (parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }

        void ReleaseViews()
        {
            if (SingletonRuntime.IsQuitting || !PoolManager.HasInstance)
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
