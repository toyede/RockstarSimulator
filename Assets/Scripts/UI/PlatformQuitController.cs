using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>WebGL에서 지원되지 않는 Application.Quit 대신 명확한 종료 안내를 보여준다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlatformQuitController : MonoBehaviour
    {
        GameObject _webExitOverlay;

        public void QuitGame()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ShowWebExitOverlay();
#else
            if (UIManager.HasInstance) UIManager.Instance.QuitGame();
            else SceneLoader.Quit();
#endif
        }

        [ContextMenu("Debug/Show Web Exit Overlay")]
        public void ShowWebExitOverlay()
        {
            EnsureOverlay();
            _webExitOverlay.SetActive(true);
        }

        void EnsureOverlay()
        {
            if (_webExitOverlay != null) return;

            Font font = FindProjectFont();
            var canvasObject = new GameObject(
                "WebExitCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _webExitOverlay = CreateImage(
                "WebExitOverlay",
                canvasObject.transform,
                new Color(0f, 0f, 0f, 0.88f));
            Stretch((RectTransform)_webExitOverlay.transform);

            GameObject panel = CreateImage(
                "WebExitPanel",
                _webExitOverlay.transform,
                new Color32(0x2E, 0x22, 0x2F, 0xFA));
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 330f);

            CreateText(
                "Title",
                panel.transform,
                font,
                "플레이해주셔서 감사합니다!",
                42,
                new Vector2(0f, 78f),
                new Vector2(640f, 70f),
                new Color32(0xF9, 0xC2, 0x2B, 0xFF));
            CreateText(
                "Description",
                panel.transform,
                font,
                "WebGL에서는 브라우저를 직접 종료할 수 없습니다.\n브라우저 탭을 닫거나 타이틀로 돌아가세요.",
                27,
                new Vector2(0f, 2f),
                new Vector2(650f, 100f),
                Color.white);

            GameObject buttonObject = CreateImage(
                "ReturnButton",
                panel.transform,
                new Color32(0xA2, 0x4B, 0x6F, 0xFF));
            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -103f);
            buttonRect.sizeDelta = new Vector2(230f, 66f);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(() => _webExitOverlay.SetActive(false));
            CreateText(
                "Label",
                buttonObject.transform,
                font,
                "타이틀로 돌아가기",
                25,
                Vector2.zero,
                new Vector2(210f, 54f),
                Color.white);

            _webExitOverlay.SetActive(false);
        }

        static Font FindProjectFont()
        {
            Text[] texts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] != null && texts[i].font != null)
                    return texts[i].font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static GameObject CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return gameObject;
        }

        static void CreateText(
            string name,
            Transform parent,
            Font font,
            string value,
            int size,
            Vector2 position,
            Vector2 dimensions,
            Color color)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            var text = gameObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(16, size - 8);
            text.resizeTextMaxSize = size;
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
