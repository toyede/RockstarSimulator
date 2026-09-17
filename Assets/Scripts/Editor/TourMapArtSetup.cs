using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 9/10 아트 적용.
    ///
    ///   Tools/Tour/Apply 0910 Art
    ///     1. Sprites/0910_art 의 배경·스탠딩일러·증강아이콘·카드 임포트 교정 (Sprite·Single·Point·비압축)
    ///     2. TourMapConfig: 배경 = map_background_v2, 노드 5개 좌표를 새 맵에 맞게
    ///     3. TourHub 씬: [TourMap] 에 MapContent 컨테이너를 만들어 배경·빗금·노드·버스를 그 아래로 (도입 확대·결승 축소용),
    ///        배경 스프라이트 교체, 노드 위치 갱신. [Dialogue] 의 PortraitLeft/Right 슬롯을 스탠딩 일러 크기로
    ///     4. StageVisualCatalog: stage_01/02/03 배경(+2·3 조명 오버레이) 교체. (stage4_* 파일은 스테이지 3 용)
    ///     5. Main 씬의 stage_01~03 소품 세트를 새 카탈로그로 다시 생성
    ///   여러 번 실행해도 안전하다.
    /// </summary>
    public static class TourMapArtSetup
    {
        const string ArtRoot = "Assets/Sprites/0910_art";
        const string HubScenePath = "Assets/Scenes/TourHub.unity";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string ConfigPath = "Assets/Resources/Tour/TourMapConfig.asset";

        // 새 맵(3300×1856)에서의 노드 위치 (정규화, 왼쪽 아래 원점)
        static readonly Vector2[] NodePositions =
        {
            new Vector2(0.150f, 0.565f), // 1 골목 버스킹 — 왼쪽 작은 섬 마을
            new Vector2(0.260f, 0.787f), // 2 지하 라이브홀 — 큰 섬 위쪽 마을
            new Vector2(0.360f, 0.476f), // 3 페스티벌 — 큰 섬 아래쪽 마을
            new Vector2(0.575f, 0.707f), // 4 아레나 — 가운데 위 도시 섬
            new Vector2(0.795f, 0.262f), // 5 보스 — 오른쪽 아래 돔 경기장
        };

        [MenuItem("Tools/Tour/Apply 0910 Art", false, 23)]
        public static void Apply()
        {
            FixImports();

            var config = AssetDatabase.LoadAssetAtPath<TourMapConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError("[0910Art] TourMapConfig 가 없습니다. Tools/Tour/Setup Tour Map 을 먼저 실행하세요.");
                return;
            }
            Sprite map = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/배경/map_background_v2.png");
            config.EditorSetMap(map, new List<Vector2>(NodePositions));
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            ApplyHubScene(config, map);
            ApplyStageCatalog();
            RebuildStageSets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[0910Art] 적용 완료: 맵 배경·노드 좌표·MapContent·스탠딩 슬롯·스테이지 1~3 배경·소품 세트.");
        }

        // ---------------- 1. 임포트 ----------------

        static void FixImports()
        {
            foreach (string folder in new[] { "배경", "스탠딩일러", "증강아이콘", "카드" })
            {
                string dir = $"{ArtRoot}/{folder}";
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                    int maxSize = folder == "배경" ? 4096 : 2048;
                    if (importer.maxTextureSize < maxSize) { importer.maxTextureSize = maxSize; changed = true; }
                    if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
                    if (changed) importer.SaveAndReimport();
                }
            }
        }

        // ---------------- 3. TourHub 씬 ----------------

        static void ApplyHubScene(TourMapConfig config, Sprite map)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != HubScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            }

            TourMapView view = Object.FindFirstObjectByType<TourMapView>(FindObjectsInactive.Include);
            if (view == null)
            {
                Debug.LogWarning("[0910Art] TourHub 에 TourMapView 가 없어 맵 씬은 건너뜁니다 (Tools/Tour/Setup Tour Map Scene).");
            }
            else
            {
                RestructureMap(view, config, map);
            }

            ApplyPortraitSlots();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void RestructureMap(TourMapView view, TourMapConfig config, Sprite map)
        {
            var so = new SerializedObject(view);
            var background = so.FindProperty("mapBackground").objectReferenceValue as Image;
            var pathRoot = so.FindProperty("pathRoot").objectReferenceValue as RectTransform;
            var nodeRoot = so.FindProperty("nodeRoot").objectReferenceValue as RectTransform;
            var busRoot = so.FindProperty("busRoot").objectReferenceValue as RectTransform;
            var content = so.FindProperty("mapContent").objectReferenceValue as RectTransform;

            var root = (RectTransform)view.transform;
            if (content == null)
            {
                Transform existing = root.Find("MapContent");
                if (existing != null) content = (RectTransform)existing;
            }
            if (content == null)
            {
                var go = new GameObject("MapContent", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create MapContent");
                content = go.GetComponent<RectTransform>();
                content.SetParent(root, false);
                content.SetAsFirstSibling();
            }
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = config.ReferenceResolution;
            content.anchoredPosition = Vector2.zero;
            content.localScale = Vector3.one;

            // 배경 → 빗금 → 노드 → 버스 순서로 컨테이너 아래에
            Reparent(background != null ? background.rectTransform : null, content);
            Reparent(pathRoot, content);
            Reparent(nodeRoot, content);
            Reparent(busRoot, content);

            if (background != null && map != null)
            {
                background.sprite = map;
                background.preserveAspect = false;
                Stretch(background.rectTransform);
                EditorUtility.SetDirty(background);
            }

            // 노드 위치 갱신
            SerializedProperty slots = so.FindProperty("nodeSlots");
            Vector2 size = config.ReferenceResolution;
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slotRoot = slots.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue as RectTransform;
                if (slotRoot == null) continue;
                Vector2 normalized = config.GetNodePosition(i);
                slotRoot.anchoredPosition = new Vector2((normalized.x - 0.5f) * size.x, (normalized.y - 0.5f) * size.y);
                EditorUtility.SetDirty(slotRoot);
            }

            if (busRoot != null && slots.arraySize > 0)
            {
                var first = slots.GetArrayElementAtIndex(0).FindPropertyRelative("root").objectReferenceValue as RectTransform;
                if (first != null) busRoot.anchoredPosition = first.anchoredPosition + config.BusOffset;
                EditorUtility.SetDirty(busRoot);
            }

            so.FindProperty("mapContent").objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        static void Reparent(RectTransform target, RectTransform parent)
        {
            if (target == null || parent == null || target.parent == parent) return;
            target.SetParent(parent, false);
            EditorUtility.SetDirty(target);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>[Dialogue] 의 스탠딩 슬롯: 대화 상자 위, 왼쪽/오른쪽 아래 기준, 세로 620px.</summary>
        static void ApplyPortraitSlots()
        {
            GameObject dialogueRoot = GameObject.Find("[Dialogue]");
            if (dialogueRoot == null) return;
            DialoguePanel panel = dialogueRoot.GetComponentInChildren<DialoguePanel>(true);
            if (panel == null) return;

            var so = new SerializedObject(panel);
            var left = so.FindProperty("portraitLeft").objectReferenceValue as Image;
            var right = so.FindProperty("portraitRight").objectReferenceValue as Image;
            PlacePortrait(left, isLeft: true);
            PlacePortrait(right, isLeft: false);
        }

        static void PlacePortrait(Image image, bool isLeft)
        {
            if (image == null) return;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(isLeft ? 0f : 1f, 0f);
            rect.pivot = new Vector2(isLeft ? 0f : 1f, 0f);
            rect.anchoredPosition = new Vector2(isLeft ? 120f : -120f, 230f);
            rect.sizeDelta = new Vector2(560f, 640f);
            image.preserveAspect = true;
            image.raycastTarget = false;
            EditorUtility.SetDirty(image);
        }

        // ---------------- 4. 스테이지 배경 ----------------

        static void ApplyStageCatalog()
        {
            StageVisualCatalog catalog = StageVisualCatalog.LoadDefault();
            if (catalog == null)
            {
                Debug.LogWarning("[0910Art] StageVisualCatalog 가 없어 스테이지 배경은 건너뜁니다.");
                return;
            }

            Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/배경/{file}");

            SetStage(catalog, "stage_01", Load("stage1_background_v2.png"), null);
            SetStage(catalog, "stage_02", Load("stage2_background_v2.png"), Load("stage2_stage_overlay_v2.png"));
            // stage4_* 파일이 스테이지 3 (페스티벌) 용
            SetStage(catalog, "stage_03", Load("stage4_background_v2.png"), Load("stage4_stage_overlay_v2.png"));
            EditorUtility.SetDirty(catalog);
        }

        static void SetStage(StageVisualCatalog catalog, string stageId, Sprite background, Sprite overlay)
        {
            StageVisualEntry entry = catalog.EditorGetOrAddEntry(stageId);
            if (background != null) entry.backgroundBase = background;
            entry.lightOverlays = new List<Sprite>();
            if (overlay != null) entry.lightOverlays.Add(overlay);
        }

        // ---------------- 5. 소품 세트 ----------------

        static void RebuildStageSets()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            StageBackgroundView view = Object.FindFirstObjectByType<StageBackgroundView>(FindObjectsInactive.Include);
            if (view == null) return;
            Transform root = view.transform.Find("[StageSets]");
            if (root != null)
            {
                foreach (string stageId in new[] { "stage_01", "stage_02", "stage_03" })
                {
                    Transform set = root.Find(stageId);
                    if (set != null) Undo.DestroyObjectImmediate(set.gameObject);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            StageSetSetup.Setup();
        }
    }
}
