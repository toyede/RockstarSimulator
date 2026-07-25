using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 호응도 시스템 씬 셋업 메뉴. (GameJamKit 의 Tools/GameJamKit 메뉴와 같은 방식)
    ///
    /// Tools/Hype/Setup Hype Scene 한 번이면:
    ///   1. GameJamKit 매니저([Managers]) 생성  ← 킷 메뉴 재사용
    ///   2. Assets/Settings/HypeConfig.asset 생성 (기획 수치가 기본값)
    ///   3. [HypeSystem] 오브젝트 + 디버그 입력 배치
    ///   4. HypeCanvas 에 게이지 바 + 숫자 UI 배치
    /// 까지 끝난다. 이미 있는 것은 건드리지 않으므로 여러 번 실행해도 안전하다.
    /// </summary>
    public static class HypeSetupMenu
    {
        const string ConfigPath = "Assets/Settings/HypeConfig.asset";

        [MenuItem("Tools/Hype/Setup Hype Scene", false, 0)]
        public static void SetupScene()
        {
            // 1) 킷 매니저 (GameManager/AudioManager/PoolManager/UIManager/CameraShake)
            GameJamKit.EditorTools.GameJamKitMenu.CreateManagers();

            // 2) 콘픽 에셋
            var config = GetOrCreateConfig();

            // 3) HypeSystem + 디버그 입력
            var sysGo = GameObject.Find("[HypeSystem]");
            if (sysGo == null)
            {
                sysGo = new GameObject("[HypeSystem]");
                Undo.RegisterCreatedObjectUndo(sysGo, "Create HypeSystem");
            }
            var system = EnsureComponent<HypeSystem>(sysGo);
            EnsureComponent<HypeDebugInput>(sysGo);
            SetObjectField(system, "config", config); // private [SerializeField] 라 SerializedObject 로 지정

            // 3-1) 점수 누적 시스템 (카드 낼 때 열기 배율만큼 점수 가산)
            var scoreGo = GameObject.Find("[ScoreSystem]");
            if (scoreGo == null)
            {
                scoreGo = new GameObject("[ScoreSystem]");
                Undo.RegisterCreatedObjectUndo(scoreGo, "Create ScoreSystem");
            }
            EnsureComponent<PerformanceScoreSystem>(scoreGo);

            // 4) 게이지 UI + 점수 UI
            BuildGaugeUI();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = sysGo;
            Debug.Log("[Hype] 씬 셋업 완료. Play 후 Space 로 공연을 시작하고 1~4 키로 판정을 테스트하세요.");
        }

        // ---------------- 콘픽 ----------------

        static HypeConfig GetOrCreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HypeConfig>(ConfigPath);
            if (existing != null) return existing; // 이미 있으면 수치를 덮어쓰지 않는다 (튜닝 보호)

            var asset = ScriptableObject.CreateInstance<HypeConfig>(); // 필드 기본값 = 기획 수치
            AssetDatabase.CreateAsset(asset, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Hype] {ConfigPath} 생성 완료.");
            return asset;
        }

        // ---------------- 게이지 UI ----------------

        static void BuildGaugeUI()
        {
            if (Object.FindFirstObjectByType<HypeGaugeUI>() != null) return; // 이미 배치됨

            // 캔버스 (Screen Space Overlay, 1920x1080 기준 스케일)
            var canvasGo = GameObject.Find("HypeCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("HypeCanvas");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create HypeCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // 게이지 루트: 화면 우측 중앙의 버티컬 바 (아래에서 위로 차오른다)
            var gaugeGo = CreateUIObject("HypeGauge", canvasGo.transform);
            var gaugeRect = gaugeGo.GetComponent<RectTransform>();
            gaugeRect.anchorMin = gaugeRect.anchorMax = new Vector2(1f, 0.5f); // 우측 중앙 고정
            gaugeRect.pivot = new Vector2(1f, 0.5f);
            gaugeRect.anchoredPosition = new Vector2(-40f, 0f);
            gaugeRect.sizeDelta = new Vector2(48f, 420f);

            // 배경 → 지연 바 → 메인 바 순서로 겹친다 (스프라이트 없는 Image = 그레이박스 사각형)
            var bg = CreateStretchedImage("BG", gaugeGo.transform, new Color(0.12f, 0.12f, 0.12f), 0f);
            bg.raycastTarget = false;

            var delayed = CreateStretchedImage("DelayedFill", gaugeGo.transform, new Color(0.45f, 0.45f, 0.45f), 4f);
            SetupFilled(delayed);

            var fill = CreateStretchedImage("Fill", gaugeGo.transform, Color.white, 4f);
            SetupFilled(fill);

            // 바 위 라벨(열기) + 바 아래 숫자
            var title = CreateText("TitleText", gaugeGo.transform, "열기", TextAnchor.MiddleCenter);
            PlaceVerticalText(title, above: true);
            var value = CreateText("ValueText", gaugeGo.transform, "30", TextAnchor.MiddleCenter);
            PlaceVerticalText(value, above: false);

            // 배율 표시(×N): 숫자 아래에 배치
            var mult = CreateText("MultiplierText", gaugeGo.transform, "×1", TextAnchor.MiddleCenter);
            PlaceVerticalText(mult, above: false);
            var multRect = mult.GetComponent<RectTransform>();
            multRect.anchoredPosition = new Vector2(0f, -46f); // 숫자보다 더 아래
            mult.fontSize = 30;
            mult.color = Color.white;

            // HypeGaugeUI 연결
            var ui = gaugeGo.AddComponent<HypeGaugeUI>();
            SetObjectField(ui, "fillImage", fill);
            SetObjectField(ui, "delayedImage", delayed);
            SetObjectField(ui, "valueText", value);
            SetObjectField(ui, "multiplierText", mult);

            // 점수 표시 UI (같은 캔버스 상단)
            BuildScoreUI(canvasGo.transform);
        }

        // ---------------- 점수 UI ----------------

        const int DefaultTargetScore = 5000; // 기획 확정 전 임시 목표점수 (baseScore=100 기준 추정치)

        static void BuildScoreUI(Transform canvas)
        {
            if (Object.FindFirstObjectByType<ScoreUI>() != null) return; // 이미 배치됨

            var scoreGo = CreateText("ScoreText", canvas, $"SCORE 0 / {DefaultTargetScore}", TextAnchor.UpperLeft);
            var rect = scoreGo.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); // 좌측 상단
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(40f, -30f);
            rect.sizeDelta = new Vector2(420f, 48f);
            scoreGo.fontSize = 36;
            scoreGo.color = Color.white;

            var ui = scoreGo.gameObject.AddComponent<ScoreUI>();
            SetObjectField(ui, "scoreText", scoreGo);

            // targetScore 는 int 필드라 SetObjectField(Object 전용) 대신 SerializedProperty 로 직접 설정
            var serialized = new SerializedObject(ui);
            serialized.FindProperty("targetScore").intValue = DefaultTargetScore;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- UI 생성 헬퍼 ----------------

        static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return go;
        }

        /// <summary>부모를 가득 채우는 Image 생성. inset 으로 안쪽 여백을 준다.</summary>
        static Image CreateStretchedImage(string name, Transform parent, Color color, float inset)
        {
            var go = CreateUIObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>아래에서 차오르는 버티컬 Filled 이미지로 설정 + 시작값(호응도 30 기준)을 맞춘다.</summary>
        static void SetupFilled(Image img)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Vertical;
            img.fillOrigin = (int)Image.OriginVertical.Bottom;
            img.fillAmount = 0.3f;
        }

        static Text CreateText(string name, Transform parent, string content, TextAnchor anchor)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 그레이박스용 내장 폰트
            text.fontSize = 26;
            text.alignment = anchor;
            text.color = new Color(0.9f, 0.9f, 0.9f);
            text.raycastTarget = false;
            return text;
        }

        /// <summary>버티컬 바의 위(라벨) 또는 아래(숫자)에 텍스트 배치.</summary>
        static void PlaceVerticalText(Text text, bool above)
        {
            var rect = text.GetComponent<RectTransform>();
            if (above)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f); // 바 상단에 붙임
                rect.pivot = new Vector2(0.5f, 0f);
                rect.anchoredPosition = new Vector2(0f, 10f);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f); // 바 하단에 붙임
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -10f);
            }
            rect.sizeDelta = new Vector2(160f, 34f);
        }

        // ---------------- 공용 헬퍼 ----------------

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            return comp != null ? comp : Undo.AddComponent<T>(go);
        }

    }
}
