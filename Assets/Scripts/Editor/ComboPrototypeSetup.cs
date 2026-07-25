using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage.EditorTools
{
    public static class ComboPrototypeSetup
    {
        const string SystemRootName = "[Combo]";
        const string LegacyCanvasName = "ComboCanvas";
        const string ResultGroupName = "ComboResultGroup";
        const string CardReactionTextName = "CardReactionText";
        const string CardTotalTextName = "CardTotalText";
        const string ComboTextName = "ComboText";

        [MenuItem("Tools/Combo/Setup Combo Prototype", false, 0)]
        public static void Setup()
        {
            ComboSystem combo = Object.FindFirstObjectByType<ComboSystem>(
                FindObjectsInactive.Include);
            if (combo == null)
            {
                GameObject systemRoot = GameObject.Find(SystemRootName);
                if (systemRoot == null)
                {
                    systemRoot = new GameObject(SystemRootName);
                    Undo.RegisterCreatedObjectUndo(systemRoot, "Create Combo System");
                }

                combo = Undo.AddComponent<ComboSystem>(systemRoot);
            }

            combo.gameObject.SetActive(true);
            combo.enabled = true;

            RemoveLegacyPrototypeCanvas();

            Transform host = FindHudHost();
            if (host == null)
            {
                Debug.LogError(
                    "[Combo] No UI Canvas exists. Create the score/rank HUD first, " +
                    "then run this setup again.");
                return;
            }

            // 우측 상단 HUD 에 결과 그룹(카드 총점 + 콤보)을 세로로 배치한다.
            Transform group = FindOrCreateResultGroup(host);

            CardReactionTextUI reactionView = FindOrCreateCardReactionText(group);
            CardTotalTextUI totalView = FindOrCreateCardTotalText(group);
            ComboTextUI view = FindOrCreateComboText(group);

            reactionView.gameObject.SetActive(true);
            reactionView.enabled = true;
            totalView.gameObject.SetActive(true);
            totalView.enabled = true;
            view.gameObject.SetActive(true);
            view.enabled = true;

            EditorUtility.SetDirty(combo);
            EditorUtility.SetDirty(reactionView);
            EditorUtility.SetDirty(totalView);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = view.gameObject;
            Debug.Log(
                "[Combo] Setup complete. Card total + combo are displayed together " +
                "at the upper-right score/rank HUD (single result UI).");
        }

        static void RemoveLegacyPrototypeCanvas()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name != LegacyCanvasName) continue;
                Undo.DestroyObjectImmediate(canvases[i].gameObject);
            }
        }

        static Transform FindHudHost()
        {
            string[] preferredNames = { "ScoreRankHUD", "RankScoreHUD", "ScoreHUD" };
            for (int i = 0; i < preferredNames.Length; i++)
            {
                Transform explicitHud = FindSceneTransform(preferredNames[i]);
                if (explicitHud != null) return explicitHud;
            }

            ScoreUI score = Object.FindFirstObjectByType<ScoreUI>(
                FindObjectsInactive.Include);
            if (score != null)
            {
                Canvas scoreCanvas = score.GetComponentInParent<Canvas>(true);
                Transform scoreParent = score.transform.parent;
                if (scoreParent != null &&
                    scoreParent.GetComponent<Canvas>() == null)
                    return scoreParent;
                if (scoreCanvas != null) return scoreCanvas.transform;
            }

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name == "HypeCanvas")
                    return canvases[i].transform;
            }
            return canvases.Length > 0 ? canvases[0].transform : null;
        }

        /// <summary>카드 총점 + 콤보를 세로로 담는 그룹. 이미 있으면 재사용한다.</summary>
        static Transform FindOrCreateResultGroup(Transform host)
        {
            Transform existing = host.Find(ResultGroupName);
            if (existing != null) return existing;

            var go = new GameObject(ResultGroupName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Combo Result Group");
            go.transform.SetParent(host, false);
            go.layer = LayerMask.NameToLayer("UI");

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-40f, -104f); // 점수/랭크 HUD 아래
            rect.sizeDelta = new Vector2(460f, 150f);
            return go.transform;
        }

        // 세로 배치: 평가 라벨(위) → 총점 → 콤보
        const float ReactionY = 0f;
        const float TotalY = -44f;
        const float ComboY = -92f;

        static CardReactionTextUI FindOrCreateCardReactionText(Transform group)
        {
            CardReactionTextUI existing = Object.FindFirstObjectByType<CardReactionTextUI>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.transform.SetParent(group, false);
                ConfigureRect(existing.GetComponent<RectTransform>(), ReactionY);
                return existing;
            }

            Text text = CreateHudText(group, CardReactionTextName, "LOVE IT!", 32, ReactionY);
            CardReactionTextUI view = Undo.AddComponent<CardReactionTextUI>(text.gameObject);
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("legacyReactionText").objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static CardTotalTextUI FindOrCreateCardTotalText(Transform group)
        {
            CardTotalTextUI existing = Object.FindFirstObjectByType<CardTotalTextUI>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.transform.SetParent(group, false);
                ConfigureRect(existing.GetComponent<RectTransform>(), TotalY);
                return existing;
            }

            Text text = CreateHudText(group, CardTotalTextName, "+0 SCORE", 34, TotalY);
            CardTotalTextUI view = Undo.AddComponent<CardTotalTextUI>(text.gameObject);
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("legacyTotalText").objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static ComboTextUI FindOrCreateComboText(Transform group)
        {
            ComboTextUI existing = Object.FindFirstObjectByType<ComboTextUI>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.transform.SetParent(group, false);
                ConfigureRect(existing.GetComponent<RectTransform>(), ComboY);
                return existing;
            }

            Text text = CreateHudText(group, ComboTextName, "COMBO 0", 30, ComboY);
            ComboTextUI view = Undo.AddComponent<ComboTextUI>(text.gameObject);
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("legacyComboText").objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static Text CreateHudText(Transform parent, string name, string content, int fontSize, float y)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");

            ConfigureRect(go.GetComponent<RectTransform>(), y);

            Text text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperRight;
            text.color = new Color(1f, 0.88f, 0.25f, 1f);
            text.raycastTarget = false;
            return text;
        }

        static void ConfigureRect(RectTransform rect, float y)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(460f, 48f);
        }

        static Transform FindSceneTransform(string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == objectName)
                    return transforms[i];
            }
            return null;
        }
    }
}
