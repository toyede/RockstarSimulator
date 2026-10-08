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
        TMP_Text _progress;
        MissionCueSheetView _view;
        string _eventId;
        Coroutine _hideRoutine;
        bool _active;

        void OnEnable()
        {
            EventBus.Subscribe<StageEventStarted>(OnStarted);
            EventBus.Subscribe<StageEventProgress>(OnProgress);
            EventBus.Subscribe<StageEventResolved>(OnResolved);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<StageRuntimeApplied>(OnStageApplied);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<StageEventStarted>(OnStarted);
            EventBus.Unsubscribe<StageEventProgress>(OnProgress);
            EventBus.Unsubscribe<StageEventResolved>(OnResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<StageRuntimeApplied>(OnStageApplied);
            Hide();
        }

        void Update()
        {
            if (!_active || _background == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            _title.color = Color.Lerp(titleColor, Color.white, pulse * 0.18f);
        }

        void OnStarted(StageEventStarted e)
        {
            if (BossBattleUI.HandlesStageEvent(e.EventId)) return;
            EnsureView();
            StopHide();
            _eventId = e.EventId;
            _active = true;
            _panel.gameObject.SetActive(true);
            _title.text = e.Title;
            _title.color = titleColor;
            _body.text = e.Instruction;
            _timer.text = $"{e.Duration:0.0}s";
            _progress.text = "";
            _view.SetSecondary("");
        }

        void OnProgress(StageEventProgress e)
        {
            if (BossBattleUI.HandlesStageEvent(e.EventId) || !_active || _eventId != e.EventId || _timer == null) return;
            _timer.text = $"{e.Remaining:0.0}s";
            _progress.text = e.ProgressText;
            _view.SetSecondary("");
        }

        void OnResolved(StageEventResolved e)
        {
            if (BossBattleUI.HandlesStageEvent(e.EventId) || !_active || _eventId != e.EventId) return;
            _active = false;
            _panel.gameObject.SetActive(true);
            _title.text = e.Success ? $"{e.Title}  성공!" : $"{e.Title}  실패";
            _title.color = e.Success ? successColor : failColor;
            _body.text = e.ResultText;
            _timer.text = "";
            _progress.text = "";
            _view.SetSecondary("");
            StopHide();
            _hideRoutine = StartCoroutine(HideAfter(resultHoldDuration));
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Hide();
            else if (_canvas != null) _canvas.gameObject.SetActive(e.Current != GameState.Paused);
        }

        void OnStageApplied(StageRuntimeApplied e) => Hide();

        IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
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
            _eventId = null;
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
            _panel.sizeDelta = new Vector2(1000f, 256f);
            _background = panelObject.GetComponent<Image>();
            var view = _view = MissionCueSheetView.Create(_panel, resolvedFont);
            _title = view.Title; _body = view.Body; _timer = view.Timer; _progress = view.Progress;
            view.SetSecondary("");
            _title.fontSize = _title.fontSizeMax = 26f;
            _body.fontSize = _body.fontSizeMax = 24f;
            _title.fontSizeMin = 24f;
            // 일반 무대의 기존 상단 중앙 위치는 보존한다. 지시문에는 종이 면적을 넓게 준다.
            _body.rectTransform.anchorMin = new Vector2(.06f, .27f);
            _progress.rectTransform.anchorMin = new Vector2(.06f, .10f);
            _progress.rectTransform.anchorMax = new Vector2(.94f, .25f);

            _panel.gameObject.SetActive(false);
        }


#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorSetFont(TMP_FontAsset fontAsset) => font = fontAsset;
#endif
    }
}
