using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 배경 뒤쪽에 서 있는 <b>장식용 군중.</b> 게임 로직에 전혀 관여하지 않는다.
    ///
    /// - 관객 수에 비례해 모이고 흩어진다 (0 ~ maxCount)
    /// - 콤보가 오를수록 크게 들썩인다
    ///
    /// <b>실제 관객(AudienceRosterSystem)과 혼동하지 말 것.</b> 이쪽은 판정도 점수도 없는
    /// 배경 그림이고, 관객 명단을 읽기만 한다. 관객 시스템·콤보 시스템 어느 쪽도 수정하지 않는다.
    ///
    /// 움직임은 일반 관객과 <b>같은 공식</b>(<see cref="CrowdMotionEvaluator"/>)을 쓴다.
    /// 뒤쪽 사람들만 다른 리듬으로 흔들리면 눈에 거슬리기 때문이다.
    ///
    /// 개체는 Awake 에 maxCount 만큼 한 번 만들어 두고 보이고 숨기기만 한다 —
    /// 관객이 드나들 때마다 Instantiate/Destroy 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DecorativeCrowd : MonoBehaviour
    {
        [Header("스프라이트")]
        [SerializeField, Tooltip("장식용 사람 변형. 개체마다 하나를 골라 쓴다 (people1 / people2)")]
        List<Sprite> variants = new List<Sprite>();

        [Header("인원")]
        [SerializeField, Min(0), Tooltip("한 번에 보일 수 있는 최대 인원")]
        int maxCount = 9;

        [SerializeField, Tooltip(
            "관객이 이 비율만큼 찼을 때 장식 인원이 가득 찬다. " +
            "1이면 관객 정원이 다 차야 9명이 되고, 0.6이면 60%만 차도 다 모인다")]
        [Range(0.05f, 1f)] float audienceRatioForFull = 0.8f;

        [SerializeField, Tooltip("관객이 한 명이라도 있으면 최소한 이만큼은 서 있게 한다")]
        [Min(0)] int minimumWhenAnyAudience = 1;

        [Header("배치")]
        [SerializeField, Tooltip("좌우로 퍼지는 폭(월드 유닛)")]
        float spreadWidth = 16f;

        [SerializeField, Min(1), Tooltip(
            "앞뒤 줄 수. 한 줄로 세우면 띠처럼 보이므로 여러 줄로 겹쳐 무리를 만든다")]
        int rows = 3;

        [SerializeField, Tooltip("한 줄 뒤로 갈 때마다 올라가는 높이")]
        float rowSpacing = 0.55f;

        [SerializeField, Range(0.5f, 1f), Tooltip("한 줄 뒤로 갈 때마다 곱해지는 크기 비율")]
        float rowScaleFalloff = 0.92f;

        [SerializeField, Tooltip("줄마다 좌우로 반 칸씩 어긋내는 정도. 앞뒤가 정확히 겹치지 않게 한다")]
        float rowStagger = 0.5f;

        [SerializeField, Tooltip("세로로 흐트러지는 정도")]
        float verticalJitter = 0.18f;

        [SerializeField, Min(0.01f), Tooltip("기본 크기 배율")]
        float baseScale = 2.2f;

        [SerializeField, Range(0f, 0.5f), Tooltip("개체마다 크기를 흐트러뜨리는 정도")]
        float scaleJitter = 0.12f;

        [SerializeField, Tooltip("배치 난수 시드. 바꾸면 서 있는 모양이 통째로 달라진다")]
        int seed = 4321;

        [Header("정렬 (배경 뒤쪽)")]
        [SerializeField] string sortingLayer = "Default";

        [SerializeField, Tooltip(
            "배경보다 앞, 관객보다는 뒤에 와야 한다. " +
            "현재 씬 기준: night_city_ground = 0, stage_lights = 1, 관객 = 5부터, 밴드 = 45. " +
            "0보다 낮으면 배경에 가려 아예 보이지 않는다")]
        int sortingOrder = 2;

        [SerializeField, Min(1), Tooltip(
            "개체끼리 겹칠 때 앞뒤를 나누는 단계 수. sortingOrder ~ sortingOrder+span-1 를 쓴다. " +
            "관객(5)을 넘지 않도록 좁게 둔다")]
        int sortingOrderSpan = 3;

        [Header("들썩임 (콤보 0 → 최대)")]
        [SerializeField, Tooltip("콤보가 없을 때의 움직임")]
        CrowdMotionProfile calmMotion = new CrowdMotionProfile
        {
            bobHeight = 0.03f, bobSpeed = 0.9f,
            swayAngle = 2f, swaySpeed = 0.8f,
            jumpHeight = 0f, jumpsPerSecond = 0f,
            airTimeRatio = 0.6f, squash = 0.03f,
        };

        [SerializeField, Tooltip("콤보가 comboForFullMotion 이상일 때의 움직임")]
        CrowdMotionProfile hypeMotion = new CrowdMotionProfile
        {
            bobHeight = 0.09f, bobSpeed = 2.4f,
            swayAngle = 8f, swaySpeed = 2.2f,
            jumpHeight = 0.32f, jumpsPerSecond = 1.8f,
            airTimeRatio = 0.7f, squash = 0.14f,
        };

        [SerializeField, Min(1), Tooltip("이 콤보에서 들썩임이 최대가 된다")]
        int comboForFullMotion = 10;

        [SerializeField, Min(0f), Tooltip("콤보가 바뀔 때 움직임이 따라붙는 속도. 0이면 즉시")]
        float motionBlendSpeed = 3f;

        [Header("모이고 흩어지기")]
        [SerializeField, Min(0.01f), Tooltip("한 명이 나타나거나 사라지는 데 걸리는 시간(초)")]
        float transitionDuration = 0.45f;

        [SerializeField, Tooltip("나타날 때 아래에서 올라오는 거리")]
        float appearRise = 0.5f;

        [SerializeField, Min(0f), Tooltip("여러 명이 한꺼번에 움직일 때 한 명씩 늦추는 간격(초)")]
        float stagger = 0.06f;

        // ---------------- 상태 ----------------

        sealed class Decoration
        {
            public SpriteRenderer Renderer;
            public Vector3 Home;
            public float Scale;
            public float Phase;      // 개체 고유 위상 (다 같이 뛰지 않게)
            public float Visibility; // 0 = 없음, 1 = 완전히 서 있음
            public float Target;
            public float DelayLeft;
        }

        readonly List<Decoration> _decorations = new List<Decoration>();

        /// <summary>
        /// 보여줄 순서. 인덱스 0..N 을 그대로 쓰면 4명일 때 <b>왼쪽 4자리만</b> 켜져 한쪽에 몰린다.
        /// 서로 가장 멀리 떨어진 자리를 차례로 고른 순열이라, 몇 명이 서 있든 폭 전체에 퍼진다.
        /// </summary>
        readonly List<int> _revealOrder = new List<int>();

        int _targetCount;
        float _motionBlend;       // 0 = calm, 1 = hype
        float _targetMotionBlend;
        bool _warnedNoVariants;

        public int VisibleCount => _targetCount;

        void Awake() => BuildDecorations();

        void OnEnable()
        {
            EventBus.Subscribe<AudienceSummaryChanged>(OnAudienceSummaryChanged);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

            RefreshTargetCount();
            RefreshTargetMotion(ComboSystem.HasInstance ? ComboSystem.Instance.CurrentCombo : 0);
            _motionBlend = _targetMotionBlend;
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AudienceSummaryChanged>(OnAudienceSummaryChanged);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        // ---------------- 생성 ----------------

        void BuildDecorations()
        {
            if (_decorations.Count > 0) return;

            if (variants.Count == 0)
            {
                if (!_warnedNoVariants)
                {
                    _warnedNoVariants = true;
                    Debug.LogWarning(
                        "[DecorativeCrowd] 스프라이트가 비어 있습니다. " +
                        "Tools/Art/Setup Decorative Crowd 를 실행하세요.",
                        this);
                }
                return;
            }

            var random = new System.Random(seed);
            int rowCount = Mathf.Max(1, rows);
            int perRow = Mathf.Max(1, Mathf.CeilToInt(maxCount / (float)rowCount));

            for (int i = 0; i < maxCount; i++)
            {
                int row = i / perRow;                 // 0 = 맨 앞줄
                int column = i - row * perRow;
                int columnsInRow = Mathf.Min(perRow, maxCount - row * perRow);

                var go = new GameObject($"Decoration_{i:00}");
                go.transform.SetParent(transform, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = variants[random.Next(variants.Count)];
                renderer.sortingLayerName = sortingLayer;
                // 앞줄이 뒷줄을 가리도록 한다. sortingOrderSpan 안에서만 움직여
                // 뒤쪽 개체가 관객(5) 위로 올라오지 않게 한다
                int depth = Mathf.Clamp(
                    rowCount - 1 - row,
                    0,
                    Mathf.Max(1, sortingOrderSpan) - 1);
                renderer.sortingOrder = sortingOrder + depth;
                renderer.enabled = false;

                // 뒷줄일수록 좁고 작게 — 사진처럼 겹쳐 선 무리로 보이게 한다
                float rowWidth = spreadWidth * Mathf.Pow(rowScaleFalloff, row);
                float step = columnsInRow <= 1 ? 0f : rowWidth / (columnsInRow - 1);
                float x = columnsInRow <= 1 ? 0f : -rowWidth * 0.5f + step * column;

                // 줄마다 반 칸씩 어긋내 앞뒤가 정확히 겹치지 않게 한다
                if (row % 2 == 1) x += step * rowStagger * 0.5f;
                x += ((float)random.NextDouble() * 2f - 1f) * step * 0.18f;

                float y = row * rowSpacing +
                          ((float)random.NextDouble() * 2f - 1f) * verticalJitter;

                float scale = baseScale *
                              Mathf.Pow(rowScaleFalloff, row) *
                              (1f + ((float)random.NextDouble() * 2f - 1f) * scaleJitter);

                _decorations.Add(new Decoration
                {
                    Renderer = renderer,
                    Home = new Vector3(x, y, 0f),
                    Scale = scale,
                    Phase = (float)random.NextDouble() * 10f,
                    Visibility = 0f,
                    Target = 0f,
                });
            }

            BuildRevealOrder();
        }

        /// <summary>
        /// 몇 명만 서 있어도 폭 전체에 퍼져 보이도록, <b>서로 가장 멀리 떨어진 자리부터</b> 채우는 순서를 만든다.
        ///
        /// 인덱스 0..N 을 그대로 켜면 4명일 때 왼쪽 4자리만 켜져 한쪽에 몰린다.
        /// 가운데에서 시작해 매번 "이미 켜진 자리들에서 가장 먼 자리"를 고르면
        /// 인원이 몇이든 고르게 퍼진다.
        /// </summary>
        void BuildRevealOrder()
        {
            _revealOrder.Clear();
            if (_decorations.Count == 0) return;

            var remaining = new List<int>(_decorations.Count);
            for (int i = 0; i < _decorations.Count; i++) remaining.Add(i);

            // 첫 명은 한가운데 (한 명만 있을 때 구석에 혼자 서 있지 않게)
            int first = 0;
            float bestCenterDistance = float.PositiveInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                float distance = Mathf.Abs(_decorations[remaining[i]].Home.x);
                if (distance >= bestCenterDistance) continue;
                bestCenterDistance = distance;
                first = i;
            }
            _revealOrder.Add(remaining[first]);
            remaining.RemoveAt(first);

            while (remaining.Count > 0)
            {
                int bestIndex = 0;
                float bestDistance = -1f;
                for (int i = 0; i < remaining.Count; i++)
                {
                    Vector3 candidate = _decorations[remaining[i]].Home;

                    float nearest = float.PositiveInfinity; // 이미 뽑힌 자리 중 가장 가까운 것
                    for (int p = 0; p < _revealOrder.Count; p++)
                    {
                        float distance =
                            (candidate - _decorations[_revealOrder[p]].Home).sqrMagnitude;
                        if (distance < nearest) nearest = distance;
                    }

                    if (nearest <= bestDistance) continue;
                    bestDistance = nearest;
                    bestIndex = i;
                }

                _revealOrder.Add(remaining[bestIndex]);
                remaining.RemoveAt(bestIndex);
            }
        }

        // ---------------- 이벤트 ----------------

        void OnAudienceSummaryChanged(AudienceSummaryChanged e) => RefreshTargetCount();

        void OnComboChanged(ComboChanged e) => RefreshTargetMotion(e.CurrentCombo);

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current != GameState.Ready) return;

            // 새 공연 시작에는 콤보가 0으로 돌아간다 (ComboChanged 가 안 올 수도 있어 직접 맞춘다)
            RefreshTargetMotion(0);
            RefreshTargetCount();
        }

        /// <summary>
        /// 관객 수 → 장식 인원. 관객 정원 대비 비율로 환산하므로
        /// 관객 시스템의 정원 설정이 바뀌어도 자동으로 따라간다.
        /// </summary>
        void RefreshTargetCount()
        {
            int audienceCount = AudienceRoster.Count;
            int capacity = AudienceRosterSystem.HasInstance
                ? AudienceRosterSystem.Instance.Capacity
                : 0;

            if (audienceCount <= 0) { SetTargetCount(0); return; }

            float ratio = capacity > 0
                ? audienceCount / (capacity * Mathf.Max(0.05f, audienceRatioForFull))
                : 1f;
            int count = Mathf.RoundToInt(Mathf.Clamp01(ratio) * maxCount);

            // 관객이 남아 있는 한 배경이 통째로 비지는 않게 한다
            SetTargetCount(Mathf.Clamp(
                Mathf.Max(count, Mathf.Min(minimumWhenAnyAudience, maxCount)),
                0,
                maxCount));
        }

        void SetTargetCount(int count)
        {
            if (_targetCount == count) return;

            _targetCount = count;

            // 한꺼번에 나타나면 우르르 떠오르는 느낌이라 한 명씩 늦춘다
            int changeIndex = 0;
            for (int order = 0; order < _revealOrder.Count; order++)
            {
                // 인덱스 순이 아니라 "퍼져 보이는 순서"로 켠다 (한쪽에 몰리지 않게)
                Decoration decoration = _decorations[_revealOrder[order]];
                float target = order < _targetCount ? 1f : 0f;
                if (Mathf.Approximately(decoration.Target, target)) continue;

                decoration.Target = target;
                decoration.DelayLeft = stagger * changeIndex;
                changeIndex++;
            }
        }

        void RefreshTargetMotion(int combo)
        {
            _targetMotionBlend = Mathf.Clamp01(combo / (float)Mathf.Max(1, comboForFullMotion));
        }

        // ---------------- 매 프레임 ----------------

        void Update()
        {
            _motionBlend = motionBlendSpeed <= 0f
                ? _targetMotionBlend
                : Mathf.MoveTowards(_motionBlend, _targetMotionBlend, Time.deltaTime * motionBlendSpeed);

            for (int i = 0; i < _decorations.Count; i++)
                TickDecoration(_decorations[i]);
        }

        void TickDecoration(Decoration decoration)
        {
            SpriteRenderer renderer = decoration.Renderer;
            if (renderer == null) return;

            // 1) 모이기·흩어지기
            if (decoration.DelayLeft > 0f) decoration.DelayLeft -= Time.deltaTime;
            else
            {
                decoration.Visibility = Mathf.MoveTowards(
                    decoration.Visibility,
                    decoration.Target,
                    Time.deltaTime / Mathf.Max(0.01f, transitionDuration));
            }

            if (decoration.Visibility <= 0.001f)
            {
                if (renderer.enabled) renderer.enabled = false;
                return;
            }
            if (!renderer.enabled) renderer.enabled = true;

            // 2) 들썩임 — 일반 관객과 같은 공식. 콤보가 오를수록 hype 쪽으로 섞인다
            CrowdMotionEvaluator.EvaluateBlended(
                calmMotion,
                hypeMotion,
                _motionBlend,
                Time.time + decoration.Phase,
                out float height,
                out float sway,
                out float squash);

            // 3) 등장 중이면 아래에서 올라오며 흐릿하게 나타난다
            float eased = decoration.Visibility * decoration.Visibility * (3f - 2f * decoration.Visibility);
            float rise = (1f - eased) * -appearRise;

            renderer.transform.localPosition =
                decoration.Home + new Vector3(0f, height + rise, 0f);
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, sway);

            float scale = decoration.Scale * eased;
            renderer.transform.localScale = new Vector3(
                scale * (1f + squash * 0.5f),
                scale * (1f - squash),
                1f);

            Color color = renderer.color;
            color.a = eased;
            renderer.color = color;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            maxCount = Mathf.Max(0, maxCount);
            comboForFullMotion = Mathf.Max(1, comboForFullMotion);
            sortingOrderSpan = Mathf.Max(1, sortingOrderSpan);
            rows = Mathf.Max(1, rows);
        }

        [ContextMenu("Debug/Show All")] void DebugShowAll() => SetTargetCount(maxCount);
        [ContextMenu("Debug/Hide All")] void DebugHideAll() => SetTargetCount(0);
        [ContextMenu("Debug/Max Motion")] void DebugMaxMotion() => _targetMotionBlend = 1f;
        [ContextMenu("Debug/Calm Motion")] void DebugCalmMotion() => _targetMotionBlend = 0f;
#endif
    }
}
