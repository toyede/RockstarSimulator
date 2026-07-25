using System.IO;
using ContextStage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    public static class CrowdCompositionSetupMenu
    {
        const string PrototypeFolder = "Assets/Prefabs/Audience/Prototype";
        const string GeneratedFolder = "Assets/Sprites/Crowd/Prototype";
        const string ConfigPath = "Assets/Settings/CrowdCompositionConfig.asset";

        const string ChillSpritePath = GeneratedFolder + "/placeholder_chill_circle.png";
        const string SingalongSpritePath = GeneratedFolder + "/placeholder_singalong_square.png";
        const string MoshSpritePath = GeneratedFolder + "/placeholder_mosh_diamond.png";

        const string ChillPrefabPath = PrototypeFolder + "/CrowdMember_Chill_Placeholder.prefab";
        const string SingalongPrefabPath = PrototypeFolder + "/CrowdMember_Singalong_Placeholder.prefab";
        const string MoshPrefabPath = PrototypeFolder + "/CrowdMember_Mosh_Placeholder.prefab";
        const string MoshSpriteSheetPath = "Assets/Sprites/Crowd/crowd_low.png";

        enum PlaceholderShape
        {
            Circle,
            Square,
            Diamond
        }

        [MenuItem("Tools/Crowd/Setup Crowd Composition Prototype", false, 10)]
        public static void Setup()
        {
            EnsureFolder(PrototypeFolder);
            EnsureFolder(GeneratedFolder);
            CrowdCompositionConfig config = GetOrCreateConfig();

            Sprite chillSprite = GetOrCreateSprite(ChillSpritePath, PlaceholderShape.Circle);
            Sprite singalongSprite = GetOrCreateSprite(SingalongSpritePath, PlaceholderShape.Square);
            Sprite moshSprite = LoadFirstSprite(MoshSpriteSheetPath);
            if (moshSprite == null)
                moshSprite = GetOrCreateSprite(MoshSpritePath, PlaceholderShape.Diamond);

            GameObject chillPrefab = GetOrCreatePrefab(
                ChillPrefabPath,
                CrowdPreference.Chill,
                chillSprite,
                new Color32(49, 223, 234, 255),
                "CHILL");

            GameObject singalongPrefab = GetOrCreatePrefab(
                SingalongPrefabPath,
                CrowdPreference.Singalong,
                singalongSprite,
                new Color32(100, 49, 234, 255),
                "SING");

            GameObject moshPrefab = GetOrCreatePrefab(
                MoshPrefabPath,
                CrowdPreference.Mosh,
                moshSprite,
                Color.white,
                "MOSH");
            ConfigureMoshPrefab(moshSprite);

            CrowdSpawner spawner = Object.FindFirstObjectByType<CrowdSpawner>();
            GameObject host = spawner != null ? spawner.gameObject : GameObject.Find("[Crowd]");

            if (host == null)
            {
                host = new GameObject("[Crowd]");
                Undo.RegisterCreatedObjectUndo(host, "Create Crowd");
                spawner = Undo.AddComponent<CrowdSpawner>(host);
                Debug.LogWarning(
                    "[CrowdComposition] No CrowdSpawner existed. A basic [Crowd] host was created; " +
                    "run Tools/Crowd/Setup Crowd Scene as well if the mood system is missing.");
            }
            else if (spawner == null)
            {
                spawner = Undo.AddComponent<CrowdSpawner>(host);
            }

            CrowdCompositionManager manager =
                host.GetComponent<CrowdCompositionManager>() ??
                Undo.AddComponent<CrowdCompositionManager>(host);

            if (host.GetComponent<CrowdCompositionDebugView>() == null)
                Undo.AddComponent<CrowdCompositionDebugView>(host);
            CrowdShiftDirector shiftDirector =
                host.GetComponent<CrowdShiftDirector>() ??
                Undo.AddComponent<CrowdShiftDirector>(host);

            var managerObject = new SerializedObject(manager);
            SetObjectReference(managerObject, "config", config);
            managerObject.ApplyModifiedPropertiesWithoutUndo();

            var shiftObject = new SerializedObject(shiftDirector);
            SetObjectReference(shiftObject, "manager", manager);
            shiftObject.ApplyModifiedPropertiesWithoutUndo();

            var spawnerObject = new SerializedObject(spawner);
            SetObjectReference(spawnerObject, "compositionManager", manager);
            SetObjectReference(spawnerObject, "chillPrefab", chillPrefab.GetComponent<CrowdMemberView>());
            SetObjectReference(spawnerObject, "singalongPrefab", singalongPrefab.GetComponent<CrowdMemberView>());
            SetObjectReference(spawnerObject, "moshPrefab", moshPrefab.GetComponent<CrowdMemberView>());
            spawnerObject.ApplyModifiedPropertiesWithoutUndo();

            CardSystem cardSystem = Object.FindFirstObjectByType<CardSystem>();
            if (cardSystem != null)
            {
                var cardObject = new SerializedObject(cardSystem);
                SetObjectReference(cardObject, "crowdComposition", manager);
                cardObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(cardSystem);
            }

            SpecialAudienceCrowdActor[] specialActors =
                Object.FindObjectsByType<SpecialAudienceCrowdActor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < specialActors.Length; i++)
            {
                SpecialAudienceCrowdActor specialActor = specialActors[i];
                var actorObject = new SerializedObject(specialActor);
                SetObjectReference(actorObject, "crowdSpawner", spawner);
                actorObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(specialActor);
            }

            EnsureSpecialAudienceDropTarget(specialActors);

            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(shiftDirector);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = host;
            Debug.Log(
                "[CrowdComposition] Prototype setup complete. " +
                "F1 Balanced / F2 Formal / F3 Britpop / F4 Hardcore. " +
                "Replace each placeholder prefab's root SpriteRenderer sprite and remove its Label child " +
                "when final audience art arrives.");
        }

        static CrowdCompositionConfig GetOrCreateConfig()
        {
            CrowdCompositionConfig config =
                AssetDatabase.LoadAssetAtPath<CrowdCompositionConfig>(ConfigPath);
            if (config != null) return config;

            EnsureFolder(Path.GetDirectoryName(ConfigPath)?.Replace('\\', '/'));
            config = ScriptableObject.CreateInstance<CrowdCompositionConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        static void EnsureSpecialAudienceDropTarget(SpecialAudienceCrowdActor[] actors)
        {
            SpecialAudienceDropTarget existing =
                Object.FindFirstObjectByType<SpecialAudienceDropTarget>(FindObjectsInactive.Include);
            if (existing != null || actors == null || actors.Length == 0) return;

            SpecialAudienceCrowdActor actor = null;
            for (int i = 0; i < actors.Length; i++)
            {
                if (actors[i] != null && actors[i].gameObject.activeInHierarchy)
                {
                    actor = actors[i];
                    break;
                }
            }
            if (actor == null) return;

            var targetObject = new GameObject("SpecialAudienceHitArea");
            Undo.RegisterCreatedObjectUndo(targetObject, "Create Special Audience Hit Area");
            targetObject.transform.SetParent(actor.transform.parent, false);
            targetObject.transform.position = actor.transform.position;

            var collider = Undo.AddComponent<BoxCollider2D>(targetObject);
            collider.isTrigger = true;
            collider.size = new Vector2(2.6f, 3.4f);
            collider.offset = new Vector2(0f, 0.4f);

            var target = Undo.AddComponent<SpecialAudienceDropTarget>(targetObject);
            var targetSerialized = new SerializedObject(target);
            SetObjectReference(targetSerialized, "hitCollider", collider);
            SetObjectReference(targetSerialized, "followTarget", actor.transform);
            SetObjectReference(targetSerialized, "hoverScaleTarget", actor.transform);
            targetSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static GameObject GetOrCreatePrefab(
            string path,
            CrowdPreference preference,
            Sprite sprite,
            Color color,
            string label)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject($"CrowdMember_{preference}_Placeholder");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;

            var memberView = root.AddComponent<CrowdMemberView>();
            var memberObject = new SerializedObject(memberView);
            memberObject.FindProperty("preference").enumValueIndex = (int)preference;
            memberObject.FindProperty("useMoodSprites").boolValue = false;
            memberObject.ApplyModifiedPropertiesWithoutUndo();

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);

            var text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 32;
            text.characterSize = 0.09f;
            text.color = Color.white;

            var textRenderer = labelObject.GetComponent<MeshRenderer>();
            textRenderer.sortingLayerName = renderer.sortingLayerName;
            textRenderer.sortingOrder = renderer.sortingOrder + 10;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Sprite LoadFirstSprite(string path)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite && sprite.rect.width > 16f && sprite.rect.height > 16f)
                    return sprite;
            }

            return null;
        }

        static void ConfigureMoshPrefab(Sprite previewSprite)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MoshPrefabPath);
            try
            {
                var renderer = root.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    renderer.sprite = previewSprite;
                    renderer.color = Color.white;
                }

                var memberView = root.GetComponent<CrowdMemberView>();
                if (memberView != null)
                {
                    var serialized = new SerializedObject(memberView);
                    serialized.FindProperty("preference").enumValueIndex =
                        (int)CrowdPreference.Mosh;
                    serialized.FindProperty("useMoodSprites").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                Transform label = root.transform.Find("Label");
                if (label != null)
                    Object.DestroyImmediate(label.gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, MoshPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Sprite GetOrCreateSprite(string path, PlaceholderShape shape)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);
                    bool inside;

                    switch (shape)
                    {
                        case PlaceholderShape.Circle:
                            inside = dx * dx + dy * dy <= radius * radius;
                            break;
                        case PlaceholderShape.Diamond:
                            inside = dx + dy <= radius;
                            break;
                        default:
                            inside = dx <= radius && dy <= radius;
                            break;
                    }

                    pixels[y * size + x] = inside ? white : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

    }
}
