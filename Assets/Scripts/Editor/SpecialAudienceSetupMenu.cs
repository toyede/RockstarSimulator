using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 특별 관객 셋업 메뉴. (HypeSetupMenu · AudioSetupMenu 와 같은 방식)
    ///
    /// Tools/Special Audience/Setup Special Audience 한 번이면:
    ///   1. [SpecialAudience] 오브젝트 + SpecialAudienceManager 배치
    ///   2. 그레이박스 UI (요구 텍스트 3종 + 남은 시간 바) 생성
    ///   3. Manager ↔ View 레퍼런스 연결
    /// 까지 끝난다. 이미 있으면 건드리지 않으므로 여러 번 실행해도 안전하다.
    ///
    /// 아트가 아이콘을 주면 View 인스펙터에서 아이콘 오브젝트만 갈아끼우면 된다.
    /// </summary>
    public static class SpecialAudienceSetupMenu
    {
        [MenuItem("Tools/Special Audience/Setup Special Audience", false, 0)]
        public static void SetupScene()
        {
            var go = GameObject.Find("[SpecialAudience]");
            if (go == null)
            {
                go = new GameObject("[SpecialAudience]");
                Undo.RegisterCreatedObjectUndo(go, "Create SpecialAudience");
            }

            var manager = go.GetComponent<SpecialAudienceManager>() ?? Undo.AddComponent<SpecialAudienceManager>(go);
            var view = Object.FindFirstObjectByType<SpecialAudienceView>() ?? BuildGreyboxView();

            SetObjectField(manager, "view", view);
            AssignSprites(view);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = go;
            Debug.Log("[SpecialAudience] 셋업 완료. Play 후 P 키로 즉시 등장을 확인하세요. (씬을 Ctrl+S 로 저장할 것)");
        }

        // ---------------- 스프라이트 연결 ----------------

        const string SpecialSpriteSheet = "Assets/Sprites/Crowd/SpecialCrowd/Special_Crowd.png";

        /// <summary>
        /// 슬라이스된 Special_Crowd 시트에서 스프라이트를 꺼내 Chill/Singalong/Mosh 순서로 넣는다.
        /// 순서가 아트 의도와 다르면 View 인스펙터에서 직접 바꾸면 된다.
        /// 이미 지정돼 있으면 덮어쓰지 않는다.
        /// </summary>
        [MenuItem("Tools/Special Audience/Reassign Special Audience Sprites", false, 20)]
        public static void ReassignSprites()
        {
            var view = Object.FindFirstObjectByType<SpecialAudienceView>();
            if (view == null)
            {
                Debug.LogWarning("[SpecialAudience] 씬에 SpecialAudienceView 가 없습니다. Setup 을 먼저 실행하세요.");
                return;
            }
            AssignSprites(view, overwrite: true);
        }

        static void AssignSprites(SpecialAudienceView view, bool overwrite = false)
        {
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpecialSpriteSheet)
                .OfType<Sprite>()
                .OrderBy(s => TrailingNumber(s.name))
                .ToList();

            if (sprites.Count == 0)
            {
                Debug.LogWarning($"[SpecialAudience] '{SpecialSpriteSheet}' 에서 스프라이트를 찾지 못했습니다. " +
                                 "Texture Type = Sprite, Sprite Mode = Multiple 로 슬라이스되어 있는지 확인하세요.");
                return;
            }

            var so = new SerializedObject(view);
            string[] fields = { "chillClip", "singalongClip", "moshClip" };
            int filled = 0;

            for (int i = 0; i < fields.Length && i < sprites.Count; i++)
            {
                // 클립의 frames[0] 에 넣는다. 나중에 아트가 프레임을 더 주면
                // 인스펙터에서 frames 크기만 늘리면 그대로 애니메이션이 된다.
                var frames = so.FindProperty(fields[i])?.FindPropertyRelative("frames");
                if (frames == null) continue;
                if (!overwrite && frames.arraySize > 0) continue; // 수동 지정 보호

                frames.arraySize = 1;
                frames.GetArrayElementAtIndex(0).objectReferenceValue = sprites[i];
                filled++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[SpecialAudience] 스프라이트 {filled}장 연결 " +
                      $"({string.Join(" / ", sprites.Take(3).Select(s => s.name))} → Chill / Singalong / Mosh)");
        }

        static int TrailingNumber(string name)
        {
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            return i < name.Length && int.TryParse(name.Substring(i), out int n) ? n : 0;
        }

        // ---------------- 그레이박스 UI ----------------

        static SpecialAudienceView BuildGreyboxView()
        {
            var canvasGo = GameObject.Find("SpecialAudienceCanvas");
            if (canvasGo == null)
            {
                canvasGo = new GameObject("SpecialAudienceCanvas");
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create SpecialAudienceCanvas");
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30; // 카드 캔버스(20)보다 위
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // 화면 좌상단 패널
            var rootGo = CreateUIObject("SpecialAudiencePanel", canvasGo.transform);
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(40f, -80f);
            rootRect.sizeDelta = new Vector2(360f, 150f);

            var bg = CreateStretchedImage("BG", rootGo.transform, new Color(0.1f, 0.1f, 0.15f, 0.85f));

            var title = CreateText("Title", rootGo.transform, "특별 관객", 24);
            PlaceTop(title.GetComponent<RectTransform>(), -8f, 34f);

            // 요구 타입 3종 (아트가 아이콘을 주기 전까지 텍스트로 대체)
            var chill = CreateLabelObject("ChillIcon", rootGo.transform, "CHILL", new Color(0.45f, 0.75f, 1f));
            var sing = CreateLabelObject("SingalongIcon", rootGo.transform, "SINGALONG", new Color(1f, 0.85f, 0.4f));
            var mosh = CreateLabelObject("MoshIcon", rootGo.transform, "MOSH", new Color(1f, 0.4f, 0.45f));

            // 남은 시간 바
            var timerBg = CreateUIObject("TimerBG", rootGo.transform);
            var timerBgRect = timerBg.GetComponent<RectTransform>();
            timerBgRect.anchorMin = new Vector2(0f, 0f);
            timerBgRect.anchorMax = new Vector2(1f, 0f);
            timerBgRect.pivot = new Vector2(0.5f, 0f);
            timerBgRect.offsetMin = new Vector2(12f, 12f);
            timerBgRect.offsetMax = new Vector2(-12f, 34f);
            var bgImage = timerBg.AddComponent<Image>();
            bgImage.color = new Color(0.25f, 0.25f, 0.25f);
            bgImage.raycastTarget = false;

            var fill = CreateStretchedImage("TimerFill", timerBg.transform, new Color(0.95f, 0.75f, 0.2f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            // 아트 스프라이트가 표시될 자리 (패널 왼쪽). 스프라이트가 없으면 자동으로 숨겨진다
            var iconGo = CreateUIObject("RequestIcon", rootGo.transform);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(12f, -4f);
            iconRect.sizeDelta = new Vector2(92f, 92f);
            var iconImage = iconGo.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            iconImage.enabled = false;

            var view = rootGo.AddComponent<SpecialAudienceView>();
            SetObjectField(view, "requestImage", iconImage);
            SetObjectField(view, "specialAudienceRoot", rootGo);
            SetObjectField(view, "chillIcon", chill);
            SetObjectField(view, "singalongIcon", sing);
            SetObjectField(view, "moshIcon", mosh);
            SetObjectField(view, "timerFillImage", fill);

            bg.raycastTarget = false;
            return view;
        }

        static GameObject CreateLabelObject(string name, Transform parent, string label, Color color)
        {
            var go = CreateUIObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            rect.sizeDelta = new Vector2(320f, 48f);

            var text = go.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;

            go.SetActive(false); // 매니저가 요구 타입에 맞는 것만 켠다
            return go;
        }

        // ---------------- 헬퍼 (HypeSetupMenu 와 동일한 방식) ----------------

        static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return go;
        }

        static Image CreateStretchedImage(string name, Transform parent, Color color)
        {
            var go = CreateUIObject(name, parent);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text CreateText(string name, Transform parent, string content, int size)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.9f, 0.9f);
            text.raycastTarget = false;
            return text;
        }

        static void PlaceTop(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, height);
        }

        static void SetObjectField(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SpecialAudience] {target.GetType().Name} 에서 '{fieldName}' 필드를 찾지 못했습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
