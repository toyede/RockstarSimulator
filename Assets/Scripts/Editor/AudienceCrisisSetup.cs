using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage.EditorTools
{
    public static class AudienceCrisisSetup
    {
        const string ConfigPath =
            "Assets/Settings/Audience/AudienceCrisisConfig.asset";
        const string MemberPrefabPath =
            "Assets/Prefabs/Audience/AudienceMember.prefab";

        [MenuItem("Tools/Audience/Setup Nearby Concert Crisis")]
        public static void Setup()
        {
            AudienceCrisisConfig config = GetOrCreateConfig();
            AudienceRosterSystem roster =
                Object.FindFirstObjectByType<AudienceRosterSystem>(
                    FindObjectsInactive.Include);
            if (roster == null)
            {
                throw new UnityException(
                    "[AudienceCrisisSetup] AudienceRosterSystem is missing.");
            }

            GameObject systemRoot = roster.gameObject;
            NearbyConcertCrisisDirector director =
                systemRoot.GetComponent<NearbyConcertCrisisDirector>();
            if (director == null)
            {
                director =
                    Undo.AddComponent<NearbyConcertCrisisDirector>(
                        systemRoot);
            }

            bool configAssigned =
                EditorSetupUtility.SetObjectField(
                    director,
                    "config",
                    config);
            bool rosterAssigned =
                EditorSetupUtility.SetObjectField(
                    director,
                    "audienceRoster",
                    roster);
            if (!configAssigned || !rosterAssigned)
            {
                throw new UnityException(
                    "[AudienceCrisisSetup] Failed to configure director.");
            }

            AudienceCrisisDebugInput debugInput =
                systemRoot.GetComponent<AudienceCrisisDebugInput>();
            if (debugInput == null)
            {
                debugInput =
                    Undo.AddComponent<AudienceCrisisDebugInput>(
                        systemRoot);
            }
            if (!EditorSetupUtility.SetObjectField(
                    debugInput,
                    "director",
                    director))
            {
                throw new UnityException(
                    "[AudienceCrisisSetup] Failed to configure debug input.");
            }

            UpdateAudienceMemberPrefab();
            BuildWarningCanvas();

            EditorSceneManager.MarkSceneDirty(systemRoot.scene);
            Selection.activeGameObject = systemRoot;
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[AudienceCrisisSetup] Nearby Concert Crisis is ready. " +
                "Save Main scene after checking the overlay.",
                director);
        }

        [MenuItem("Tools/Audience/Debug/Force Nearby Concert Crisis")]
        public static void ForceCrisis()
        {
            if (GameManager.HasInstance &&
                GameManager.Instance.State == GameState.Ready)
            {
                GameManager.Instance.StartGame();
            }

            NearbyConcertCrisisDirector director =
                Object.FindFirstObjectByType<NearbyConcertCrisisDirector>(
                    FindObjectsInactive.Include);
            if (director == null)
            {
                Debug.LogError(
                    "[AudienceCrisisSetup] Crisis director is missing.");
                return;
            }

            director.ForceStartCrisis();
        }

        [MenuItem(
            "Tools/Audience/Debug/Force Nearby Concert Crisis",
            true)]
        static bool CanForceCrisis() => Application.isPlaying;

        static AudienceCrisisConfig GetOrCreateConfig()
        {
            EditorSetupUtility.EnsureFolder(
                "Assets/Settings/Audience");
            AudienceCrisisConfig existing =
                AssetDatabase.LoadAssetAtPath<AudienceCrisisConfig>(
                    ConfigPath);
            if (existing != null) return existing;

            AudienceCrisisConfig asset =
                ScriptableObject.CreateInstance<AudienceCrisisConfig>();
            AssetDatabase.CreateAsset(asset, ConfigPath);
            return asset;
        }

        static void UpdateAudienceMemberPrefab()
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(MemberPrefabPath);
            if (root == null)
            {
                throw new UnityException(
                    "[AudienceCrisisSetup] AudienceMember prefab is missing.");
            }

            try
            {
                AudienceMemberActor actor =
                    root.GetComponent<AudienceMemberActor>();
                if (actor == null)
                {
                    throw new UnityException(
                        "[AudienceCrisisSetup] AudienceMemberActor is missing.");
                }

                Transform warning =
                    FindOrCreateWorldChild(root.transform, "CrisisWarning");
                warning.localPosition = new Vector3(0f, 1.6f, 0f);
                warning.localRotation = Quaternion.identity;
                warning.localScale = Vector3.one;

                TextMesh text = warning.GetComponent<TextMesh>();
                if (text == null)
                    text = warning.gameObject.AddComponent<TextMesh>();
                text.text = "!";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 80;
                text.characterSize = 0.12f;
                text.color = new Color(1f, 0.17f, 0.08f, 1f);

                MeshRenderer renderer =
                    warning.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sortingOrder = 100;

                warning.gameObject.SetActive(false);
                EditorSetupUtility.SetObjectField(
                    actor,
                    "crisisWarningRoot",
                    warning.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, MemberPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void BuildWarningCanvas()
        {
            GameObject canvasObject = GameObject.Find("CrisisCanvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject(
                    "CrisisCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                Undo.RegisterCreatedObjectUndo(
                    canvasObject,
                    "Create Crisis Canvas");
            }

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;

            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            AudienceCrisisWarningUI view =
                canvasObject.GetComponent<AudienceCrisisWarningUI>();
            if (view == null)
            {
                view = Undo.AddComponent<AudienceCrisisWarningUI>(
                    canvasObject);
            }

            Transform overlay =
                FindOrCreateUiChild(canvasObject.transform, "Overlay");
            RectTransform overlayRect = EnsureRect(overlay.gameObject);
            Stretch(overlayRect);

            Image dim = EnsureComponent<Image>(overlay.gameObject);
            dim.color = new Color(0f, 0f, 0f, 0.1f);
            dim.raycastTarget = false;

            CanvasGroup group =
                EnsureComponent<CanvasGroup>(overlay.gameObject);
            group.interactable = false;
            group.blocksRaycasts = false;

            Text title = CreateText(
                overlay,
                "Title",
                "긴급 상황!",
                72,
                new Color(1f, 0.12f, 0.08f),
                new Vector2(0f, 150f),
                new Vector2(1500f, 110f));
            Text description = CreateText(
                overlay,
                "Description",
                "옆동네 인기 밴드의 공연이 곧 시작됩니다",
                38,
                Color.white,
                new Vector2(0f, 50f),
                new Vector2(1500f, 120f));
            Text countdown = CreateText(
                overlay,
                "Countdown",
                "관객 이탈까지 6.0초",
                100,
                new Color(1f, 0.78f, 0.12f),
                new Vector2(0f, -80f),
                new Vector2(600f, 130f));
            Text progress = CreateText(
                overlay,
                "Progress",
                "붙잡은 관객 0 / 3",
                34,
                Color.white,
                new Vector2(0f, -190f),
                new Vector2(1100f, 70f));

            EditorSetupUtility.SetObjectField(
                view,
                "overlayRoot",
                overlay.gameObject);
            EditorSetupUtility.SetObjectField(view, "titleText", title);
            EditorSetupUtility.SetObjectField(
                view,
                "descriptionText",
                description);
            EditorSetupUtility.SetObjectField(
                view,
                "countdownText",
                countdown);
            EditorSetupUtility.SetObjectField(
                view,
                "progressText",
                progress);

            overlay.gameObject.SetActive(false);
        }

        static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            Vector2 position,
            Vector2 size)
        {
            Transform child = FindOrCreateUiChild(parent, name);
            RectTransform rect = EnsureRect(child.gameObject);
            rect.anchorMin = rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = EnsureComponent<Text>(child.gameObject);
            text.text = content;
            text.font = AssetDatabase.LoadAssetAtPath<Font>(
                "Assets/Font/DungGeunMo.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static Transform FindOrCreateUiChild(
            Transform parent,
            string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;

            GameObject gameObject =
                new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(
                gameObject,
                $"Create {name}");
            gameObject.transform.SetParent(parent, false);
            gameObject.layer = LayerMask.NameToLayer("UI");
            return gameObject.transform;
        }

        static Transform FindOrCreateWorldChild(
            Transform parent,
            string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child;

            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        static RectTransform EnsureRect(GameObject gameObject)
        {
            RectTransform rect =
                gameObject.GetComponent<RectTransform>();
            if (rect != null) return rect;
            return Undo.AddComponent<RectTransform>(gameObject);
        }

        static T EnsureComponent<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : Undo.AddComponent<T>(gameObject);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
