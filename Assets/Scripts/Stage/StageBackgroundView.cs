using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 스테이지 배경 교체. (에셋 명세 §3 레이어 구조 / §13 StageVisualCatalog)
    ///
    /// - Base 배경: 기존 night_city_ground SpriteRenderer 의 스프라이트를 바꾼다 (Order 0 유지)
    /// - 조명 레이어: Base 위에 자식 SpriteRenderer 로 겹친다 (Order 1)
    /// - 전경 소품: 카메라 화면 좌우 하단 가장자리에 붙인다 (Order 46, 밴드 45 위)
    /// - 장식 관객: 공연장 전용 스프라이트가 있으면 DecorativeCrowd 에 교체 요청
    ///
    /// 스테이지는 StageRuntimeApplied 이벤트 또는 StageRuntimeDirector.CurrentStage 로 알아낸다.
    /// 투어 없이 실행하면 previewStage(있으면)를 쓰고, 그것도 없으면 기존 배경 그대로 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageBackgroundView : MonoBehaviour
    {
        [Header("기존 배경 (씬의 SpriteRenderer)")]
        [SerializeField, Tooltip("Base 배경. 스프라이트만 교체된다")]
        SpriteRenderer baseRenderer;

        [SerializeField, Tooltip("기존 무대 조명 그림(stage_lights). 스테이지 조명 레이어가 있으면 숨긴다")]
        SpriteRenderer defaultLightRenderer;

        [SerializeField, Tooltip("장식 관객. 공연장 전용 스프라이트가 있을 때만 교체")]
        DecorativeCrowd decorativeCrowd;

        [Header("정렬")]
        [SerializeField] string sortingLayer = "Default";
        [SerializeField, Tooltip("조명 레이어 Sorting Order (배경 0, 장식 관객 2~4 사이)")]
        int lightOverlaySortingOrder = 1;
        [SerializeField, Tooltip("전경 소품 Sorting Order (밴드 45 위)")]
        int foregroundSortingOrder = 46;
        [SerializeField] bool hideDefaultLightsWhenOverlaysExist = true;

        [Header("씬 소품 세트")]
        [SerializeField, Tooltip("Background/[StageSets]. 자식 StageSet 중 stageId 가 맞는 것을 켠다. 세트가 없는 스테이지는 카탈로그로 런타임 생성")]
        Transform stageSetsRoot;

        [Header("미리보기 / 폴백")]
        [SerializeField, Tooltip("투어 없이 Main 을 실행할 때 보여줄 스테이지. 비우면 기존 배경 유지")]
        StageDefinition previewStage;

        readonly List<SpriteRenderer> _spawned = new List<SpriteRenderer>();
        StageSet _activeSet;

        /// <summary>지금 켜진 씬 소품 세트 (없으면 null). 소품 애니메이션을 걸 때 여기서 Props 를 얻는다.</summary>
        public StageSet ActiveSet => _activeSet;
        Sprite _originalBaseSprite;
        bool _originalLightEnabled = true;
        bool _cachedOriginal;
        string _appliedStageId;

        public string AppliedStageId => _appliedStageId;

        void OnEnable() => EventBus.Subscribe<StageRuntimeApplied>(OnStageApplied);
        void OnDisable() => EventBus.Unsubscribe<StageRuntimeApplied>(OnStageApplied);

        void Start()
        {
            if (!string.IsNullOrEmpty(_appliedStageId)) return;

            StageDefinition stage = StageRuntimeDirector.CurrentStage;
            if (stage == null) stage = previewStage;
            if (stage != null) Apply(stage.StageId);
        }

        void OnStageApplied(StageRuntimeApplied e)
        {
            if (e.Stage != null) Apply(e.StageId);
        }

        /// <summary>카탈로그에서 stageId 의 비주얼을 찾아 적용한다. 없으면 false (배경 유지).</summary>
        public bool Apply(string stageId)
        {
            if (!StageVisualCatalog.TryResolve(stageId, out StageVisualEntry entry))
            {
                Debug.LogWarning(
                    $"[StageBackground] '{stageId}' 비주얼 항목이 없습니다. Tools/Tour/Setup Stage Runtime 을 실행하세요.",
                    this);
                return false;
            }

            ApplyEntry(entry);
            _appliedStageId = stageId;
            return true;
        }

        /// <summary>기존 배경(night_city_ground)으로 되돌린다.</summary>
        public void Restore()
        {
            ClearSpawned();
            ActivateStageSet(null);
            if (_cachedOriginal)
            {
                if (baseRenderer != null) baseRenderer.sprite = _originalBaseSprite;
                if (defaultLightRenderer != null) defaultLightRenderer.enabled = _originalLightEnabled;
            }
            _appliedStageId = null;
        }

        void ApplyEntry(StageVisualEntry entry)
        {
            CacheOriginal();
            ClearSpawned();

            if (baseRenderer != null && entry.backgroundBase != null)
                baseRenderer.sprite = entry.backgroundBase;

            // 씬에 소품 세트가 있으면 그것을 켜고(위치·크기는 씬에서 조절), 없을 때만 카탈로그로 생성한다
            StageSet set = ActivateStageSet(entry.stageId);
            if (set != null)
            {
                if (baseRenderer != null && set.BaseBackgroundOverride != null) baseRenderer.sprite = set.BaseBackgroundOverride;
                if (defaultLightRenderer != null)
                    defaultLightRenderer.enabled = !(hideDefaultLightsWhenOverlaysExist && set.HasLights);
            }
            else
            {
                Transform overlayParent = baseRenderer != null ? baseRenderer.transform : transform;
                int overlayCount = 0;
                if (entry.lightOverlays != null)
                {
                    for (int i = 0; i < entry.lightOverlays.Count; i++)
                    {
                        Sprite sprite = entry.lightOverlays[i];
                        if (sprite == null) continue;

                        SpriteRenderer overlay = SpawnRenderer($"StageLight_{i:00}", overlayParent, sprite,
                            lightOverlaySortingOrder + i);
                        overlay.transform.localPosition = Vector3.zero;
                        overlay.transform.localScale = Vector3.one;
                        overlayCount++;
                    }
                }

                if (defaultLightRenderer != null)
                    defaultLightRenderer.enabled = !(hideDefaultLightsWhenOverlaysExist && overlayCount > 0);

                PlaceForeground(entry.foregroundLeft, "StageForeground_Left", left: true);
                PlaceForeground(entry.foregroundRight, "StageForeground_Right", left: false);
            }

            if (decorativeCrowd != null &&
                entry.decorativeCrowdVariants != null &&
                entry.decorativeCrowdVariants.Count > 0)
            {
                decorativeCrowd.ReplaceVariants(entry.decorativeCrowdVariants);
            }
        }

        /// <summary>stageId 가 맞는 세트만 켜고 나머지는 끈다. null 이면 전부 끈다. 켠 세트를 돌려준다 (없으면 null).</summary>
        StageSet ActivateStageSet(string stageId)
        {
            _activeSet = null;
            if (stageSetsRoot == null) return null;

            StageSet[] sets = stageSetsRoot.GetComponentsInChildren<StageSet>(true);
            for (int i = 0; i < sets.Length; i++)
            {
                StageSet set = sets[i];
                if (set == null) continue;
                bool match = !string.IsNullOrEmpty(stageId) && set.StageId == stageId;
                set.gameObject.SetActive(match);
                if (match) _activeSet = set;
            }
            return _activeSet;
        }

        void PlaceForeground(Sprite sprite, string name, bool left)
        {
            if (sprite == null) return;

            Camera camera = Camera.main;
            SpriteRenderer renderer = SpawnRenderer(name, transform, sprite, foregroundSortingOrder);

            if (camera == null || !camera.orthographic)
            {
                renderer.transform.position = Vector3.zero;
                return;
            }

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 center = camera.transform.position;
            Vector2 extents = sprite.bounds.extents;

            float x = left
                ? center.x - halfWidth + extents.x
                : center.x + halfWidth - extents.x;
            float y = center.y - halfHeight + extents.y;
            renderer.transform.position = new Vector3(x, y, 0f);
            renderer.transform.localScale = Vector3.one;
        }

        SpriteRenderer SpawnRenderer(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            _spawned.Add(renderer);
            return renderer;
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
            }
            _spawned.Clear();
        }

        void CacheOriginal()
        {
            if (_cachedOriginal) return;
            _cachedOriginal = true;
            if (baseRenderer != null) _originalBaseSprite = baseRenderer.sprite;
            if (defaultLightRenderer != null) _originalLightEnabled = defaultLightRenderer.enabled;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(SpriteRenderer baseSprite, SpriteRenderer lights, DecorativeCrowd crowd)
        {
            baseRenderer = baseSprite;
            defaultLightRenderer = lights;
            decorativeCrowd = crowd;
        }

        /// <summary>[에디터 셋업 전용] 씬 소품 세트 루트 연결.</summary>
        public void EditorSetStageSetsRoot(Transform root) => stageSetsRoot = root;

        /// <summary>[에디터 셋업 전용] 소품 세트를 만들 때 런타임과 같은 기준점을 쓰기 위해 노출.</summary>
        public SpriteRenderer EditorBaseRenderer => baseRenderer;
        public int EditorLightOverlaySortingOrder => lightOverlaySortingOrder;
        public int EditorForegroundSortingOrder => foregroundSortingOrder;
        public string EditorSortingLayer => sortingLayer;
#endif
    }
}
