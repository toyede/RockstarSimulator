using System.Collections.Generic;
using GameJamKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 팝업 뒤 블러 배경 셋업.
    ///
    /// <c>Tools/UI/Setup Blur Backdrop</c> 한 번이면:
    /// <list type="number">
    /// <item><c>Assets/Prefabs/UI/BlurBackdrop.prefab</c> 을 만들고(없을 때만)</item>
    /// <item>대상 프리팹 안의 모든 <c>UIPopup</c> 을 찾아</item>
    /// <item>각 팝업의 <b>첫 자식</b>으로 블러 배경을 끼워 넣는다 (내용보다 뒤에 그려지도록)</item>
    /// </list>
    ///
    /// 이미 배경이 있는 팝업은 건너뛴다. 여러 번 실행해도 안전하다.
    ///
    /// <b>팀원 소유 프리팹을 수정한다.</b> (PausePopup / ScoreCanvas)
    /// 실행 후 담당자에게 공유할 것.
    /// </summary>
    public static class UIBlurBackdropSetup
    {
        const string BackdropPrefabPath = "Assets/Prefabs/UI/BlurBackdrop.prefab";
        const string BlurShaderPath = "Assets/Shaders/UIKawaseBlur.shader";
        const string BackdropName = "BlurBackdrop";

        /// <summary>블러 배경을 넣을 팝업 프리팹들. 새 팝업이 생기면 여기에 추가한다.</summary>
        static readonly string[] TargetPrefabs =
        {
            "Assets/Prefabs/UI/PausePopup.prefab",
            "Assets/Prefabs/UI/ScoreCanvas.prefab",
        };

        [MenuItem("Tools/UI/Setup Blur Backdrop", false, 31)]
        public static void Setup()
        {
            GameObject backdropPrefab = EnsureBackdropPrefab();
            if (backdropPrefab == null) return;

            int injected = 0;
            int skipped = 0;
            for (int i = 0; i < TargetPrefabs.Length; i++)
                InjectIntoPrefab(TargetPrefabs[i], backdropPrefab, ref injected, ref skipped);

            AssetDatabase.SaveAssets();
            Selection.activeObject = backdropPrefab;
            Debug.Log(
                $"[UIBlur] 블러 배경 셋업 완료. 새로 넣은 팝업 {injected}개 / 이미 있던 팝업 {skipped}개.\n" +
                "흐림 정도는 BlurBackdrop 프리팹의 downscale·blurPasses 로, " +
                "어둡기는 RawImage 의 color 로 조절합니다.",
                backdropPrefab);
        }

        // ---------------- 프리팹 만들기 ----------------

        static GameObject EnsureBackdropPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BackdropPrefabPath);
            if (existing != null) return existing;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(BlurShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[UIBlur] 블러 셰이더를 찾지 못했습니다: {BlurShaderPath}");
                return null;
            }

            EnsureFolder("Assets/Prefabs/UI");

            var temp = new GameObject(BackdropName, typeof(RectTransform), typeof(RawImage));
            try
            {
                temp.layer = LayerMask.NameToLayer("UI");

                // 화면 전체를 덮도록 네 모서리에 붙인다
                RectTransform rect = temp.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;

                RawImage image = temp.GetComponent<RawImage>();
                image.raycastTarget = false;
                image.enabled = false;
                // 살짝 어둡게 깔아 팝업 글자가 읽히게 한다. 여기 값만 바꾸면 어둡기가 조절된다
                image.color = new Color(0.65f, 0.65f, 0.7f, 1f);

                var backdrop = temp.AddComponent<UIBlurBackdrop>();
                var serialized = new SerializedObject(backdrop);
                SetObjectReference(serialized, "backdropImage", image);
                SetObjectReference(serialized, "blurShader", shader);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, BackdropPrefabPath);
                Debug.Log($"[UIBlur] 블러 배경 프리팹을 만들었습니다: {BackdropPrefabPath}");
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        // ---------------- 팝업에 끼워 넣기 ----------------

        static void InjectIntoPrefab(
            string prefabPath,
            GameObject backdropPrefab,
            ref int injected,
            ref int skipped)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                Debug.LogWarning($"[UIBlur] 프리팹을 찾지 못해 건너뜁니다: {prefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            bool changed = false;
            try
            {
                var popups = new List<UIPopup>(root.GetComponentsInChildren<UIPopup>(true));
                if (popups.Count == 0)
                {
                    Debug.LogWarning($"[UIBlur] UIPopup 이 없어 건너뜁니다: {prefabPath}");
                    return;
                }

                for (int i = 0; i < popups.Count; i++)
                {
                    UIPopup popup = popups[i];
                    if (popup.GetComponentInChildren<UIBlurBackdrop>(true) != null)
                    {
                        skipped++;
                        continue;
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(
                        backdropPrefab,
                        popup.transform);
                    if (instance == null) continue;

                    // 팝업 내용보다 먼저 그려지도록 첫 자식으로 보낸다
                    instance.transform.SetAsFirstSibling();
                    StretchToParent(instance.GetComponent<RectTransform>());

                    // 캡처 순간 자기 팝업을 잠깐 투명하게 만들 대상을 명시해 둔다
                    var backdrop = instance.GetComponent<UIBlurBackdrop>();
                    var serialized = new SerializedObject(backdrop);
                    SetObjectReference(serialized, "popupGroup", popup.GetComponent<CanvasGroup>());
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    injected++;
                    changed = true;
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (changed)
                Debug.Log($"[UIBlur] 블러 배경을 넣었습니다: {prefabPath}");
        }

        static void StretchToParent(RectTransform rect)
        {
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
