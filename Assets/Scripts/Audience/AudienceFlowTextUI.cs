using System;
using System.Collections.Generic;
using GameJamKit;
using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객이 들어오고 나가는 것을 관객 무리 위에 숫자로 띄운다. (+1 관객 / -3 관객)
    ///
    /// 로스터는 관객 <b>한 명당 한 번씩</b> 이벤트를 발행하므로 그대로 띄우면
    /// "-1"이 세 번 뜬다. 짧은 시간(aggregateWindow) 동안 모아서 한 장으로 합친다.
    ///
    /// 공연 시작·리셋으로 명단이 통째로 갈리는 것은 플레이어의 행동이 아니므로
    /// 기본적으로 제외한다 (excludedJoinReasons / excludedDepartureReasons).
    ///
    /// 로스터를 전혀 수정하지 않고 EventBus 만 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudienceFlowTextUI : MonoBehaviour
    {
        /// <summary>이탈 사유별 색·문구. 목록에 없는 사유는 기본값을 쓴다.</summary>
        [Serializable]
        public struct DepartureStyle
        {
            [Tooltip("이 사유로 나갈 때")]
            public AudienceDepartureReason reason;

            [Tooltip("표시 색")]
            public Color color;

            [Tooltip("비우면 기본 문구(departFormat)를 쓴다. 채우면 이 문구로 대체된다")]
            public string formatOverride;
        }

        [Header("위치")]
        [SerializeField, Tooltip(
            "비워두면 씬의 AudienceRosterPresenter 의 관객 루트를 따라간다. " +
            "직접 지정하면 그 오브젝트 위에 뜬다")]
        Transform followTarget;

        [SerializeField, Tooltip("따라갈 대상으로부터의 오프셋(월드 유닛). 관객 무리 위로 띄운다")]
        Vector3 followOffset = new Vector3(0f, 2.6f, 0f);

        [SerializeField, Tooltip("연속으로 뜰 때 좌우로 흩뿌리는 폭. 0이면 항상 같은 자리")]
        float horizontalJitter = 0.6f;

        [Header("문구")]
        [SerializeField, Tooltip("입장 문구. {0} 자리에 인원수가 들어간다")]
        string joinFormat = "+{0} 관객";

        [SerializeField, Tooltip("퇴장 문구. {0} 자리에 인원수가 들어간다")]
        string departFormat = "-{0} 관객";

        [SerializeField] Color joinColor = new Color(0.45f, 1f, 0.55f, 1f);
        [SerializeField] Color departColor = new Color(1f, 0.42f, 0.35f, 1f);

        [SerializeField, Tooltip("이탈 사유별로 색·문구를 다르게 하고 싶을 때만 채운다")]
        List<DepartureStyle> departureStyles = new List<DepartureStyle>
        {
            new DepartureStyle
            {
                reason = AudienceDepartureReason.NearbyConcert,
                color = new Color(1f, 0.62f, 0.2f, 1f),
                formatOverride = "-{0} 옆 공연으로",
            },
        };

        [Header("집계")]
        [SerializeField, Min(0f), Tooltip(
            "이 시간(초) 동안 들어온 이벤트를 하나로 합친다. " +
            "0이면 한 명당 한 장씩 뜬다")]
        float aggregateWindow = 0.15f;

        [SerializeField, Tooltip("표시하지 않을 입장 사유. 공연 시작 시 명단이 채워지는 것은 연출 대상이 아니다")]
        List<AudienceJoinReason> excludedJoinReasons = new List<AudienceJoinReason>
        {
            AudienceJoinReason.Initialization,
        };

        [SerializeField, Tooltip("표시하지 않을 퇴장 사유")]
        List<AudienceDepartureReason> excludedDepartureReasons = new List<AudienceDepartureReason>
        {
            AudienceDepartureReason.Reset,
        };

        [Header("표현")]
        [SerializeField, Tooltip("비워두면 TMP 기본 글꼴을 쓴다. 셋업 메뉴가 DungGeunMo 를 연결한다")]
        TMP_FontAsset font;

        [SerializeField] string sortingLayer = "Default";

        [SerializeField, Tooltip("관객보다 확실히 앞에 나오도록 큰 값을 준다")]
        int sortingOrder = 200;

        [SerializeField, Min(1), Tooltip("동시에 떠 있을 수 있는 텍스트 수")]
        int maxSimultaneousTexts = 6;

        [SerializeField] FloatingWorldTextStyle textStyle = new FloatingWorldTextStyle
        {
            duration = 1.2f,
            riseDistance = 1.1f,
            startScale = 0.5f,
            peakScale = 1.3f,
            fadeStart = 0.55f,
            fontSize = 3.4f,
        };

        FloatingWorldTextPool _pool;

        // 집계 버킷: 입장 1개 + 퇴장은 스타일별로 나눈다.
        // (같은 창 안에서 사유가 섞여도 색이 뒤바뀌지 않는다)
        int _joinCount;
        int[] _departCounts;
        float _flushAt;
        bool _pending;
        int _jitterStep;

        void Awake()
        {
            _departCounts = new int[departureStyles.Count + 1]; // 마지막 칸 = 기본 스타일
            _pool = new FloatingWorldTextPool(
                transform,
                font,
                sortingLayer,
                textStyle,
                maxSimultaneousTexts,
                "AudienceFlowText");
        }

        void OnEnable()
        {
            EventBus.Subscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Subscribe<AudienceDeparted>(OnAudienceDeparted);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Unsubscribe<AudienceDeparted>(OnAudienceDeparted);
            ClearPending();
            _pool?.StopAll();
        }

        void OnAudienceJoined(AudienceJoined e)
        {
            if (excludedJoinReasons.Contains(e.Reason)) return;

            _joinCount++;
            Schedule();
        }

        void OnAudienceDeparted(AudienceDeparted e)
        {
            if (excludedDepartureReasons.Contains(e.Reason)) return;

            _departCounts[ResolveDepartureStyleIndex(e.Reason)]++;
            Schedule();
        }

        void Schedule()
        {
            if (_pending) return;

            _pending = true;
            _flushAt = Time.unscaledTime + Mathf.Max(0f, aggregateWindow);
        }

        void Update()
        {
            FollowTarget();

            if (!_pending || Time.unscaledTime < _flushAt) return;
            Flush();
        }

        /// <summary>관객 무리 위에 붙어 있게 한다. 무리가 움직이지 않아도 늦게 생성되는 경우가 있어 매 프레임 확인한다.</summary>
        void FollowTarget()
        {
            if (followTarget == null)
            {
                if (!AudienceRosterSystem.HasInstance) return;

                var presenter = AudienceRosterSystem.Instance
                    .GetComponent<AudienceRosterPresenter>();
                if (presenter == null || presenter.MemberRoot == null) return;
                followTarget = presenter.MemberRoot;
            }

            transform.position = followTarget.position + followOffset;
        }

        void Flush()
        {
            if (_joinCount > 0)
            {
                Show(string.Format(joinFormat, _joinCount), joinColor);
                _joinCount = 0;
            }

            for (int i = 0; i < _departCounts.Length; i++)
            {
                if (_departCounts[i] <= 0) continue;

                ResolveDepartureStyle(i, out string format, out Color color);
                Show(string.Format(format, _departCounts[i]), color);
                _departCounts[i] = 0;
            }

            _pending = false;
        }

        void Show(string text, Color color)
        {
            if (_pool == null) return;

            // 연속으로 뜰 때 정확히 겹치지 않도록 좌우로 조금씩 어긋나게 놓는다
            float offsetX = horizontalJitter <= 0f
                ? 0f
                : ((_jitterStep & 1) == 0 ? 1f : -1f) *
                  horizontalJitter * (0.4f + 0.6f * ((_jitterStep >> 1) % 2));
            _jitterStep++;

            _pool.Play(text, color, new Vector3(offsetX, 0f, 0f), sortingOrder);
        }

        int ResolveDepartureStyleIndex(AudienceDepartureReason reason)
        {
            for (int i = 0; i < departureStyles.Count; i++)
                if (departureStyles[i].reason == reason) return i;
            return _departCounts.Length - 1; // 기본 스타일
        }

        void ResolveDepartureStyle(int index, out string format, out Color color)
        {
            if (index >= 0 && index < departureStyles.Count)
            {
                DepartureStyle style = departureStyles[index];
                format = string.IsNullOrEmpty(style.formatOverride)
                    ? departFormat
                    : style.formatOverride;
                color = style.color;
                return;
            }

            format = departFormat;
            color = departColor;
        }

        void ClearPending()
        {
            _joinCount = 0;
            if (_departCounts != null) Array.Clear(_departCounts, 0, _departCounts.Length);
            _pending = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            maxSimultaneousTexts = Mathf.Max(1, maxSimultaneousTexts);
            aggregateWindow = Mathf.Max(0f, aggregateWindow);

            // 스타일을 인스펙터에서 늘렸는데 Play 중이면 버킷 크기를 맞춰준다
            if (Application.isPlaying &&
                _departCounts != null &&
                _departCounts.Length != departureStyles.Count + 1)
                _departCounts = new int[departureStyles.Count + 1];
        }
#endif
    }
}
