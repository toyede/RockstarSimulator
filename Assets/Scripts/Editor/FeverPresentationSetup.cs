using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 피버타임 연출 셋업.
    ///
    /// <c>Tools/Feedback/Setup Fever Presentation</c> 한 번이면:
    /// <list type="number">
    /// <item>HUD 캔버스에 fevertime 배너 Image 를 만들고</item>
    /// <item>진입 사운드(fevertime_intro)를 연결하고</item>
    /// <item>Friends 애니메이터를 가속 대상으로 등록하고</item>
    /// <item>StageLighting 아래에 노란 픽셀 스포트라이트를 만들어 화면 상단에 세운다</item>
    /// </list>
    ///
    /// 이미 있는 것은 덮어쓰지 않는다. 여러 번 실행해도 안전하다.
    /// </summary>
    public static class FeverPresentationSetup
    {
        const string PresentationRootName = "[FeverPresentation]";
        const string BannerName = "FeverBanner";
        const string SpotlightName = "Fever Spotlight";
        const string BannerSpritePath = "Assets/Sprites/UI/fevertime.png";
        const string IntroClipPath = "Assets/Audio/OneShot/fevertime_intro.wav";

        [MenuItem("Tools/Feedback/Setup Fever Presentation", false, 23)]
        public static void Setup()
        {
            SetupPresentation();
            SetupSpotlight();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Fever] 피버 연출 셋업 완료. 씬을 Ctrl+S 로 저장하세요.");
        }

        // ---------------- 배너 + 사운드 + 캐릭터 가속 ----------------

        static void SetupPresentation()
        {
            FeverPresentation existing = Object.FindFirstObjectByType<FeverPresentation>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log(
                    "[Fever] FeverPresentation 이 이미 있습니다. 설정을 덮어쓰지 않았습니다.",
                    existing);
                return;
            }

            Transform canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError(
                    "[Fever] UI 캔버스를 찾지 못했습니다. " +
                    "먼저 Tools/UI/Setup Score Rank HUD 를 실행하세요.");
                return;
            }

            var root = new GameObject(PresentationRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Fever Presentation");
            var presentation = EnsureComponent<FeverPresentation>(root);

            Image banner = CreateBanner(canvas);
            var serialized = new SerializedObject(presentation);
            SetObjectReference(serialized, "bannerImage", banner);
            SetObjectReference(
                serialized,
                "introClip",
                AssetDatabase.LoadAssetAtPath<AudioClip>(IntroClipPath));

            AssignAcceleratedAnimators(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presentation);

            if (AssetDatabase.LoadAssetAtPath<AudioClip>(IntroClipPath) == null)
                Debug.LogWarning($"[Fever] 진입 사운드를 찾지 못했습니다: {IntroClipPath}");

            Selection.activeGameObject = root;
        }

        /// <summary>Friends 를 찾아 가속 대상으로 넣는다. 없으면 비워 두고 경고만 남긴다.</summary>
        static void AssignAcceleratedAnimators(SerializedObject serialized)
        {
            SerializedProperty list = serialized.FindProperty("acceleratedAnimators");
            if (list == null) return;

            var targets = new List<SpriteSheetAnimator>();
            SpriteSheetAnimator[] animators = Object.FindObjectsByType<SpriteSheetAnimator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < animators.Length; i++)
                if (animators[i].name == "Friends") targets.Add(animators[i]);

            if (targets.Count == 0)
            {
                Debug.LogWarning(
                    "[Fever] 'Friends' 애니메이터를 찾지 못했습니다. " +
                    "Tools/Art/Setup Raccoon And Friends 를 먼저 실행하거나, " +
                    "FeverPresentation 의 acceleratedAnimators 에 직접 넣으세요.");
                return;
            }

            list.arraySize = targets.Count;
            for (int i = 0; i < targets.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
        }

        static Image CreateBanner(Transform canvas)
        {
            var go = new GameObject(BannerName, typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "Create Fever Banner");
            go.transform.SetParent(canvas, false);
            go.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(684f, 312f); // 114x52 스프라이트의 6배

            Image image = go.GetComponent<Image>();
            image.sprite = LoadBannerSprite();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;

            Color color = Color.white;
            color.a = 0f;
            image.color = color;

            if (image.sprite == null)
                Debug.LogWarning($"[Fever] 배너 스프라이트를 찾지 못했습니다: {BannerSpritePath}");

            return image;
        }

        /// <summary>
        /// fevertime.png 는 spriteMode = Multiple 로 들어와 있어 메인 에셋이 Texture2D 다.
        /// 임포터를 바꾸지 않고 하위 스프라이트를 직접 꺼낸다 (아트 담당 자산을 건드리지 않는다).
        /// </summary>
        static Sprite LoadBannerSprite()
        {
            var direct = AssetDatabase.LoadAssetAtPath<Sprite>(BannerSpritePath);
            if (direct != null) return direct;

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(BannerSpritePath);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] is Sprite sprite)
                {
                    Debug.Log(
                        $"[Fever] fevertime.png 가 Multiple 로 임포트돼 있어 " +
                        $"하위 스프라이트 '{sprite.name}' 를 사용합니다.");
                    return sprite;
                }
            }

            return null;
        }

        // ---------------- 피버 스포트라이트 ----------------

        static void SetupSpotlight()
        {
            FeverSpotlight existing = Object.FindFirstObjectByType<FeverSpotlight>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log(
                    "[Fever] FeverSpotlight 가 이미 있습니다. 설정을 덮어쓰지 않았습니다.",
                    existing);
                return;
            }

            var lightingRoot = GameObject.Find("StageLighting");
            if (lightingRoot == null)
            {
                Debug.LogWarning(
                    "[Fever] StageLighting 이 없어 스포트라이트를 루트에 만듭니다. " +
                    "먼저 Tools/Lighting/Setup Stage Lighting 을 실행하는 편이 정리에 좋습니다.");
            }

            var go = new GameObject(SpotlightName);
            Undo.RegisterCreatedObjectUndo(go, "Create Fever Spotlight");
            if (lightingRoot != null) go.transform.SetParent(lightingRoot.transform, false);

            // PixelSpotlight2D 는 transform.up 을 조사 방향으로 쓴다 → 아래를 향하도록 180도
            go.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            PlaceAtCameraTop(go.transform);

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerAngle = 24f;
            light.pointLightOuterAngle = 46f;
            light.pointLightInnerRadius = 0.5f;
            light.pointLightOuterRadius = 18f;  // 화면 하단까지 닿는 길이
            light.falloffIntensity = 0.95f;
            light.shadowSoftness = 0.05f;
            light.color = new Color(1f, 0.85f, 0.15f, 1f);
            light.intensity = 0f;               // 피버 전에는 꺼져 있다

            Undo.AddComponent<FeverSpotlight>(go);
            Selection.activeGameObject = go;
        }

        static void PlaceAtCameraTop(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                target.position = new Vector3(0f, 7f, 0f);
                return;
            }

            float halfHeight = camera.orthographic
                ? camera.orthographicSize
                : Mathf.Abs(camera.transform.position.z) *
                  Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 center = camera.transform.position;
            target.position = new Vector3(center.x, center.y + halfHeight + 0.5f, 0f);
        }

        static Transform FindHudCanvas()
        {
            ScoreUI score = Object.FindFirstObjectByType<ScoreUI>(FindObjectsInactive.Include);
            if (score != null)
            {
                Canvas scoreCanvas = score.GetComponentInParent<Canvas>(true);
                if (scoreCanvas != null) return scoreCanvas.transform;
            }

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i].name == "HypeCanvas") return canvases[i].transform;

            return canvases.Length > 0 ? canvases[0].transform : null;
        }
    }
}
