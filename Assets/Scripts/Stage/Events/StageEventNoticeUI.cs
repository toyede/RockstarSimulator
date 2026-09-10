using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 기믹 이벤트 알림 (제목 · 지시문 · 남은 시간 · 진행도). 상단 중앙, 기존 위기 경고와 같은 자리.
    /// EventBus 만 구독하고 룰은 모른다. 런타임에 스스로 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageEventNoticeUI : MonoBehaviour
    {
        [SerializeField, Tooltip("한글 폰트. 비우면 DialogueCatalog 스타일 폰트")] TMP_FontAsset font;
        [SerializeField] Color titleColor = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
        [SerializeField] Color successColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField] Color failColor = new Color32(0xF5, 0x7D, 0x4A, 0xFF);
        [SerializeField, Min(0f)] float resultHoldDuration = 1.8f;
        [SerializeField, Tooltip("상단에서의 위치(px, 1080 기준). 점수 HUD 아래, 관객 머리 위")] float topOffset = 92f;

        Canvas _canvas;
        RectTransform _panel;
        Image _background;
        TMP_Text _title;
        TMP_Text _body;
        TMP_Text _timer;
        Coroutine _hideRoutine;
        bool _active;

        void OnEnable()
        {
            EventBus.Subscribe<StageEventStarted>(OnStarted);
            EventBus.Subscribe<StageEventProgress>(OnProgress);
            EventBus.Subscribe<StageEventResolved>(OnResolved);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<StageEventStarted>(OnStarted);
            EventBus.Unsubscribe<StageEventProgress>(OnProgress);
            EventBus.Unsubscribe<StageEventResolved>(OnResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            Hide();
        }

        void Update()
        {
            if (!_active || _background == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            _background.color = Color.Lerp(new Color(0.08f, 0.05f, 0.1f, 0.9f), titleColor * 0.5f, pulse * 0.35f);
        }

        void OnStarted(StageEventStarted e)
        {
            EnsureView();
            StopHide();
            _active = true;
            _panel.gameObject.SetActive(true);
            _title.text = e.Title;
            _title.color = titleColor;
            _body.text = e.Instruction;
            _timer.text = $"{e.Duration:0.0}s";
        }

        void OnProgress(StageEventProgress e)
        {
            if (_timer == null) return;
            _timer.text = string.IsNullOrEmpty(e.ProgressText) ? $"{e.Remaining:0.0}s" : $"{e.ProgressText}   {e.Remaining:0.0}s";
        }

        void OnResolved(StageEventResolved e)
        {
            EnsureView();
            _active = false;
            _background.color = new Color(0.08f, 0.05f, 0.1f, 0.9f);
            _panel.gameObject.SetActive(true);
            _title.text = e.Success ? $"{e.Title}  성공!" : $"{e.Title}  실패";
            _title.color = e.Success ? successColor : failColor;
            _body.text = e.ResultText;
            _timer.text = "";
            StopHide();
            _hideRoutine = StartCoroutine(HideAfter(resultHoldDuration));
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Hide();
        }

        IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Hide();
        }

        void StopHide()
        {
            if (_hideRoutine == null) return;
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        void Hide()
        {
            StopHide();
            _active = false;
            if (_panel != null) _panel.gameObject.SetActive(false);
        }

        void EnsureView()
        {
            if (_canvas != null) return;

            TMP_FontAsset resolvedFont = font;
            if (resolvedFont == null)
            {
                DialogueCatalog catalog = DialogueCatalog.LoadDefault();
                if (catalog != null && catalog.Style != null) resolvedFont = catalog.Style.Font;
            }

            var canvasObject = new GameObject("StageEventNoticeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 152;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panel = panelObject.GetComponent<RectTransform>();
            _panel.SetParent(canvasObject.transform, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 1f);
            _panel.pivot = new Vector2(0.5f, 1f);
            _panel.anchoredPosition = new Vector2(0f, -topOffset);
            _panel.sizeDelta = new Vector2(1000f, 84f);
            _background = panelObject.GetComponent<Image>();
            _background.color = new Color(0.08f, 0.05f, 0.1f, 0.9f);
            _background.raycastTarget = false;

            _title = CreateText(_panel, "Title", resolvedFont, 26f, TextAlignmentOptions.MidlineLeft, titleColor);
            SetRect(_title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-240f, 34f));
            _timer = CreateText(_panel, "Timer", resolvedFont, 24f, TextAlignmentOptions.MidlineRight, Color.white);
            SetRect(_timer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -6f), new Vector2(420f, 34f));
            _body = CreateText(_panel, "Body", resolvedFont, 22f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(-40f, 36f));

            _panel.gameObject.SetActive(false);
        }

        static TMP_Text CreateText(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.outlineWidth = 0.15f;
            text.outlineColor = new Color32(0x16, 0x12, 0x1C, 0xFF);
            return text;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorSetFont(TMP_FontAsset fontAsset) => font = fontAsset;
#endif
    }
}
