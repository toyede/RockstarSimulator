using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 보스전 HUD: 상단 팬 쟁탈 바(라이벌 | 우리) + 드레인 카운트다운·감소 팝업 + 패턴 예고/진행 + REVENGE TIME! 자막.
    /// 런타임에 스스로 만들고 EventBus 만 구독한다 (룰 참조 없음). 랭크·점수·시간 HUD 는 그대로 보인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossBattleUI : MonoBehaviour
    {
        [SerializeField, Tooltip("한글 폰트. 비우면 DialogueCatalog 스타일 폰트")] TMP_FontAsset font;
        [SerializeField] string rivalName = "LUX//FAUNA";
        [SerializeField] string ourName = "RACCOON ROLL";
        [SerializeField] Color rivalColor = new Color32(0xF0, 0x4F, 0x78, 0xFF);
        [SerializeField] Color ourColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField] Color revengeColor = new Color32(0xB0, 0x3C, 0xFF, 0xFF);
        [SerializeField] Color warningColor = new Color32(0xEA, 0x4F, 0x36, 0xFF);
        [SerializeField] Color successColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField, Min(0f)] float resultHoldDuration = 1.6f;
        [SerializeField, Min(0f), Tooltip("바가 목표값을 따라가는 속도")] float fillLerpSpeed = 6f;
        [SerializeField, Tooltip("상단에서의 위치(px, 1080 기준). 점수·랭크 HUD 아래")] float topOffset = 118f;

        Canvas _canvas;
        Image _barBack;
        Image _rivalFill;
        TMP_Text _rivalText;
        TMP_Text _ourText;
        TMP_Text _revengeTag;
        TMP_Text _drainText;
        TMP_Text _drainPopup;
        RectTransform _patternPanel;
        Image _patternBackground;
        TMP_Text _patternTitle;
        TMP_Text _patternBody;
        TMP_Text _patternProgress;
        TMP_Text _patternTimer;
        TMP_Text _bigText;

        float _targetRivalRatio = 1f;
        bool _patternActive;
        bool _revenge;
        Coroutine _hideRoutine;
        Coroutine _popupRoutine;

        void OnEnable()
        {
            EventBus.Subscribe<BossFanBalanceChanged>(OnBalance);
            EventBus.Subscribe<BossDrainCountdown>(OnDrainCountdown);
            EventBus.Subscribe<BossDrainApplied>(OnDrainApplied);
            EventBus.Subscribe<BossRevengeChanged>(OnRevenge);
            EventBus.Subscribe<BossPatternAnnounced>(OnPatternAnnounced);
            EventBus.Subscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Subscribe<BossPatternProgress>(OnPatternProgress);
            EventBus.Subscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<BossFanBalanceChanged>(OnBalance);
            EventBus.Unsubscribe<BossDrainCountdown>(OnDrainCountdown);
            EventBus.Unsubscribe<BossDrainApplied>(OnDrainApplied);
            EventBus.Unsubscribe<BossRevengeChanged>(OnRevenge);
            EventBus.Unsubscribe<BossPatternAnnounced>(OnPatternAnnounced);
            EventBus.Unsubscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Unsubscribe<BossPatternProgress>(OnPatternProgress);
            EventBus.Unsubscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_rivalFill == null) return;

            _rivalFill.fillAmount = fillLerpSpeed <= 0f
                ? _targetRivalRatio
                : Mathf.MoveTowards(_rivalFill.fillAmount, _targetRivalRatio, fillLerpSpeed * Time.unscaledDeltaTime);

            if (_patternActive && _patternBackground != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                if (_panelStyledFromTutorial)
                {
                    // 튜토리얼 패널 모양을 빌린 경우 배경은 그대로 두고 제목만 맥동
                    _patternTitle.color = Color.Lerp(_revenge ? revengeColor : warningColor, Color.white, pulse * 0.35f);
                }
                else
                {
                    Color color = Color.Lerp(new Color(0.08f, 0.05f, 0.1f, 0.92f), warningColor * 0.7f, pulse * 0.6f);
                    color.a = 0.92f;
                    _patternBackground.color = color;
                }
            }
        }

        // ---------------- 이벤트 ----------------

        void OnBalance(BossFanBalanceChanged e)
        {
            EnsureView();
            _canvas.gameObject.SetActive(true);
            _targetRivalRatio = e.RivalRatio;
            _rivalText.text = $"{rivalName}  {e.Rival}";
            _ourText.text = $"{e.Ours}  {ourName}";
            _revenge = e.Revenge;
            _rivalFill.color = _revenge ? revengeColor : rivalColor;
            _revengeTag.gameObject.SetActive(_revenge);
        }

        void OnDrainCountdown(BossDrainCountdown e)
        {
            if (_drainText == null) return;
            _drainText.text = e.Active
                ? $"다음 감소  −{e.Amount:N0}  {e.Remaining:0.0}s"
                : "라이벌 팬 없음 · 감소 정지";
            _drainText.color = e.Active && e.Remaining < 1f ? warningColor : new Color(1f, 1f, 1f, 0.85f);
        }

        void OnDrainApplied(BossDrainApplied e)
        {
            EnsureView();
            if (e.Amount <= 0) return;
            _drainPopup.text = $"−{e.Amount:N0}";
            _drainPopup.gameObject.SetActive(true);
            if (_popupRoutine != null) StopCoroutine(_popupRoutine);
            _popupRoutine = StartCoroutine(DrainPopup());
        }

        IEnumerator DrainPopup()
        {
            RectTransform rect = _drainPopup.rectTransform;
            Vector2 basePos = new Vector2(0f, -topOffset - 60f);
            float elapsed = 0f;
            const float duration = 0.8f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = basePos + new Vector2(0f, -30f * t);
                _drainPopup.color = new Color(warningColor.r, warningColor.g, warningColor.b, 1f - t * t);
                yield return null;
            }
            _drainPopup.gameObject.SetActive(false);
            _popupRoutine = null;
        }

        void OnRevenge(BossRevengeChanged e)
        {
            EnsureView();
            _revenge = e.Active;
            _revengeTag.gameObject.SetActive(_revenge);
            _rivalFill.color = _revenge ? revengeColor : rivalColor;
            if (e.Active) ShowBig("REVENGE TIME!", revengeColor, 1.6f);
        }

        void OnPatternAnnounced(BossPatternAnnounced e)
        {
            EnsureView();
            StopHide();
            _patternPanel.gameObject.SetActive(false);
            string title = e.Enhanced ? $"{e.Title}  ▲" : e.Title;
            ShowBig(title, _revenge ? revengeColor : warningColor, Mathf.Max(0.5f, e.DisplaySeconds));
        }

        void OnPatternStarted(BossPatternStarted e)
        {
            EnsureView();
            StopHide();
            if (_bigText != null) _bigText.gameObject.SetActive(false);
            _patternActive = true;
            _patternPanel.gameObject.SetActive(true);
            _patternTitle.text = e.Enhanced ? $"{e.Title}  ▲" : e.Title;
            _patternTitle.color = _revenge ? revengeColor : warningColor;
            _patternBody.text = e.Instruction;
            _patternProgress.text = "";
            _patternTimer.text = $"{e.Duration:0.0}s";
        }

        void OnPatternProgress(BossPatternProgress e)
        {
            if (_patternProgress == null) return;
            _patternProgress.text = e.ProgressText;
            _patternTimer.text = $"{e.Remaining:0.0}s";
            _patternProgress.color = e.Achieved ? successColor : Color.white;
        }

        void OnPatternResolved(BossPatternResolved e)
        {
            EnsureView();
            _patternActive = false;
            if (!_panelStyledFromTutorial) _patternBackground.color = new Color(0.08f, 0.05f, 0.1f, 0.92f);

            if (e.Success)
            {
                _patternTitle.text = $"{e.Title}  성공!";
                _patternTitle.color = successColor;
                _patternBody.text = e.FansMoved > 0
                    ? $"라이벌 팬 {e.FansMoved}명이 우리 무대로 · 보너스 +{e.BonusScore:N0}"
                    : $"만석! 보너스 +{e.BonusScore:N0}";
            }
            else
            {
                _patternTitle.text = $"{e.Title}  실패";
                _patternTitle.color = warningColor;
                _patternBody.text = e.FansMoved > 0
                    ? $"우리 팬 {e.FansMoved}명이 라이벌 무대로… 감소가 커집니다"
                    : "기회를 놓쳤습니다";
            }
            _patternProgress.text = "";
            _patternTimer.text = "";

            StopHide();
            _hideRoutine = StartCoroutine(HidePatternAfter(resultHoldDuration));
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current != GameState.GameOver && e.Current != GameState.Ready) return;
            _patternActive = false;
            _targetRivalRatio = 1f;
            _revenge = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        // ---------------- 표시 ----------------

        void ShowBig(string text, Color color, float hold)
        {
            _bigText.text = text;
            _bigText.color = color;
            _bigText.gameObject.SetActive(true);
            StartCoroutine(HideBigAfter(hold));
        }

        IEnumerator HideBigAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_bigText != null) _bigText.gameObject.SetActive(false);
        }

        IEnumerator HidePatternAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_patternPanel != null) _patternPanel.gameObject.SetActive(false);
            _hideRoutine = null;
        }

        void StopHide()
        {
            if (_hideRoutine == null) return;
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
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

            var canvasObject = new GameObject("BossBattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 155;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 팬 쟁탈 바: 배경(우리 색) 위에 라이벌 색이 왼쪽에서 차오른다
            RectTransform bar = CreateRect(canvasObject.transform, "FanBar", new Vector2(0.5f, 1f), new Vector2(0f, -topOffset), new Vector2(900f, 26f));
            _barBack = bar.gameObject.AddComponent<Image>();
            _barBack.sprite = CreateSolidSprite();
            _barBack.color = ourColor;
            _barBack.raycastTarget = false;

            var fillObject = new GameObject("RivalFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(bar, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            _rivalFill = fillObject.GetComponent<Image>();
            _rivalFill.sprite = CreateSolidSprite();
            _rivalFill.type = Image.Type.Filled;
            _rivalFill.fillMethod = Image.FillMethod.Horizontal;
            _rivalFill.fillOrigin = 0;
            _rivalFill.fillAmount = 1f;
            _rivalFill.color = rivalColor;
            _rivalFill.raycastTarget = false;

            _rivalText = CreateText(bar, "Rival", resolvedFont, 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_rivalText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 6f), new Vector2(420f, 30f));
            _ourText = CreateText(bar, "Ours", resolvedFont, 24f, TextAlignmentOptions.MidlineRight, Color.white);
            SetRect(_ourText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(420f, 30f));
            _revengeTag = CreateText(bar, "Revenge", resolvedFont, 20f, TextAlignmentOptions.Center, revengeColor);
            SetRect(_revengeTag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(300f, 30f));
            _revengeTag.text = "REVENGE TIME!";
            _revengeTag.gameObject.SetActive(false);

            _drainText = CreateText(bar, "Drain", resolvedFont, 20f, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.85f));
            SetRect(_drainText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(600f, 28f));
            _drainText.text = "";

            _drainPopup = CreateText(canvasObject.transform, "DrainPopup", resolvedFont, 44f, TextAlignmentOptions.Center, warningColor);
            SetRect(_drainPopup.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -topOffset - 60f), new Vector2(400f, 60f));
            _drainPopup.fontStyle = FontStyles.Bold;
            _drainPopup.gameObject.SetActive(false);

            // 패턴 패널
            _patternPanel = CreateRect(canvasObject.transform, "Pattern", new Vector2(0.5f, 1f), new Vector2(0f, -topOffset - 96f), new Vector2(1000f, 118f));
            _patternBackground = _patternPanel.gameObject.AddComponent<Image>();
            _patternBackground.color = new Color(0.08f, 0.05f, 0.1f, 0.92f);
            _patternBackground.raycastTarget = false;

            _patternTitle = CreateText(_patternPanel, "Title", resolvedFont, 32f, TextAlignmentOptions.MidlineLeft, warningColor);
            SetRect(_patternTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-40f, 40f));
            _patternTimer = CreateText(_patternPanel, "Timer", resolvedFont, 28f, TextAlignmentOptions.MidlineRight, Color.white);
            SetRect(_patternTimer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -8f), new Vector2(200f, 40f));
            _patternBody = CreateText(_patternPanel, "Body", resolvedFont, 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_patternBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(-40f, 34f));
            _patternProgress = CreateText(_patternPanel, "Progress", resolvedFont, 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_patternProgress.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(-40f, 30f));
            _patternPanel.gameObject.SetActive(false);
            ApplyTutorialPanelStyle();

            // 큰 자막 (예고 · REVENGE)
            _bigText = CreateText(canvasObject.transform, "Big", resolvedFont, 56f, TextAlignmentOptions.Center, successColor);
            SetRect(_bigText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1400f, 90f));
            _bigText.outlineWidth = 0.25f;
            _bigText.outlineColor = new Color32(0x16, 0x12, 0x1C, 0xFF);
            _bigText.gameObject.SetActive(false);
        }

        /// <summary>패턴 경고 패널을 튜토리얼 메시지 패널과 같은 모양(배경 스프라이트·색·테두리·글자 크기)으로 맞춘다.</summary>
        void ApplyTutorialPanelStyle()
        {
            TutorialOverlayUI tutorial = FindFirstObjectByType<TutorialOverlayUI>(FindObjectsInactive.Include);
            Image source = tutorial != null ? tutorial.PanelImage : null;
            if (source == null) return;

            _patternBackground.sprite = source.sprite;
            _patternBackground.type = source.type;
            _patternBackground.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            _patternBackground.color = source.color;
            _panelStyledFromTutorial = true;

            Outline outline = source.GetComponent<Outline>();
            if (outline != null)
            {
                Outline copy = _patternPanel.gameObject.AddComponent<Outline>();
                copy.effectColor = outline.effectColor;
                copy.effectDistance = outline.effectDistance;
                copy.useGraphicAlpha = outline.useGraphicAlpha;
            }

            Shadow shadow = source.GetComponent<Shadow>();
            if (shadow != null && !(shadow is Outline))
            {
                Shadow copy = _patternPanel.gameObject.AddComponent<Shadow>();
                copy.effectColor = shadow.effectColor;
                copy.effectDistance = shadow.effectDistance;
            }

            _patternTitle.fontSize = tutorial.TitleFontSize;
            _patternBody.fontSize = tutorial.BodyFontSize;
            _patternProgress.fontSize = tutorial.BodyFontSize;
            _patternTimer.fontSize = tutorial.BodyFontSize + 2;
        }

        bool _panelStyledFromTutorial;

        static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, anchor, anchor, new Vector2(0.5f, 1f), position, size);
            return rect;
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

        static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(TMP_FontAsset fontAsset, string name, int unused = 0)
        {
            font = fontAsset;
            if (!string.IsNullOrEmpty(name)) rivalName = name;
        }
#endif
    }
}
