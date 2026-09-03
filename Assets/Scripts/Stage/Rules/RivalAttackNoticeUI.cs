using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 보스전 라이벌 공격 예고 문구. (기획서 §9 보스 UI 문구)
    ///
    /// 기존 CrisisCanvas 오버레이(긴급 상황!)는 그대로 두고, 그 위에 라이벌 이름·노리는 성향을
    /// 한 줄로 덧붙인다. 캔버스는 런타임에 스스로 만든다 (씬 배치 불필요, 매니저 참조 없음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RivalAttackNoticeUI : MonoBehaviour
    {
        [SerializeField, Tooltip("한글 폰트. 비우면 DialogueCatalog 스타일의 폰트를 빌려 쓴다")]
        TMP_FontAsset font;

        [SerializeField, Min(10f)] float fontSize = 30f;
        [SerializeField] Color warningColor = new Color32(0xF0, 0x4F, 0x78, 0xFF);
        [SerializeField] Color successColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField] Color failColor = new Color32(0xF5, 0x7D, 0x4A, 0xFF);
        [SerializeField, Min(0f)] float resultHoldDuration = 1.6f;
        [SerializeField, Tooltip("화면 위에서부터의 위치(px, 1080 기준)")]
        float topOffset = 150f;

        Canvas _canvas;
        TMP_Text _text;
        Coroutine _hideRoutine;

        void OnEnable()
        {
            EventBus.Subscribe<RivalAttackWarningStarted>(OnWarningStarted);
            EventBus.Subscribe<RivalAttackResolved>(OnResolved);
            EventBus.Subscribe<AudienceCrisisCancelled>(OnCancelled);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<RivalAttackWarningStarted>(OnWarningStarted);
            EventBus.Unsubscribe<RivalAttackResolved>(OnResolved);
            EventBus.Unsubscribe<AudienceCrisisCancelled>(OnCancelled);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            Hide();
        }

        void OnWarningStarted(RivalAttackWarningStarted e)
        {
            EnsureView();
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            string preference = PreferenceLabel(e.TargetPreference);
            _text.color = warningColor;
            _text.text =
                $"라이벌의 도발! {e.RivalName}이(가) {preference} 팬을 노립니다.\n" +
                $"{e.Duration:0}초 안에 표시된 {preference} 관객의 호응도를 {Mathf.CeilToInt(e.SafeEngagement)} 이상으로 올리거나 " +
                $"{preference} Special 카드를 성공시키세요.";
            _text.enabled = true;
        }

        void OnResolved(RivalAttackResolved e)
        {
            EnsureView();
            string preference = PreferenceLabel(e.TargetPreference);
            if (e.StolenCount == 0)
            {
                _text.color = successColor;
                _text.text = e.DefendedBySpecialHit
                    ? $"{preference} 팬을 Special 카드로 지켜냈습니다!"
                    : $"{preference} 팬이 우리 무대에 남았습니다!";
            }
            else
            {
                _text.color = failColor;
                _text.text = $"{preference} 관객 {e.StolenCount}명이 라이벌 무대로 떠났습니다.";
            }

            _text.enabled = true;
            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        void OnCancelled(AudienceCrisisCancelled e) => Hide();

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Hide();
        }

        IEnumerator HideAfterDelay()
        {
            if (resultHoldDuration > 0f) yield return new WaitForSeconds(resultHoldDuration);
            Hide();
            _hideRoutine = null;
        }

        void Hide()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }
            if (_text != null) _text.enabled = false;
        }

        static string PreferenceLabel(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return "CHILL";
                case CrowdPreference.Singalong: return "SINGALONG";
                case CrowdPreference.Mosh: return "MOSH";
                default: return preference.ToString().ToUpperInvariant();
            }
        }

        void EnsureView()
        {
            if (_canvas != null) return;

            var canvasObject = new GameObject(
                "RivalAttackNoticeCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 150;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textObject = new GameObject("Notice", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -topOffset);
            rect.sizeDelta = new Vector2(1300f, 110f);

            _text = textObject.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset resolvedFont = font;
            if (resolvedFont == null)
            {
                DialogueCatalog catalog = DialogueCatalog.LoadDefault();
                if (catalog != null && catalog.Style != null) resolvedFont = catalog.Style.Font;
            }
            if (resolvedFont != null) _text.font = resolvedFont;
            _text.fontSize = fontSize;
            _text.alignment = TextAlignmentOptions.Center;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.raycastTarget = false;
            _text.outlineWidth = 0.25f;
            _text.outlineColor = new Color32(0x16, 0x12, 0x1C, 0xFF);
            _text.enabled = false;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorSetFont(TMP_FontAsset fontAsset) => font = fontAsset;
#endif
    }
}
