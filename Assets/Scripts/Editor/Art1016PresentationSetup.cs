using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextStage.EditorTools
{
    public static class Art1016PresentationSetup
    {
        [MenuItem("Tools/Art/Apply 1016 Standing and Broadcast Art")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "Main" || scene.isDirty)
                throw new InvalidOperationException("저장된 Main Edit Mode에서 실행하세요. 사용자 미저장 상태는 덮어쓰지 않습니다.");
            var standing = DialoguePortraitCatalog.LoadDefault();
            var catalog = StageVisualCatalog.LoadDefault();
            var stage = UnityEngine.Object.FindObjectsByType<StageSet>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).FirstOrDefault(s => s.StageId == "stage_04");
            var backdrop = UnityEngine.Object.FindFirstObjectByType<StageBackgroundView>();
            if (standing == null || catalog == null || stage == null || backdrop == null)
                throw new InvalidOperationException("기존 카탈로그/방송국 StageSet 참조가 필요합니다.");
            var hedgehog = ImportSprite("Assets/Sprites/CharactersStanding/Hedgehog_Normal.png");
            var skunk = ImportSprite("Assets/Sprites/CharactersStanding/Skunk_Normal.png");
            var background = ImportSprite("Assets/Sprites/1016_art/1016_art/stage3_background.png");
            var floor = ImportSprite("Assets/Sprites/1016_art/1016_art/stage3_stage_overlay.png");
            standing.Upsert("hedgehog", Array.Empty<string>(), new[] { hedgehog }, .45f);
            standing.Upsert("skunk", Array.Empty<string>(), new[] { skunk }, .45f);
            EditorUtility.SetDirty(standing); AssetDatabase.SaveAssetIfDirty(standing);
            var entry = catalog.EditorGetOrAddEntry("stage_04");
            entry.backgroundBase = background;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);

            var so = new SerializedObject(stage);
            so.FindProperty("baseBackgroundOverride").objectReferenceValue = background;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (stage.SceneBackground != null) stage.SceneBackground.sprite = background;
            var overlay = stage.transform.Find("BroadcastFloorOverlay");
            if (overlay == null)
            {
                var go = new GameObject("BroadcastFloorOverlay", typeof(SpriteRenderer));
                go.transform.SetParent(stage.transform, false); overlay = go.transform;
            }
            var source = backdrop.EditorBaseRenderer;
            overlay.position = source.transform.position; overlay.rotation = source.transform.rotation;
            overlay.localScale = source.transform.lossyScale;
            var renderer = overlay.GetComponent<SpriteRenderer>();
            renderer.sprite = floor; renderer.sortingLayerID = source.sortingLayerID;
            renderer.sortingOrder = source.sortingOrder + 2; // 바닥/앰프, 관객·밴드보다 뒤
            foreach (var presenter in UnityEngine.Object.FindObjectsByType<AudienceRosterPresenter>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                var p = new SerializedObject(presenter);
                p.FindProperty("layoutHalfWidth").floatValue = 8.4f;
                p.ApplyModifiedPropertiesWithoutUndo();
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Audience/AudienceMember.prefab");
            var actor = new SerializedObject(prefab.GetComponent<AudienceMemberActor>());
            actor.FindProperty("calmWanderRadius").floatValue = .55f;
            actor.FindProperty("calmWalkSpeed").floatValue = .24f;
            actor.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SavePrefabAsset(prefab);
            // 향후 재배치용 prefab도 같은 가로 폭을 사용. 나머지 설정은 보존한다.
            var runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Audience/AudienceRuntime.prefab");
            if (runtimePrefab != null)
            {
                var p = new SerializedObject(runtimePrefab.GetComponent<AudienceRosterPresenter>());
                p.FindProperty("layoutHalfWidth").floatValue = 8.4f;
                p.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SavePrefabAsset(runtimePrefab);
            }
            EnsureScoreFeedback();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[Art1016] 기본 스탠딩2종, 방송국 Base+Floor, 관객 가로 범위 적용 완료. 표정/다른 무대/조명 수동 배치는 보존.");
        }
        static void EnsureScoreFeedback()
        {
            foreach (var score in UnityEngine.Object.FindObjectsByType<ScoreUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (score.GetComponent<ScoreLossPresentation>() == null) score.gameObject.AddComponent<ScoreLossPresentation>();
        }
        [MenuItem("Tools/Art/Configure Score Loss Feedback")]
        public static void ConfigureScoreFeedback()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.name != "Main" || scene.isDirty)
                throw new InvalidOperationException("저장된 Main Edit Mode가 필요합니다.");
            EnsureScoreFeedback();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        static Sprite ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("에셋 없음: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Sprite 임포트 실패: " + path);
        }
    }
}
