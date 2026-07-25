using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ContextStage.EditorTools
{
    public static class AudienceFoundationSetup
    {
        internal const string SettingsFolder = "Assets/Settings/Audience";
        internal const string EngagementConfigPath =
            SettingsFolder + "/AudienceEngagementConfig.asset";
        internal const string FlowConfigPath =
            SettingsFolder + "/AudienceFlowConfig.asset";
        internal const string MemberPrefabPath =
            "Assets/Prefabs/Audience/AudienceMember.prefab";

        const string ChillSourcePath =
            "Assets/Prefabs/Audience/Prototype/CrowdMember_Chill_Placeholder.prefab";
        const string SingalongSourcePath =
            "Assets/Prefabs/Audience/Prototype/CrowdMember_Singalong_Placeholder.prefab";
        const string MoshSourcePath =
            "Assets/Prefabs/Audience/Prototype/CrowdMember_Mosh_Placeholder.prefab";

        [MenuItem("Tools/Audience/Setup Audience Foundation Assets")]
        public static void SetupAssets()
        {
            EditorSetupUtility.EnsureFolder(SettingsFolder);

            AudienceEngagementConfig engagement =
                LoadOrCreate<AudienceEngagementConfig>(EngagementConfigPath);
            AudienceFlowConfig flow =
                LoadOrCreate<AudienceFlowConfig>(FlowConfigPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (engagement == null || flow == null)
                throw new UnityException(
                    "[AudienceSetup] Failed to create required audience settings.");

            Debug.Log(
                $"[AudienceSetup] Foundation assets ready: " +
                $"{EngagementConfigPath}, {FlowConfigPath}");
        }

        [MenuItem("Tools/Audience/Create Or Update Audience Member Prefab")]
        public static void CreateOrUpdateMemberPrefab()
        {
            EditorSetupUtility.EnsureFolder("Assets/Prefabs/Audience");

            SpriteRenderer chillSource = LoadSourceRenderer(ChillSourcePath);
            SpriteRenderer singalongSource = LoadSourceRenderer(SingalongSourcePath);
            SpriteRenderer moshSource = LoadSourceRenderer(MoshSourcePath);
            Sprite barSprite =
                AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (barSprite == null)
                throw new UnityException("[AudienceSetup] Built-in UI sprite is unavailable.");

            bool loadedPrefabContents =
                AssetDatabase.LoadAssetAtPath<GameObject>(MemberPrefabPath) != null;
            GameObject root = loadedPrefabContents
                ? PrefabUtility.LoadPrefabContents(MemberPrefabPath)
                : new GameObject("AudienceMember");

            try
            {
                SpriteRenderer character = root.GetComponent<SpriteRenderer>();
                if (character == null)
                    character = root.AddComponent<SpriteRenderer>();
                AudienceMemberActor actor = root.GetComponent<AudienceMemberActor>();
                if (actor == null)
                    actor = root.AddComponent<AudienceMemberActor>();

                Transform warningRoot = FindOrCreateChild(root.transform, "WarningBar");
                warningRoot.localPosition = new Vector3(0f, 1.25f, 0f);
                warningRoot.localRotation = Quaternion.identity;
                warningRoot.localScale = new Vector3(1.15f, 0.12f, 1f);

                Transform background = FindOrCreateChild(warningRoot, "Background");
                background.localPosition = Vector3.zero;
                background.localRotation = Quaternion.identity;
                background.localScale = Vector3.one;
                SpriteRenderer backgroundRenderer =
                    background.GetComponent<SpriteRenderer>();
                if (backgroundRenderer == null)
                    backgroundRenderer =
                        background.gameObject.AddComponent<SpriteRenderer>();
                backgroundRenderer.sprite = barSprite;
                backgroundRenderer.color = new Color(0.06f, 0.07f, 0.10f, 0.9f);

                Transform fill = FindOrCreateChild(warningRoot, "Fill");
                fill.localPosition = Vector3.zero;
                fill.localRotation = Quaternion.identity;
                fill.localScale = Vector3.one;
                SpriteRenderer fillRenderer = fill.GetComponent<SpriteRenderer>();
                if (fillRenderer == null)
                    fillRenderer = fill.gameObject.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = barSprite;
                fillRenderer.color = new Color(0.95f, 0.19f, 0.16f, 1f);

                var serialized = new SerializedObject(actor);
                SetReference(serialized, "characterRenderer", character);
                SetReference(serialized, "chillSprite", chillSource.sprite);
                SetReference(serialized, "singalongSprite", singalongSource.sprite);
                SetReference(serialized, "moshSprite", moshSource.sprite);
                SetColor(serialized, "chillColor", chillSource.color);
                SetColor(serialized, "singalongColor", singalongSource.color);
                SetColor(serialized, "moshColor", moshSource.color);
                SetReference(serialized, "warningRoot", warningRoot.gameObject);
                SetReference(serialized, "warningFill", fill);
                SetReference(
                    serialized,
                    "warningBackgroundRenderer",
                    backgroundRenderer);
                SetReference(serialized, "warningFillRenderer", fillRenderer);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                character.sprite = moshSource.sprite;
                character.color = moshSource.color;
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                PrefabUtility.SaveAsPrefabAsset(root, MemberPrefabPath);
            }
            finally
            {
                if (loadedPrefabContents)
                    PrefabUtility.UnloadPrefabContents(root);
                else
                    UnityEngine.Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AudienceSetup] Audience member prefab ready: {MemberPrefabPath}");
        }

        [MenuItem("Tools/Audience/Configure Audience Card Profiles")]
        public static void ConfigureCardProfiles()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/Card_Prefab" });
            int configured = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CardDefinition card = root.GetComponent<CardDefinition>();
                    if (card == null) continue;

                    var serialized = new SerializedObject(card);
                    if (card.Role == CardRole.Special)
                    {
                        RequireProperty(serialized, "role").enumValueIndex =
                            (int)CardRole.Normal;
                    }
                    SerializedProperty profile =
                        RequireProperty(serialized, "audienceReaction");
                    bool applies = card.Role != CardRole.Utility;
                    bool isTemplate = string.Equals(
                        path,
                        "Assets/Card_Prefab/Card_Base.prefab",
                        StringComparison.OrdinalIgnoreCase);
                    SetBool(profile, "appliesToAudience", applies);
                    SetInt(
                        profile,
                        "chillScore",
                        applies
                            ? !isTemplate &&
                              card.TargetPreference == CrowdPreference.Chill ? 3 : 1
                            : 0);
                    SetInt(
                        profile,
                        "singalongScore",
                        applies
                            ? !isTemplate &&
                              card.TargetPreference == CrowdPreference.Singalong ? 3 : 1
                            : 0);
                    SetInt(
                        profile,
                        "moshScore",
                        applies
                            ? !isTemplate &&
                              card.TargetPreference == CrowdPreference.Mosh ? 3 : 1
                            : 0);
                    SetInt(profile, "calmScore", applies ? 1 : 0);
                    SetInt(profile, "middleScore", applies ? 2 : 0);
                    SetInt(profile, "excitedScore", applies ? 3 : 0);
                    SetFloat(profile, "engagementMultiplier", applies ? 1f : 0f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    configured++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AudienceSetup] Configured {configured} card audience profiles.");
        }

        [MenuItem("Tools/Audience/Configure Main Scene For Individual Audience")]
        public static void ConfigureMainScene()
        {
            GameObject root = GameObject.Find("[AudienceSystem]");
            if (root == null)
                throw new UnityException(
                    "[AudienceSetup] The staged [AudienceSystem] object is missing.");

            AudienceRosterSystem system = root.GetComponent<AudienceRosterSystem>();
            if (system == null)
                throw new UnityException(
                    "[AudienceSetup] AudienceRosterSystem is missing.");
            AudienceRosterPresenter presenter =
                root.GetComponent<AudienceRosterPresenter>();
            if (presenter == null)
                presenter = root.AddComponent<AudienceRosterPresenter>();
            AudienceDebugInput debugInput = root.GetComponent<AudienceDebugInput>();
            if (debugInput == null)
                debugInput = root.AddComponent<AudienceDebugInput>();

            AudienceEngagementConfig engagement =
                AssetDatabase.LoadAssetAtPath<AudienceEngagementConfig>(
                    EngagementConfigPath);
            AudienceFlowConfig flow =
                AssetDatabase.LoadAssetAtPath<AudienceFlowConfig>(FlowConfigPath);
            if (engagement == null || flow == null)
                throw new UnityException(
                    "[AudienceSetup] Run Setup Audience Foundation Assets first.");
            AudienceMemberActor memberPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(MemberPrefabPath)
                    ?.GetComponent<AudienceMemberActor>();
            if (memberPrefab == null)
                throw new UnityException(
                    "[AudienceSetup] Run Create Or Update Audience Member Prefab first.");

            root.transform.position = new Vector3(0f, 2f, 0f);
            bool engagementAssigned = EditorSetupUtility.SetObjectField(
                system,
                "engagementConfig",
                engagement);
            bool flowAssigned = EditorSetupUtility.SetObjectField(
                system,
                "flowConfig",
                flow);
            bool prefabAssigned = EditorSetupUtility.SetObjectField(
                presenter,
                "memberPrefab",
                memberPrefab);
            bool rootAssigned = EditorSetupUtility.SetObjectField(
                presenter,
                "memberRoot",
                root.transform);
            CardSystem cards = UnityEngine.Object.FindFirstObjectByType<CardSystem>(
                FindObjectsInactive.Include);
            bool rosterAssigned =
                cards != null &&
                EditorSetupUtility.SetObjectField(cards, "audienceRoster", system);
            if (!engagementAssigned ||
                !flowAssigned ||
                !prefabAssigned ||
                !rootAssigned ||
                !rosterAssigned)
                throw new UnityException(
                    "[AudienceSetup] Failed to configure the audience runtime.");

            system.enabled = true;
            presenter.enabled = true;
            debugInput.enabled = true;
            DisableLegacySceneSystems();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log(
                "[AudienceSetup] Main now uses the individual audience runtime.",
                system);
        }

        public static void SetupCompleteIndividualAudience()
        {
            SetupAssets();
            CreateOrUpdateMemberPrefab();
            ConfigureCardProfiles();
            ConfigureMainScene();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static SpriteRenderer LoadSourceRenderer(string path)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            SpriteRenderer renderer =
                source != null ? source.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null)
                throw new UnityException(
                    $"[AudienceSetup] Source audience sprite is missing: {path}");
            return renderer;
        }

        static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void DisableLegacySceneSystems()
        {
            SetEnabled<CrowdSpawner>(false);
            SetEnabled<CrowdMoodDirector>(false);
            SetEnabled<CrowdCompositionManager>(false);
            SetEnabled<CrowdCompositionDebugView>(false);
            SetEnabled<CrowdShiftDirector>(false);
            SetEnabled<HypeSystem>(false);
            SetEnabled<HypeDebugInput>(false);
            SetEnabled<SpecialAudienceManager>(false);
            SetEnabled<SpecialAudienceDropTarget>(false);
            SetEnabled<StageLightEventBridge>(false);
            SetEnabled<CrowdAmbienceSystem>(false);
            SetEnabled<CrowdAmbienceDebugInput>(false);

            GameObject hypeGauge = GameObject.Find("HypeCanvas/HypeGauge");
            if (hypeGauge != null) hypeGauge.SetActive(false);
            GameObject specialAudience = GameObject.Find("[Crowd]/SpecialAudience");
            if (specialAudience != null) specialAudience.SetActive(false);
            GameObject specialCanvas = GameObject.Find("SpecialAudienceCanvas");
            if (specialCanvas != null) specialCanvas.SetActive(false);
        }

        static void SetEnabled<T>(bool value) where T : Behaviour
        {
            T[] components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
                components[i].enabled = value;
        }

        static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string fieldName)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property == null)
                throw new InvalidOperationException(
                    $"Missing serialized field '{fieldName}' on " +
                    $"{serialized.targetObject.GetType().Name}.");
            return property;
        }

        static SerializedProperty RequireRelative(
            SerializedProperty parent,
            string fieldName)
        {
            SerializedProperty property = parent.FindPropertyRelative(fieldName);
            if (property == null)
                throw new InvalidOperationException(
                    $"Missing serialized field '{parent.propertyPath}.{fieldName}'.");
            return property;
        }

        static void SetReference(
            SerializedObject serialized,
            string fieldName,
            UnityEngine.Object value) =>
            RequireProperty(serialized, fieldName).objectReferenceValue = value;

        static void SetColor(
            SerializedObject serialized,
            string fieldName,
            Color value) =>
            RequireProperty(serialized, fieldName).colorValue = value;

        static void SetBool(
            SerializedProperty parent,
            string fieldName,
            bool value) =>
            RequireRelative(parent, fieldName).boolValue = value;

        static void SetInt(
            SerializedProperty parent,
            string fieldName,
            int value) =>
            RequireRelative(parent, fieldName).intValue = value;

        static void SetFloat(
            SerializedProperty parent,
            string fieldName,
            float value) =>
            RequireRelative(parent, fieldName).floatValue = value;
    }
}
