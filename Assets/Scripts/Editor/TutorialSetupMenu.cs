using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 튜토리얼 셋업 메뉴. (다른 셋업 메뉴들과 같은 방식)
    ///
    /// Tools/Tutorial/Setup Tutorial 한 번이면:
    ///   1. [Tutorial] 오브젝트 + TutorialFlow / TutorialOverlayUI / TutorialTips
    ///   2. TutorialCanvas (sortingOrder 100 — 카드(20)·특별관객(30) 위) + 메시지 패널 + 스킵 버튼 + 팁 텍스트
    ///   3. 레퍼런스 연결
    /// 까지 끝난다. 이미 있는 것은 건드리지 않으므로 여러 번 실행해도 안전하다.
    /// </summary>
    public static class TutorialSetupMenu
    {
        [MenuItem("Tools/Tutorial/Setup Tutorial", false, 0)]
        public static void SetupScene()
        {
            // 1) 루트
            var root = GameObject.Find("[Tutorial]");
            if (root == null)
            {
                root = new GameObject("[Tutorial]");
                Undo.RegisterCreatedObjectUndo(root, "Create Tutorial");
            }

            var overlay = Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>(FindObjectsInactive.Include);
            if (overlay == null) overlay = BuildOverlay(root.transform);

            var flow = root.GetComponent<ContextStage.TutorialFlow>();
            if (flow == null) flow = Undo.AddComponent<ContextStage.TutorialFlow>(root);
            SetObjectField(flow, "overlay", overlay);

            var tips = root.GetComponent<ContextStage.TutorialTips>();
            if (tips == null) tips = Undo.AddComponent<ContextStage.TutorialTips>(root);
            EnsureTipText(tips, overlay.transform.parent != null ? overlay.GetComponentInParent<Canvas>() : null);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("[Tutorial] 셋업 완료. 첫 공연 시작 시 자동으로 뜹니다 " +
                      "(TutorialFlow ⋮ → Debug/Start Tutorial 로 강제 실행 가능). 씬을 Ctrl+S 로 저장할 것!");
        }

        // ---------------- UI 빌드 ----------------

        static ContextStage.TutorialOverlayUI BuildOverlay(Transform parent)
        {
            // 캔버스 — 카드(20)·특별관객(30)·기존 HUD 보다 위
            var canvasGo = new GameObject("TutorialCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create TutorialCanvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var overlay = canvasGo.AddComponent<ContextStage.TutorialOverlayUI>();

            // ---- 메시지 패널 (상단 중앙) ----
            var panel = CreateRect("TutorialPanel", canvasGo.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -80f), new Vector2(980f, 170f));

            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.1f, 0.88f);

            // 패널 전체가 '계속' 버튼
            var continueButton = panel.gameObject.AddComponent<Button>();
            continueButton.transition = Selectable.Transition.None;

            var main = CreateText(panel, "MainText", "튜토리얼", 34, TextAnchor.MiddleCenter,
                new Vector2(0f, -14f), new Vector2(-40f, 60f), stretchTop: true);
            main.color = new Color(1f, 0.9f, 0.5f);

            var sub = CreateText(panel, "SubText", "", 24, TextAnchor.UpperCenter,
                new Vector2(0f, -78f), new Vector2(-60f, 80f), stretchTop: true);
            sub.color = new Color(0.92f, 0.92f, 0.92f);

            var hint = CreateText(panel, "ContinueHint", "▼ 클릭해서 계속", 18, TextAnchor.LowerRight,
                new Vector2(-16f, 8f), new Vector2(260f, 26f), stretchTop: false);
            hint.color = new Color(0.7f, 0.85f, 1f, 0.9f);

            // ---- 스킵 버튼 (우상단) ----
            var skipRect = CreateRect("SkipButton", canvasGo.transform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -24f), new Vector2(190f, 52f));
            var skipImage = skipRect.gameObject.AddComponent<Image>();
            skipImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            var skipButton = skipRect.gameObject.AddComponent<Button>();
            var skipLabel = CreateText(skipRect, "Label", "튜토리얼 건너뛰기 ▶", 20, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, stretchTop: false, stretchAll: true);
            skipLabel.color = Color.white;

            // ---- 연결 ----
            SetObjectField(overlay, "panelRoot", panel.gameObject);
            SetObjectField(overlay, "mainText", main);
            SetObjectField(overlay, "subText", sub);
            SetObjectField(overlay, "continueHint", hint.gameObject);
            SetObjectField(overlay, "continueButton", continueButton);
            SetObjectField(overlay, "skipButton", skipButton);

            panel.gameObject.SetActive(false);
            skipRect.gameObject.SetActive(false);
            return overlay;
        }

        static void EnsureTipText(ContextStage.TutorialTips tips, Canvas canvas)
        {
            if (canvas == null) return;

            var existing = canvas.transform.Find("TipText");
            Text tip;
            if (existing != null) tip = existing.GetComponent<Text>();
            else
            {
                var rect = CreateRect("TipText", canvas.transform,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -270f), new Vector2(900f, 44f));
                tip = rect.gameObject.AddComponent<Text>();
                tip.font = ProjectFontTool.LegacyFont;
                tip.fontSize = 26;
                tip.fontStyle = FontStyle.Bold;
                tip.alignment = TextAnchor.MiddleCenter;
                tip.color = new Color(1f, 0.95f, 0.6f, 0f);
                tip.raycastTarget = false;

                var outline = rect.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
            SetObjectField(tips, "tipText", tip);
        }

        // ---------------- 헬퍼 ----------------

        static RectTransform CreateRect(
            string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        static Text CreateText(
            RectTransform parent, string name, string content, int fontSize, TextAnchor anchor,
            Vector2 anchoredPosition, Vector2 sizeDelta, bool stretchTop, bool stretchAll = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;

            if (stretchAll)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else if (stretchTop)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = sizeDelta;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = sizeDelta;
            }

            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = ProjectFontTool.LegacyFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.raycastTarget = false;
            return text;
        }
    }
}
