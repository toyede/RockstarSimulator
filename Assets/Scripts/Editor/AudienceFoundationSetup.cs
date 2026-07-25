using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
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

                RectTransform reactionRoot =
                    FindOrCreateRectChild(
                        root.transform,
                        "ReactionPopup",
                        out bool createdReactionRoot);
                if (createdReactionRoot)
                {
                    reactionRoot.localPosition = new Vector3(0f, 1.75f, 0f);
                    reactionRoot.localRotation = Quaternion.identity;
                    reactionRoot.localScale = Vector3.one;
                    reactionRoot.sizeDelta = new Vector2(3f, 1f);
                }

                TextMeshPro reactionText =
                    reactionRoot.GetComponent<TextMeshPro>();
                bool createdReactionText = reactionText == null;
                if (reactionText == null)
                    reactionText = reactionRoot.gameObject.AddComponent<TextMeshPro>();
                AudienceReactionPopup reactionPopup =
                    reactionRoot.GetComponent<AudienceReactionPopup>();
                if (reactionPopup == null)
                    reactionPopup =
                        reactionRoot.gameObject.AddComponent<AudienceReactionPopup>();

                if (createdReactionText)
                {
                    reactionText.font = TMP_Settings.defaultFontAsset;
                    reactionText.fontSize = 4.5f;
                    reactionText.fontStyle = FontStyles.Bold;
                    reactionText.alignment = TextAlignmentOptions.Center;
                    reactionText.color = new Color(1f, 0.92f, 0.28f, 1f);
                    reactionText.margin = Vector4.zero;
                    reactionText.richText = false;
                }
                if (reactionText.font == null)
                    throw new UnityException(
                        "[AudienceSetup] TMP default font is not configured.");

                reactionText.text = string.Empty;
                reactionText.enabled = false;

                var reactionSerialized = new SerializedObject(reactionPopup);
                SetReference(reactionSerialized, "valueText", reactionText);
                reactionSerialized.ApplyModifiedPropertiesWithoutUndo();

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
                SetReference(serialized, "reactionPopup", reactionPopup);
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

        [MenuItem("Tools/Audience/Validate Audience Card Prefab Values")]
        public static void ConfigureCardProfiles()
        {
            var failures = new List<string>();
            CollectCardProfileFailures(failures);
            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "Audience card prefab validation failed:\n- " +
                    string.Join("\n- ", failures));

            Debug.Log(
                "[AudienceSetup] Card prefab audience values are valid. " +
                "No prefab values were changed.");
        }

        [MenuItem("Tools/Audience/Prepare Card Prefabs For Manual Values")]
        public static void PrepareCardPrefabsForManualValues()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/Card_Prefab" });
            var captured = new Dictionary<string, CardProfileValues>(
                StringComparer.OrdinalIgnoreCase);
            string basePath = "Assets/Card_Prefab/Card_Base.prefab";

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CardDefinition card = root.GetComponent<CardDefinition>();
                    if (card == null) continue;
                    captured[path] = CardProfileValues.From(card.AudienceReaction);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (!captured.ContainsKey(basePath))
                throw new UnityException(
                    $"[AudienceSetup] Card base prefab is missing: {basePath}");

            WriteCardProfile(basePath, CardProfileValues.NoReaction);
            foreach (KeyValuePair<string, CardProfileValues> pair in captured)
            {
                if (string.Equals(
                        pair.Key,
                        basePath,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                WriteCardProfile(pair.Key, pair.Value);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ConfigureCardProfiles();
            Debug.Log(
                $"[AudienceSetup] Prepared {captured.Count - 1} card prefabs for " +
                "manual audience-value authoring. Existing effective values were preserved.");
        }

        [Obsolete("Use AudienceRuntimeSetup.InstallInActiveScene instead.")]
        public static void ConfigureMainScene()
        {
            AudienceRuntimeSetup.InstallInActiveScene();
        }

        public static void SetupCompleteIndividualAudience()
        {
            SetupAssets();
            CreateOrUpdateMemberPrefab();
            AudienceRuntimeSetup.CreateOrUpdateRuntimePrefab();
            ConfigureCardProfiles();
            AssetDatabase.SaveAssets();
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

        static RectTransform FindOrCreateRectChild(
            Transform parent,
            string name,
            out bool created)
        {
            Transform child = parent.Find(name);
            if (child != null)
            {
                created = false;
                if (child is RectTransform rectTransform) return rectTransform;
                throw new UnityException(
                    $"[AudienceSetup] '{name}' must use RectTransform.");
            }

            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            created = true;
            return rect;
        }

        internal static void CollectCardProfileFailures(List<string> failures)
        {
            if (failures == null) throw new ArgumentNullException(nameof(failures));

            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/Card_Prefab" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                CardDefinition card =
                    prefab != null ? prefab.GetComponent<CardDefinition>() : null;
                if (card == null) continue;

                AudienceReactionProfile profile = card.AudienceReaction;
                if (profile == null)
                {
                    failures.Add($"[{path}] Audience reaction values are missing.");
                    continue;
                }

                if (!profile.TryValidate(out string error))
                {
                    failures.Add($"[{path}] {error}");
                    continue;
                }

                bool isTemplate = string.Equals(
                    path,
                    "Assets/Card_Prefab/Card_Base.prefab",
                    StringComparison.OrdinalIgnoreCase);
                bool shouldReact = !isTemplate && card.Role != CardRole.Utility;
                if (profile.AppliesToAudience != shouldReact)
                {
                    failures.Add(
                        $"[{path}] AppliesToAudience must be {shouldReact} for " +
                        $"{(isTemplate ? "the base template" : card.Role.ToString())}.");
                }

                if (!shouldReact && !CardProfileValues.From(profile).IsNoReaction)
                    failures.Add(
                        $"[{path}] Non-performance cards must keep every audience " +
                        "value and multiplier at zero.");

                if (card.Role == CardRole.Special)
                {
                    SpecialCardTargetEffect targetEffect =
                        card.SpecialTargetEffect;
                    string targetError = targetEffect == null
                        ? "Data is missing."
                        : string.Empty;
                    if (targetEffect == null ||
                        !targetEffect.TryValidate(out targetError))
                    {
                        failures.Add(
                            $"[{path}] Invalid targeted special-card effect: " +
                            targetError);
                    }
                }
            }
        }

        static void WriteCardProfile(string path, CardProfileValues values)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CardDefinition card = root.GetComponent<CardDefinition>();
                if (card == null)
                    throw new UnityException(
                        $"[AudienceSetup] CardDefinition is missing: {path}");

                var serialized = new SerializedObject(card);
                SerializedProperty profile =
                    RequireProperty(serialized, "audienceReaction");
                SetBool(profile, "appliesToAudience", values.AppliesToAudience);
                SetInt(profile, "chillScore", values.ChillScore);
                SetInt(profile, "singalongScore", values.SingalongScore);
                SetInt(profile, "moshScore", values.MoshScore);
                SetInt(profile, "calmScore", values.CalmScore);
                SetInt(profile, "middleScore", values.MiddleScore);
                SetInt(profile, "excitedScore", values.ExcitedScore);
                SetFloat(
                    profile,
                    "engagementMultiplier",
                    values.EngagementMultiplier);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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

        readonly struct CardProfileValues
        {
            public CardProfileValues(
                bool appliesToAudience,
                int chillScore,
                int singalongScore,
                int moshScore,
                int calmScore,
                int middleScore,
                int excitedScore,
                float engagementMultiplier)
            {
                AppliesToAudience = appliesToAudience;
                ChillScore = chillScore;
                SingalongScore = singalongScore;
                MoshScore = moshScore;
                CalmScore = calmScore;
                MiddleScore = middleScore;
                ExcitedScore = excitedScore;
                EngagementMultiplier = engagementMultiplier;
            }

            public static CardProfileValues NoReaction =>
                new CardProfileValues(false, 0, 0, 0, 0, 0, 0, 0f);

            public bool AppliesToAudience { get; }
            public int ChillScore { get; }
            public int SingalongScore { get; }
            public int MoshScore { get; }
            public int CalmScore { get; }
            public int MiddleScore { get; }
            public int ExcitedScore { get; }
            public float EngagementMultiplier { get; }

            public bool IsNoReaction =>
                !AppliesToAudience &&
                ChillScore == 0 &&
                SingalongScore == 0 &&
                MoshScore == 0 &&
                CalmScore == 0 &&
                MiddleScore == 0 &&
                ExcitedScore == 0 &&
                Mathf.Approximately(EngagementMultiplier, 0f);

            public static CardProfileValues From(AudienceReactionProfile profile)
            {
                if (profile == null)
                    throw new ArgumentNullException(nameof(profile));

                return new CardProfileValues(
                    profile.AppliesToAudience,
                    profile.ChillScore,
                    profile.SingalongScore,
                    profile.MoshScore,
                    profile.CalmScore,
                    profile.MiddleScore,
                    profile.ExcitedScore,
                    profile.EngagementMultiplier);
            }
        }
    }
}
