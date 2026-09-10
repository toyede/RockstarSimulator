using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 투어 맵 선택 화면의 아트·배치·연출 수치. (피그마 "맵 선택" 프레임)
    /// Resources/Tour/TourMapConfig.asset 하나. 아트가 바뀌면 여기 스프라이트만 교체한다.
    /// </summary>
    [CreateAssetMenu(fileName = "TourMapConfig", menuName = "ContextStage/Tour/Map Config")]
    public sealed class TourMapConfig : ScriptableObject
    {
        public const string ResourcesPath = "Tour/TourMapConfig";

        [Serializable]
        public struct RankSprite
        {
            [Tooltip("StageResult.rank 와 같은 라벨 (S/A/B/C/D/F)")]
            public string label;
            public Sprite sprite;
        }

        [Header("아트 (Sprites/0823_art)")]
        [SerializeField] Sprite background;
        [SerializeField, Tooltip("핀 기본 (24×36)")] Sprite pinNormal;
        [SerializeField, Tooltip("핀 활성·호버 (28×40)")] Sprite pinHover;
        [SerializeField, Tooltip("핀 안 마이크 아이콘 (16×16)")] Sprite micIcon;
        [SerializeField, Tooltip("보스 노드 아이콘 (16×16)")] Sprite bossIcon;
        [SerializeField, Tooltip("경로 점·노드 점 (8×8)")] Sprite dot;
        [SerializeField, Tooltip("버스 (48×24)")] Sprite bus;
        [SerializeField, Tooltip("너구리 얼굴")] Sprite raccoon;
        [SerializeField] List<RankSprite> rankSprites = new List<RankSprite>();

        [Header("배치 (1920×1080 기준 정규화 좌표, 좌하단 0,0)")]
        [SerializeField, Tooltip("노드 순서대로. 노드 수보다 적으면 마지막 좌표를 재사용한다")]
        List<Vector2> nodePositions = new List<Vector2>
        {
            new Vector2(0.245f, 0.370f), // 골목 (좌하단 섬)
            new Vector2(0.380f, 0.710f), // 지하 라이브홀 (좌상단 마을)
            new Vector2(0.535f, 0.215f), // 페스티벌 (하단 도시)
            new Vector2(0.700f, 0.335f), // 아레나 (외딴 집)
            new Vector2(0.850f, 0.600f), // 스타디움 (우측 대도시)
        };
        [SerializeField] Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Header("픽셀 배율")]
        [SerializeField, Min(1f), Tooltip("핀·마이크·버스의 화면 배율 (도트 1px = 이 값 px)")]
        float pixelScale = 5f;
        [SerializeField, Min(0.1f), Tooltip("버스만 추가로 곱하는 배율 (pixelScale × 이 값)")]
        float busScale = 0.65f;
        [SerializeField, Tooltip("노드 점 기준 버스 위치 오프셋(px). 핀 바로 아래에 선다")]
        Vector2 busOffset = new Vector2(5f, -50f);
        [SerializeField, Min(8f)] float raccoonSize = 75f;
        [SerializeField, Tooltip("버스 기준 너구리 얼굴 위치 오프셋(px)")]
        Vector2 raccoonOffset = new Vector2(75f, 25f);
        [SerializeField, Min(8f)] float rankSize = 90f;
        [SerializeField, Tooltip("핀 기준 랭크 글자 위치(px)")]
        Vector2 rankOffset = new Vector2(78f, 44f);

        [Header("점")]
        [SerializeField, Min(2f)] float visitedDotSize = 40f;
        [SerializeField, Min(2f)] float futureDotSize = 64f;
        [SerializeField] Color visitedDotColor = Color.white;
        [SerializeField] Color futureDotColor = new Color(0.85f, 0.85f, 0.9f, 0.55f);
        [SerializeField] Color lockedDotColor = new Color(0.85f, 0.85f, 0.9f, 0.3f);
        [SerializeField, Min(4f), Tooltip("경로 빗금 간격(px)")] float dashSpacing = 26f;
        [SerializeField, Tooltip("빗금 하나의 길이×두께(px). 경로 방향으로 회전한다")]
        Vector2 dashSize = new Vector2(14f, 5f);
        [SerializeField] Color dashColor = new Color(1f, 1f, 1f, 0.75f);

        [Header("연출 (피그마 모션)")]
        [SerializeField, Min(0.05f), Tooltip("너구리·버스 2프레임 교대 간격(초). 피그마 400ms")]
        float frameInterval = 0.4f;
        [SerializeField, Min(0f), Tooltip("너구리 얼굴 2프레임: 좌로 이 각도 ↔ 우로 이 각도 (도)")]
        float raccoonTiltDegrees = 30f;
        [SerializeField, Min(0f), Tooltip("버스가 2프레임 교대 시 위아래로 움직이는 픽셀 (0이면 정지)")]
        float bobPixels = 3f;
        [SerializeField, Min(0.1f), Tooltip("노드 한 칸 이동 시간(초). 피그마 3000ms Linear")]
        float travelSecondsPerSegment = 3f;
        [SerializeField, Min(0f), Tooltip("클릭 후 출발까지 지연(초). 피그마 300ms")]
        float travelStartDelay = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("도착 핀이 작은 크기에서 튀어나오는 시간(초). 피그마 200ms Ease out")]
        float pinPopDuration = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("도착 핀이 등장을 시작하는 크기 배율")]
        float pinPopStartScale = 0.3f;
        [SerializeField, Min(0.05f), Tooltip("(예전 점멸 간격 — 지금은 쓰지 않는다)")]
        float pinBlinkInterval = 0.35f;
        [SerializeField, Min(0f), Tooltip("도착 핀 등장 후 대화로 넘어가기까지 대기(초)")]
        float arrivalBlinkDuration = 1.6f;
        [SerializeField, Min(1f), Tooltip("마우스를 올린 노드의 핀·아이콘 크기 배율. 평소에는 1")]
        float pinHoverScale = 1.25f;
        [SerializeField, Min(0.01f), Tooltip("호버 크기 전환 시간(초)")]
        float pinHoverDuration = 0.12f;
        [SerializeField, Tooltip("이동 방향에 따라 버스를 좌우 반전할지")]
        bool flipBusByDirection = false;

        public Sprite Background => background;
        public Sprite PinNormal => pinNormal;
        public Sprite PinHover => pinHover != null ? pinHover : pinNormal;
        public Sprite MicIcon => micIcon;
        public Sprite BossIcon => bossIcon != null ? bossIcon : micIcon;
        public Sprite Dot => dot;
        public Sprite Bus => bus;
        public Sprite Raccoon => raccoon;
        public IReadOnlyList<Vector2> NodePositions => nodePositions;
        public Vector2 ReferenceResolution => referenceResolution;
        public float PixelScale => pixelScale;
        public float BusScale => busScale;
        public Vector2 BusOffset => busOffset;
        public float RaccoonSize => raccoonSize;
        public Vector2 RaccoonOffset => raccoonOffset;
        public float RankSize => rankSize;
        public Vector2 RankOffset => rankOffset;
        public float VisitedDotSize => visitedDotSize;
        public float FutureDotSize => futureDotSize;
        public Color VisitedDotColor => visitedDotColor;
        public Color FutureDotColor => futureDotColor;
        public Color LockedDotColor => lockedDotColor;
        public float DashSpacing => dashSpacing;
        public Vector2 DashSize => dashSize;
        public Color DashColor => dashColor;
        public float FrameInterval => frameInterval;
        public float RaccoonTiltDegrees => raccoonTiltDegrees;
        public float BobPixels => bobPixels;
        public float TravelSecondsPerSegment => travelSecondsPerSegment;
        public float TravelStartDelay => travelStartDelay;
        public float PinPopDuration => pinPopDuration;
        public float PinPopStartScale => pinPopStartScale;
        public float PinBlinkInterval => pinBlinkInterval;
        public float ArrivalBlinkDuration => arrivalBlinkDuration;
        public float PinHoverScale => pinHoverScale;
        public float PinHoverDuration => pinHoverDuration;
        public bool FlipBusByDirection => flipBusByDirection;

        public static TourMapConfig LoadDefault() => Resources.Load<TourMapConfig>(ResourcesPath);

        /// <summary>노드 인덱스의 정규화 좌표. 목록보다 인덱스가 크면 마지막 좌표.</summary>
        public Vector2 GetNodePosition(int index)
        {
            if (nodePositions == null || nodePositions.Count == 0) return new Vector2(0.5f, 0.5f);
            return nodePositions[Mathf.Clamp(index, 0, nodePositions.Count - 1)];
        }

        public Sprite GetRankSprite(string label)
        {
            if (string.IsNullOrWhiteSpace(label) || rankSprites == null) return null;
            for (int i = 0; i < rankSprites.Count; i++)
            {
                if (string.Equals(rankSprites[i].label, label.Trim(), StringComparison.OrdinalIgnoreCase))
                    return rankSprites[i].sprite;
            }
            return null;
        }

        public bool HasArt => background != null && pinNormal != null && dot != null && bus != null;

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] 비어 있는 칸만 채운다 (아트가 직접 넣은 스프라이트는 유지).</summary>
        public void EditorFillArt(
            Sprite bg, Sprite pin, Sprite pinActive, Sprite mic, Sprite boss,
            Sprite dotSprite, Sprite busSprite, Sprite raccoonSprite,
            List<RankSprite> ranks)
        {
            if (background == null) background = bg;
            if (pinNormal == null) pinNormal = pin;
            if (pinHover == null) pinHover = pinActive;
            if (micIcon == null) micIcon = mic;
            if (bossIcon == null) bossIcon = boss;
            if (dot == null) dot = dotSprite;
            if (bus == null) bus = busSprite;
            if (raccoon == null) raccoon = raccoonSprite;
            if ((rankSprites == null || rankSprites.Count == 0) && ranks != null) rankSprites = ranks;
        }
#endif
    }
}
