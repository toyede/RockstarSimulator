#if UNITY_EDITOR
using ContextStage;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ContextStageEditor
{
    /// <summary>Title 메뉴의 랭킹 버튼과 서버 랭킹 팝업을 안전하게 한 번만 구성한다.</summary>
    public static class TitleLeaderboardSetup
    {
        const string TitleScenePath = "Assets/Scenes/Title.unity";
        const string ButtonsPrefabPath = "Assets/Prefabs/UI/Buttons.prefab";
        const string ButtonPrefabPath = "Assets/Prefabs/UI/Button.prefab";
        const string CreditPopupPrefabPath = "Assets/Prefabs/UI/CreditPopup.prefab";

        [MenuItem("Tools/UI/Setup Title Leaderboard", false, 35)]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[TitleLeaderboard] Play Mode를 종료한 뒤 다시 실행하세요.");
                return;
            }

            if (!SetupButtonsPrefab()) return;
            if (!SetupTitleScene()) return;

            AssetDatabase.SaveAssets();
            Debug.Log("[TitleLeaderboard] Title 씬 랭킹 버튼과 TOP 10 팝업 구성을 완료했습니다.");
        }

        static bool SetupButtonsPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(ButtonsPrefabPath);
            if (contents == null)
            {
                Debug.LogError($"[TitleLeaderboard] 프리팹을 찾지 못했습니다: {ButtonsPrefabPath}");
                return false;
            }

            try
            {
                TitleLeaderboardLauncher launcher =
                    contents.GetComponent<TitleLeaderboardLauncher>();
                if (launcher == null)
                    launcher = contents.AddComponent<TitleLeaderboardLauncher>();

                Transform rankingTransform = contents.transform.Find("RankingButton");
                GameObject rankingObject;
                if (rankingTransform != null)
                {
                    rankingObject = rankingTransform.gameObject;
                }
                else
                {
                    GameObject buttonPrefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
                    if (buttonPrefab == null)
                    {
                        Debug.LogError(
                            $"[TitleLeaderboard] 버튼 프리팹을 찾지 못했습니다: {ButtonPrefabPath}");
                        return false;
                    }

                    rankingObject = PrefabUtility.InstantiatePrefab(
                        buttonPrefab,
                        contents.transform) as GameObject;
                    if (rankingObject == null) return false;
                    rankingObject.name = "RankingButton";
                }

                Transform exitButton = contents.transform.Find("ExitButton");
                rankingObject.transform.SetSiblingIndex(
                    exitButton != null
                        ? exitButton.GetSiblingIndex()
                        : contents.transform.childCount - 1);

                Text label = rankingObject.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "랭킹";

                Button button = rankingObject.GetComponent<Button>();
                if (button == null)
                {
                    Debug.LogError("[TitleLeaderboard] RankingButton에 Button 컴포넌트가 없습니다.");
                    return false;
                }

                button.onClick = new Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(
                    button.onClick,
                    launcher.OpenLeaderboard);

                PrefabUtility.SaveAsPrefabAsset(contents, ButtonsPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static bool SetupTitleScene()
        {
            Scene titleScene = SceneManager.GetSceneByPath(TitleScenePath);
            bool openedForSetup = !titleScene.IsValid() || !titleScene.isLoaded;
            if (openedForSetup)
                titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Additive);

            try
            {
                Canvas canvas = FindInScene<Canvas>(titleScene);
                if (canvas == null)
                {
                    Debug.LogError("[TitleLeaderboard] Title 씬에서 Canvas를 찾지 못했습니다.");
                    return false;
                }

                TitleLeaderboardPopup popup = FindInScene<TitleLeaderboardPopup>(titleScene);
                if (popup == null)
                {
                    GameObject popupPrefab =
                        AssetDatabase.LoadAssetAtPath<GameObject>(CreditPopupPrefabPath);
                    if (popupPrefab == null)
                    {
                        Debug.LogError(
                            $"[TitleLeaderboard] 팝업 프리팹을 찾지 못했습니다: {CreditPopupPrefabPath}");
                        return false;
                    }

                    GameObject popupObject = PrefabUtility.InstantiatePrefab(
                        popupPrefab,
                        canvas.transform) as GameObject;
                    if (popupObject == null) return false;

                    popupObject.name = "TitleLeaderboardPopup";
                    popupObject.transform.SetAsLastSibling();
                    popup = popupObject.AddComponent<TitleLeaderboardPopup>();
                }

                ConfigurePopup(popup);
                ResizeTitleMenu(canvas);

                EditorSceneManager.MarkSceneDirty(titleScene);
                return EditorSceneManager.SaveScene(titleScene);
            }
            finally
            {
                if (openedForSetup && titleScene.IsValid() && titleScene.isLoaded)
                    EditorSceneManager.CloseScene(titleScene, true);
            }
        }

        static void ConfigurePopup(TitleLeaderboardPopup popup)
        {
            GameObject root = popup.gameObject;
            root.SetActive(true);
            root.transform.SetAsLastSibling();

            Text contentText = FindLargestNonButtonText(root);
            if (contentText != null)
            {
                contentText.text = "전체 랭킹 TOP 10\n\n랭킹을 불러오는 중...";
                contentText.alignment = TextAnchor.UpperCenter;
                contentText.fontSize = 42;
                contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
                contentText.verticalOverflow = VerticalWrapMode.Truncate;

                SerializedObject serializedPopup = new SerializedObject(popup);
                SerializedProperty textProperty =
                    serializedPopup.FindProperty("leaderboardText");
                if (textProperty != null)
                {
                    textProperty.objectReferenceValue = contentText;
                    serializedPopup.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Button closeButton = root.GetComponentInChildren<Button>(true);
            if (closeButton != null)
            {
                closeButton.gameObject.name = "CloseButton";
                Text closeLabel = closeButton.GetComponentInChildren<Text>(true);
                if (closeLabel != null) closeLabel.text = "닫기";

                closeButton.onClick = new Button.ButtonClickedEvent();
                UnityEventTools.AddPersistentListener(
                    closeButton.onClick,
                    popup.OnClickClose);
            }

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        static void ResizeTitleMenu(Canvas canvas)
        {
            Transform buttons = FindChildRecursive(canvas.transform, "Buttons");
            if (buttons is not RectTransform rect) return;

            Vector2 size = rect.sizeDelta;
            size.y = Mathf.Max(size.y, 700f);
            rect.sizeDelta = size;
        }

        static Text FindLargestNonButtonText(GameObject root)
        {
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            Text best = null;
            float bestArea = -1f;
            for (int i = 0; i < texts.Length; i++)
            {
                Text candidate = texts[i];
                if (candidate.GetComponentInParent<Button>() != null) continue;

                Rect rect = candidate.rectTransform.rect;
                float area = Mathf.Abs(rect.width * rect.height);
                if (area <= bestArea) continue;

                best = candidate;
                bestArea = area;
            }

            return best;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }

            return null;
        }

        static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), childName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
#endif
