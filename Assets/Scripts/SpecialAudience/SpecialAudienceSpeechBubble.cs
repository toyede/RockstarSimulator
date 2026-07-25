using System;
using System.Collections.Generic;
using GameJamKit;
using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 특별 관객 머리 위 말풍선. 자신이 무엇을 원하는지 계속 알려서 저격을 유도한다.
    ///
    /// - 등장하면 popInterval 마다 한 번씩 "톡" 떠오른다 (상시 표시는 시선을 뺏어서 쓰지 않는다)
    /// - 문구는 요구 타입별 표(lines)에서 가져온다 — 요구가 늘어나도 코드 수정이 필요 없다
    /// - 특별 관객이 사라지면 즉시 숨는다
    ///
    /// <b>반드시 SpecialAudience 프리팹 루트의 자식으로 둔다.</b>
    /// VisualRoot 아래에 두면 액터가 진행 방향에 따라 X 스케일을 뒤집을 때
    /// 글자가 좌우로 반전된다.
    ///
    /// 매니저를 참조하지 않고 EventBus 만 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialAudienceSpeechBubble : MonoBehaviour
    {
        /// <summary>요구 타입 한 줄. 문구와 색을 함께 관리한다.</summary>
        [Serializable]
        public struct BubbleLine
        {
            [Tooltip("이 요구일 때 띄울 문구")]
            public HeatStage stage;

            public string text;
            public Color color;
        }

        [Header("연결")]
        [SerializeField, Tooltip("비워두면 부모에서 찾는다. 크기·정렬 순서를 이 액터에 맞춘다")]
        SpecialAudienceCrowdActor actor;

        [SerializeField, Tooltip("문구를 표시할 TextMeshPro. 비워두면 이 오브젝트에서 찾거나 만든다")]
        TextMeshPro bubbleText;

        [SerializeField, Tooltip("선택 사항. 말풍선 배경 스프라이트가 생기면 여기에 연결한다")]
        SpriteRenderer bubbleBackground;

        [Header("문구 (요구 타입별)")]
        [SerializeField]
        List<BubbleLine> lines = new List<BubbleLine>
        {
            new BubbleLine
            {
                stage = HeatStage.Chill,
                text = "Chill;;",
                color = new Color(0.19f, 0.87f, 0.92f, 1f),
            },
            new BubbleLine
            {
                stage = HeatStage.Singalong,
                text = "Singalong♪",
                color = new Color(0.72f, 0.55f, 1f, 1f),
            },
            new BubbleLine
            {
                stage = HeatStage.Mosh,
                text = "Mosh!",
                color = new Color(1f, 0.36f, 0.32f, 1f),
            },
        };

        [SerializeField, Tooltip("표에 없는 요구가 들어왔을 때의 문구. 비우면 아무것도 띄우지 않는다")]
        string fallbackText = string.Empty;

        [Header("리듬")]
        [SerializeField, Min(0.1f), Tooltip("말풍선이 다시 뜨기까지의 간격(초)")]
        float popInterval = 1.5f;

        [SerializeField, Min(0f), Tooltip("등장 직후 첫 말풍선까지 기다리는 시간(초)")]
        float firstPopDelay = 0.25f;

        [SerializeField, Min(0.05f), Tooltip("떠 있는 시간(초). popInterval 보다 짧아야 깜빡인다")]
        float visibleDuration = 0.9f;

        [SerializeField, Min(0.01f), Tooltip("톡 튀어나오는 시간(초)")]
        float popInDuration = 0.12f;

        [SerializeField, Min(0.01f), Tooltip("사라지는 시간(초)")]
        float fadeOutDuration = 0.2f;

        [Header("배치")]
        [SerializeField, Tooltip("액터 발밑 기준 오프셋. 액터 크기에 비례해서 적용된다")]
        Vector3 localOffset = new Vector3(0f, 1.5f, 0f);

        [SerializeField, Min(0.01f), Tooltip("말풍선 자체의 크기 배율")]
        float bubbleScale = 1f;

        [SerializeField, Min(0.01f), Tooltip("톡 튀어나올 때의 최대 크기 배율")]
        float popPeakScale = 1.2f;

        [SerializeField, Tooltip("본체보다 앞에 그려지도록 하는 정렬 보정")]
        int sortingOrderBonus = 20;

        [SerializeField] string sortingLayer = "Default";

        [SerializeField, Min(0.1f)] float fontSize = 3f;

        // ---------------- 상태 ----------------

        bool _requestActive;      // 특별 관객이 요구를 들고 무대에 있는가
        bool _showing;            // 지금 말풍선이 떠 있는가
        float _elapsed;           // 현재 말풍선의 경과 시간
        float _nextPopAt;         // 다음 말풍선 시각
        string _currentText = string.Empty;
        Color _currentColor = Color.white;

        float VisibleTotal => popInDuration + visibleDuration + fadeOutDuration;

        void Awake()
        {
            if (actor == null) actor = GetComponentInParent<SpecialAudienceCrowdActor>();

            EnsureText();
            Hide();
        }

        void EnsureText()
        {
            if (bubbleText == null) bubbleText = GetComponent<TextMeshPro>();
            if (bubbleText == null) bubbleText = gameObject.AddComponent<TextMeshPro>();

            bubbleText.alignment = TextAlignmentOptions.Center;
            bubbleText.enableWordWrapping = false;
            bubbleText.raycastTarget = false;
            bubbleText.fontSize = fontSize;
            if (!string.IsNullOrEmpty(sortingLayer))
                bubbleText.renderer.sortingLayerName = sortingLayer;
        }

        void OnEnable()
        {
            EventBus.Subscribe<SpecialAudienceSpawned>(OnSpawned);
            EventBus.Subscribe<SpecialAudienceEnded>(OnEnded);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<SpecialAudienceSpawned>(OnSpawned);
            EventBus.Unsubscribe<SpecialAudienceEnded>(OnEnded);
            Hide();
            _requestActive = false;
        }

        // ---------------- 이벤트 ----------------

        void OnSpawned(SpecialAudienceSpawned e)
        {
            if (!TryResolveLine(e.RequestType, out _currentText, out _currentColor))
            {
                _requestActive = false;
                Hide();
                return;
            }

            _requestActive = true;
            _nextPopAt = Time.time + firstPopDelay;
            Hide();
        }

        /// <summary>
        /// 성공이든 만료든 요구가 끝나면 말풍선은 즉시 사라진다.
        /// (성공 시 액터는 잠깐 더 환호하지만, 이미 이룬 요구를 계속 외치면 어색하다)
        /// </summary>
        void OnEnded(SpecialAudienceEnded e)
        {
            _requestActive = false;
            Hide();
        }

        bool TryResolveLine(HeatStage stage, out string text, out Color color)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].stage != stage) continue;

                text = lines[i].text;
                color = lines[i].color;
                return !string.IsNullOrEmpty(text);
            }

            text = fallbackText;
            color = Color.white;
            return !string.IsNullOrEmpty(text);
        }

        // ---------------- 매 프레임 ----------------

        void Update()
        {
            // 액터가 아직 자리를 못 잡았거나 숨어 있으면 말풍선도 나오지 않는다
            bool actorVisible = actor == null || actor.IsActive;
            if (!_requestActive || !actorVisible)
            {
                if (_showing) Hide();
                return;
            }

            if (!_showing)
            {
                if (Time.time < _nextPopAt) return;
                Pop();
            }

            _elapsed += Time.deltaTime;
            if (_elapsed >= VisibleTotal)
            {
                Hide();
                // 다음 말풍선은 "이번 말풍선이 뜨기 시작한 시점 + 간격" 기준으로 잡는다.
                // 표시 시간을 늘려도 리듬이 밀리지 않는다.
                _nextPopAt = Time.time + Mathf.Max(0f, popInterval - VisibleTotal);
                return;
            }

            ApplyVisual();
        }

        void Pop()
        {
            _showing = true;
            _elapsed = 0f;

            bubbleText.text = _currentText;
            bubbleText.enabled = true;
            if (bubbleBackground != null) bubbleBackground.enabled = true;
        }

        /// <summary>
        /// 매 프레임 경과 시간으로부터 위치·크기·투명도를 새로 만든다.
        /// 액터가 줄을 옮기면 크기·정렬 순서가 바뀌므로 여기서 계속 따라간다.
        /// </summary>
        void ApplyVisual()
        {
            float actorScale = actor != null ? actor.CurrentVisualScale : 1f;

            transform.localPosition = localOffset * actorScale;

            float punch = _elapsed < popInDuration
                ? Mathf.Lerp(0f, popPeakScale, _elapsed / popInDuration)
                : Mathf.Lerp(popPeakScale, 1f, Mathf.Clamp01((_elapsed - popInDuration) / 0.15f));
            transform.localScale = Vector3.one * (bubbleScale * actorScale * punch);

            float fadeElapsed = _elapsed - (popInDuration + visibleDuration);
            float alpha = fadeElapsed <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(fadeElapsed / fadeOutDuration);

            Color color = _currentColor;
            color.a = alpha;
            bubbleText.color = color;

            if (bubbleBackground != null)
            {
                Color backgroundColor = bubbleBackground.color;
                backgroundColor.a = alpha;
                bubbleBackground.color = backgroundColor;
            }

            int order = (actor != null && actor.CharacterRenderer != null
                ? actor.CharacterRenderer.sortingOrder
                : 0) + sortingOrderBonus;
            bubbleText.renderer.sortingOrder = order;
            if (bubbleBackground != null) bubbleBackground.sortingOrder = order - 1;
        }

        void Hide()
        {
            _showing = false;
            _elapsed = 0f;
            if (bubbleText != null)
            {
                bubbleText.text = string.Empty;
                bubbleText.enabled = false;
            }
            if (bubbleBackground != null) bubbleBackground.enabled = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            popInterval = Mathf.Max(0.1f, popInterval);
            visibleDuration = Mathf.Max(0.05f, visibleDuration);
            if (bubbleText != null) bubbleText.fontSize = fontSize;
        }

        [ContextMenu("Debug/Pop Chill")] void DebugChill() => DebugPop(HeatStage.Chill);
        [ContextMenu("Debug/Pop Singalong")] void DebugSingalong() => DebugPop(HeatStage.Singalong);
        [ContextMenu("Debug/Pop Mosh")] void DebugMosh() => DebugPop(HeatStage.Mosh);

        void DebugPop(HeatStage stage)
        {
            EnsureText();
            if (!TryResolveLine(stage, out _currentText, out _currentColor)) return;

            _requestActive = true;
            _nextPopAt = Time.time;
            Hide();
        }
#endif
    }
}
