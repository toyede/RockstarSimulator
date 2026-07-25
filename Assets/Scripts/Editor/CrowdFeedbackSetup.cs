using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;
using LegacyText = UnityEngine.UI.Text;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 관객 피드백 연출 셋업.
    ///
    /// <list type="bullet">
    /// <item>관객 입퇴장 텍스트 (+1 관객 / -3 관객) — 관객 무리 위 월드 텍스트</item>
    /// <item>특별 관객 말풍선 — SpecialAudience 프리팹에 자식으로 추가</item>
    /// <item>저격 성공 보상 배너 — 기존 HUD 캔버스에 추가</item>
    /// </list>
    ///
    /// 전부 <b>이미 있는 것은 덮어쓰지 않는다.</b> 여러 번 실행해도 안전하다.
    /// 실행 후 씬을 Ctrl+S 로 저장할 것.
    /// </summary>
    public static class CrowdFeedbackSetup
    {
        const string FlowTextRootName = "[AudienceFlowText]";
        const string BubbleChildName = "SpeechBubble";
        const string BannerRootName = "SpecialHitBanner";
        const string SpecialAudiencePrefabPath =
            "Assets/Prefabs/Audience/SpecialAudience.prefab";

        [MenuItem("Tools/Feedback/Setup All Crowd Feedback", false, 0)]
        public static void SetupAll()
        {
            SetupAudienceFlowText();
            SetupSpeechBubble();
            SetupSpecialHitBanner();
            Debug.Log("[Feedback] 관객 피드백 셋업 완료. 씬을 Ctrl+S 로 저장하세요.");
        }

        // ---------------- 관객 입퇴장 텍스트 ----------------

        [MenuItem("Tools/Feedback/Setup Audience Flow Text", false, 20)]
        public static void SetupAudienceFlowText()
        {
            AudienceFlowTextUI existing = Object.FindFirstObjectByType<AudienceFlowTextUI>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log(
                    "[Feedback] AudienceFlowTextUI 가 이미 있습니다. 설정을 덮어쓰지 않았습니다.",
                    existing);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var root = GameObject.Find(FlowTextRootName);
            if (root == null)
            {
                root = new GameObject(FlowTextRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Audience Flow Text");
            }

            AudienceFlowTextUI view = EnsureComponent<AudienceFlowTextUI>(root);
            SetObjectField(view, "font", ProjectFontTool.TmpFont);

            // 관객 무리를 찾아 그 위에 얹어 둔다. 런타임에도 스스로 따라가지만
            // 에디터에서 위치를 눈으로 확인할 수 있게 미리 맞춰 준다.
            AudienceRosterPresenter presenter =
                Object.FindFirstObjectByType<AudienceRosterPresenter>(
                    FindObjectsInactive.Include);
            if (presenter != null && presenter.MemberRoot != null)
                root.transform.position = presenter.MemberRoot.position + new Vector3(0f, 2.6f, 0f);
            else
                Debug.LogWarning(
                    "[Feedback] AudienceRosterPresenter 를 찾지 못해 위치를 맞추지 못했습니다. " +
                    "런타임에는 스스로 관객 무리를 따라갑니다.");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(
                "[Feedback] 관객 입퇴장 텍스트를 만들었습니다. " +
                "followOffset 으로 높이를 조절하세요. (씬을 Ctrl+S 로 저장할 것)",
                root);
        }

        // ---------------- 특별 관객 말풍선 ----------------

        [MenuItem("Tools/Feedback/Setup Special Audience Speech Bubble", false, 21)]
        public static void SetupSpeechBubble()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SpecialAudiencePrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError(
                    $"[Feedback] 특별 관객 프리팹을 찾지 못했습니다: {SpecialAudiencePrefabPath}");
                return;
            }

            try
            {
                Transform existing = prefabRoot.transform.Find(BubbleChildName);
                if (existing != null &&
                    existing.GetComponent<SpecialAudienceSpeechBubble>() != null)
                {
                    Debug.Log("[Feedback] 말풍선이 이미 있습니다. 설정을 덮어쓰지 않았습니다.");
                    return;
                }

                // 반드시 프리팹 '루트'의 자식이어야 한다.
                // VisualRoot 아래에 두면 액터가 진행 방향에 따라 X 스케일을 뒤집을 때 글자가 반전된다.
                GameObject bubble = existing != null
                    ? existing.gameObject
                    : new GameObject(BubbleChildName);
                bubble.transform.SetParent(prefabRoot.transform, worldPositionStays: false);
                bubble.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                bubble.transform.localScale = Vector3.one;

                var text = bubble.GetComponent<TextMeshPro>();
                if (text == null) text = bubble.AddComponent<TextMeshPro>();
                text.text = "Mosh!";
                text.fontSize = 3f;
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.raycastTarget = false;
                if (ProjectFontTool.TmpFont != null) text.font = ProjectFontTool.TmpFont;

                var view = bubble.GetComponent<SpecialAudienceSpeechBubble>();
                if (view == null) view = bubble.AddComponent<SpecialAudienceSpeechBubble>();

                var serialized = new SerializedObject(view);
                SetObjectReference(serialized, "bubbleText", text);
                SetObjectReference(
                    serialized,
                    "actor",
                    prefabRoot.GetComponentInChildren<SpecialAudienceCrowdActor>(true));
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, SpecialAudiencePrefabPath);
                Debug.Log(
                    "[Feedback] 특별 관객 말풍선을 프리팹에 추가했습니다. " +
                    "문구·색은 SpeechBubble 의 lines 표에서 조절합니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            WarnIfMissingGlyphs();
        }

        /// <summary>
        /// DungGeunMo SDF 아틀라스에 ♪ 같은 글자가 없으면 빌드에서 두부(□)로 나올 수 있다.
        /// 셋업 시점에 미리 알려서 문구를 바꾸거나 폰트에 글자를 추가하게 한다.
        /// </summary>
        static void WarnIfMissingGlyphs()
        {
            TMP_FontAsset font = ProjectFontTool.TmpFont;
            if (font == null) return;

            const char MusicNote = '♪';
            if (font.HasCharacter(MusicNote)) return;

            Debug.LogWarning(
                $"[Feedback] 프로젝트 폰트에 '{MusicNote}' 글자가 없습니다. " +
                "Singalong 문구가 □ 로 보이면 Font Asset Creator 의 Custom Character List 에 " +
                "추가하거나, SpeechBubble 의 lines 에서 문구를 바꾸세요.");
        }

        // ---------------- 저격 성공 보상 배너 ----------------

        [MenuItem("Tools/Feedback/Setup Special Hit Banner", false, 22)]
        public static void SetupSpecialHitBanner()
        {
            SpecialHitRewardBanner existing =
                Object.FindFirstObjectByType<SpecialHitRewardBanner>(
                    FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log(
                    "[Feedback] SpecialHitRewardBanner 가 이미 있습니다. 설정을 덮어쓰지 않았습니다.",
                    existing);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            Transform canvas = FindHudCanvas();
            if (canvas == null)
            {
                Debug.LogError(
                    "[Feedback] UI 캔버스를 찾지 못했습니다. " +
                    "먼저 Tools/UI/Setup Score Rank HUD 를 실행하세요.");
                return;
            }

            var root = new GameObject(BannerRootName, typeof(RectTransform), typeof(CanvasGroup));
            Undo.RegisterCreatedObjectUndo(root, "Create Special Hit Banner");
            root.transform.SetParent(canvas, false);
            root.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -160f); // 상단 HUD 아래
            rect.sizeDelta = new Vector2(900f, 130f);

            LegacyText headline = CreateBannerText(
                root.transform, "Headline", "★ SPECIAL HIT ★", 48,
                0f, new Color(1f, 0.85f, 0.3f, 1f));
            LegacyText detail = CreateBannerText(
                root.transform, "Detail", "+0", 32,
                -62f, new Color(0.95f, 0.98f, 1f, 1f));

            var banner = Undo.AddComponent<SpecialHitRewardBanner>(root);
            var serialized = new SerializedObject(banner);
            SetObjectReference(serialized, "legacyHeadlineText", headline);
            SetObjectReference(serialized, "legacyDetailText", detail);
            SetObjectReference(serialized, "canvasGroup", root.GetComponent<CanvasGroup>());
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(
                "[Feedback] 저격 성공 배너를 만들었습니다. " +
                "인스펙터 ⋮ 메뉴의 Debug/Play Sample Banner 로 먼저 확인하세요. " +
                "(씬을 Ctrl+S 로 저장할 것)",
                root);
        }

        static LegacyText CreateBannerText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            float y,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LegacyText));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(900f, 56f);

            LegacyText text = go.GetComponent<LegacyText>();
            text.text = content;
            text.font = ProjectFontTool.LegacyFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static Transform FindHudCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            // 점수 HUD 가 올라가 있는 캔버스를 우선한다 (같은 화면 레이어에 얹기 위해)
            ScoreUI score = Object.FindFirstObjectByType<ScoreUI>(FindObjectsInactive.Include);
            if (score != null)
            {
                Canvas scoreCanvas = score.GetComponentInParent<Canvas>(true);
                if (scoreCanvas != null) return scoreCanvas.transform;
            }

            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i].name == "HypeCanvas") return canvases[i].transform;

            return canvases.Length > 0 ? canvases[0].transform : null;
        }
    }
}
