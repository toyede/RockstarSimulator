using System.Collections.Generic;
using ContextStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// Tools/Art/Apply 0918 Art
    ///   1. Sprites/0918_art 임포트 교정 (Sprite · Single · Point · 무압축 · PPU 100)
    ///   2. Resources/Dialogue/DialoguePortraitCatalog.asset — 화자·표정 → 초상화
    ///        너구리: 기본(0910) / happy·excited·relieved / angry / crying·sad
    ///        고슴도치: 기본(0910) / angry
    ///        LUX//FAUNA(rival): 3프레임 교대 (lux_fauna_ani_01~03)
    ///   3. Main 씬 RivalStagePlaceholder.duoFrames 에 같은 3프레임 (보스 라이벌 무대의 듀오)
    ///   카드 아트 v2(event_curtain_call_v2 · event_guitar_smash_v2)는 카드 프리팹이 이미 참조하고 있어 여기서 건드리지 않는다.
    /// </summary>
    public static class Art0918Setup
    {
        const string ArtRoot = "Assets/Sprites/0918_art";
        const string StandingRoot = "Assets/Sprites/0910_art/스탠딩일러";
        const string CatalogFolder = "Assets/Resources/Dialogue";
        const string CatalogPath = CatalogFolder + "/DialoguePortraitCatalog.asset";
        const string MainScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/Art/Apply 0918 Art", false, 30)]
        public static void Apply()
        {
            FixImports();
            DialoguePortraitCatalog catalog = BuildPortraitCatalog();
            AssignRivalDuo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Art0918] 적용 완료 — 초상화 항목 {catalog.Entries.Count}개, 라이벌 듀오 프레임 연결. " +
                      "표정 ID 는 DialogueSetupMenu 의 Line(..., \"happy\") 등으로 붙이고 Rewrite Default Sequences 를 실행한다.");
        }

        static void FixImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
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
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
                if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
        }

        static DialoguePortraitCatalog BuildPortraitCatalog()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(CatalogFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Dialogue");

            var catalog = AssetDatabase.LoadAssetAtPath<DialoguePortraitCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DialoguePortraitCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.Upsert("raccoon", null, new[] { Standing("raccoon_standing") }, 0.45f);
            catalog.Upsert("raccoon", new[] { "happy", "excited", "relieved" }, new[] { New("raccoon_standing_happy") }, 0.45f);
            catalog.Upsert("raccoon", new[] { "angry" }, new[] { New("raccoon_standing_angry") }, 0.45f);
            catalog.Upsert("raccoon", new[] { "crying", "sad" }, new[] { New("raccoon_standing_crying") }, 0.45f);
            catalog.Upsert("hedgehog", null, new[] { Standing("hedgehog_standing") }, 0.45f);
            catalog.Upsert("hedgehog", new[] { "angry" }, new[] { New("hedgehog_standing_angry") }, 0.45f);
            catalog.Upsert("rival", null, new[] { New("lux_fauna_ani_01"), New("lux_fauna_ani_02"), New("lux_fauna_ani_03") }, 0.45f);

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static void AssignRivalDuo()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            var placeholder = Object.FindFirstObjectByType<RivalStagePlaceholder>(FindObjectsInactive.Include);
            if (placeholder == null)
            {
                Debug.LogWarning("[Art0918] Main 씬에 RivalStagePlaceholder 가 없습니다. Tools/Tour/Setup Boss Battle 를 먼저 실행하세요.");
                return;
            }

            var so = new SerializedObject(placeholder);
            SerializedProperty frames = so.FindProperty("duoFrames");
            var sprites = new List<Sprite> { New("lux_fauna_ani_01"), New("lux_fauna_ani_02"), New("lux_fauna_ani_03") };
            frames.arraySize = sprites.Count;
            for (int i = 0; i < sprites.Count; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            SerializedProperty scale = so.FindProperty("duoSpriteScale");
            if (scale != null && scale.floatValue >= 1f) scale.floatValue = 0.65f; // 처음 연결할 때만 화면에 맞는 배율로 (손으로 고친 값은 유지)
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Sprite New(string file) => Load($"{ArtRoot}/{file}.png");
        static Sprite Standing(string file) => Load($"{StandingRoot}/{file}.png");

        static Sprite Load(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[Art0918] 스프라이트를 찾지 못했습니다: {path}");
            return sprite;
        }
    }
}
