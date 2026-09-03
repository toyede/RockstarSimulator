using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// TourHub 씬에 맵 선택 화면을 <b>게임오브젝트로</b> 배치한다.
    ///
    ///   Tools/Tour/Setup Tour Map Scene
    ///     TourHub 씬에 [TourMap] 캔버스(정렬 90) → TourMapView → MapBackground · Path · Nodes/Node_01~05(Dot·Pin·Icon·Rank) · Bus(BusSprite·Raccoon) · TitleButton
    ///     을 만들고 직렬화 참조를 채운 뒤 씬을 저장한다.
    ///
    /// 이후 위치·크기는 하이어라키/인스펙터에서 직접 고치면 된다. 노드 위치는 Node_xx 오브젝트 위치가 기준이고
    /// 빗금은 그 위치를 따라 런타임에 깔린다. 이미 있으면 덮어쓰지 않는다 (다시 만들려면 [TourMap] 을 지우고 실행).
    /// </summary>
    public static class TourMapSceneSetup
    {
        const string HubScenePath = "Assets/Scenes/TourHub.unity";
        const string RootName = "[TourMap]";
        const int CanvasSortingOrder = 90; // 임시 허브 패널(100) 아래, 배경만 가린다

        [MenuItem("Tools/Tour/Setup Tour Map Scene", false, 22)]
        public static void Setup()
        {
            var config = AssetDatabase.LoadAssetAtPath<TourMapConfig>("Assets/Resources/Tour/TourMapConfig.asset");
            if (config == null)
            {
                Debug.LogError("[TourMap] Resources/Tour/TourMapConfig.asset 이 없습니다. Tools/Tour/Setup Tour Map 을 먼저 실행하세요.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != HubScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            }

            GameObject root = GameObject.Find(RootName);
            if (root != null && root.GetComponentInChildren<TourMapView>(true) != null)
            {
                Debug.Log($"[TourMap] '{RootName}' 이 이미 있습니다. 다시 만들려면 지우고 실행하세요.", root);
                Selection.activeGameObject = root;
                return;
            }

            if (root == null)
            {
                root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(root, "Create TourMap");
            }

            var canvas = EnsureComponent<Canvas>(root);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasSortingOrder;
            var scaler = EnsureComponent<CanvasScaler>(root);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = config.ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            EnsureComponent<GraphicRaycaster>(root);

            var viewObject = new GameObject("TourMapView", typeof(RectTransform));
            viewObject.transform.SetParent(root.transform, false);
            var view = viewObject.AddComponent<TourMapView>();
            view.EditorBuildSceneObjects(config, ProjectFontTool.TmpFont);
            root.SetActive(true);        // 캔버스 루트는 항상 켜 둔다
            viewObject.SetActive(false); // 뷰만 끈다. TourPrototypeUI 가 Map 단계에서 켠다

            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = viewObject;

            Debug.Log(
                "[TourMap] TourHub 씬에 맵 화면을 게임오브젝트로 배치했습니다. " +
                "Node_xx / Bus / Raccoon 위치·크기는 인스펙터에서 바로 고치면 됩니다.",
                view);
        }
    }
}
