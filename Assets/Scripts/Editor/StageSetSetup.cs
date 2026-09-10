using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 스테이지 소품 세트 셋업.
    ///
    ///   Tools/Tour/Setup Stage Sets
    ///     Main 씬 Background 아래 [StageSets] 를 만들고, StageVisualCatalog 의 스테이지마다
    ///     `<stageId>` 오브젝트(StageSet) + 조명 레이어·전경 소품 SpriteRenderer 를 씬 오브젝트로 배치한다.
    ///     초기 위치는 런타임 생성과 같은 자리(조명 = Base 위치, 소품 = 화면 좌우 하단 가장자리).
    ///     이미 있는 세트는 건드리지 않는다 — 위치·크기·회전은 씬에서 고치고 저장하면 된다.
    ///   실행 후 스테이지 소품은 StageBackgroundView 가 카탈로그 대신 이 세트를 켠다.
    /// </summary>
    public static class StageSetSetup
    {
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string RootName = "[StageSets]";
        const float ReferenceAspect = 1920f / 1080f;

        [MenuItem("Tools/Tour/Setup Stage Sets", false, 15)]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            StageBackgroundView view = Object.FindFirstObjectByType<StageBackgroundView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogError("[StageSets] StageBackgroundView 가 없습니다. Tools/Tour/Setup Stage Runtime 을 먼저 실행하세요.");
                return;
            }

            StageVisualCatalog catalog = StageVisualCatalog.LoadDefault();
            if (catalog == null)
            {
                Debug.LogError("[StageSets] StageVisualCatalog 가 없습니다. Tools/Tour/Setup Stage Runtime 을 먼저 실행하세요.");
                return;
            }

            Transform root = view.transform.Find(RootName);
            if (root == null)
            {
                var rootObject = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Stage Sets");
                rootObject.transform.SetParent(view.transform, false);
                rootObject.transform.localPosition = Vector3.zero;
                root = rootObject.transform;
            }

            Camera camera = Camera.main;
            if (camera == null) camera = Object.FindFirstObjectByType<Camera>();

            int created = 0;
            foreach (StageVisualEntry entry in catalog.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.stageId)) continue;
                if (root.Find(entry.stageId) != null) continue;
                BuildSet(root, entry, view, camera);
                created++;
            }

            view.EditorSetStageSetsRoot(root);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeTransform = root;
            Debug.Log($"[StageSets] 소품 세트 {created}개 생성. 위치·크기는 Background/{RootName}/<stageId> 아래 오브젝트에서 조절한다.");
        }

        static void BuildSet(Transform root, StageVisualEntry entry, StageBackgroundView view, Camera camera)
        {
            var setObject = new GameObject(entry.stageId);
            setObject.transform.SetParent(root, false);
            setObject.transform.localPosition = Vector3.zero;
            var set = setObject.AddComponent<StageSet>();

            SpriteRenderer baseRenderer = view.EditorBaseRenderer;
            Vector3 basePosition = baseRenderer != null ? baseRenderer.transform.position : Vector3.zero;

            var lights = new List<SpriteRenderer>();
            if (entry.lightOverlays != null)
            {
                for (int i = 0; i < entry.lightOverlays.Count; i++)
                {
                    Sprite sprite = entry.lightOverlays[i];
                    if (sprite == null) continue;
                    lights.Add(CreateRenderer(setObject.transform, sprite.name, sprite, basePosition,
                        view.EditorSortingLayer, view.EditorLightOverlaySortingOrder + i));
                }
            }

            var props = new List<SpriteRenderer>();
            if (entry.foregroundLeft != null)
                props.Add(CreateRenderer(setObject.transform, entry.foregroundLeft.name, entry.foregroundLeft,
                    EdgePosition(camera, entry.foregroundLeft, left: true), view.EditorSortingLayer, view.EditorForegroundSortingOrder));
            if (entry.foregroundRight != null)
                props.Add(CreateRenderer(setObject.transform, entry.foregroundRight.name, entry.foregroundRight,
                    EdgePosition(camera, entry.foregroundRight, left: false), view.EditorSortingLayer, view.EditorForegroundSortingOrder));

            set.EditorConfigure(entry.stageId, lights, props);
            setObject.SetActive(false); // StageBackgroundView 가 스테이지에 맞춰 켠다
            EditorUtility.SetDirty(set);
        }

        /// <summary>런타임 PlaceForeground 와 같은 자리: 화면 좌우 하단 가장자리 (16:9 기준).</summary>
        static Vector3 EdgePosition(Camera camera, Sprite sprite, bool left)
        {
            float halfHeight = camera != null && camera.orthographic ? camera.orthographicSize : 5f;
            float halfWidth = halfHeight * ReferenceAspect;
            Vector3 center = camera != null ? camera.transform.position : Vector3.zero;
            Vector2 extents = sprite.bounds.extents;
            float x = left ? center.x - halfWidth + extents.x : center.x + halfWidth - extents.x;
            float y = center.y - halfHeight + extents.y;
            return new Vector3(x, y, 0f);
        }

        static SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, Vector3 worldPosition, string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            go.transform.localScale = Vector3.one;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
