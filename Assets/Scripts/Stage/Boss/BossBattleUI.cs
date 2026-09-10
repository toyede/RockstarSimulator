using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 보스 체력 바 + 패턴 예고. 상단 중앙, 기존 위기 경고와 같은 자리에서 런타임에 스스로 만든다.
    /// EventBus 만 구독한다 (룰 참조 없음). 예고 중에는 붉게 점멸하고 PEAK TIME 에는 색이 바뀐다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossBattleUI : MonoBehaviour
    {
        [SerializeField, Tooltip("한글 폰트. 비우면 DialogueCatalog 스타일 폰트")] TMP_FontAsset font;
        [SerializeField] string rivalName = "LUX//FAUNA";
        [SerializeField] Color healthColor = new Color32(0xF0, 0x4F, 0x78, 0xFF);
        [SerializeField] Color peakColor = new Color32(0xB0, 0x3C, 0xFF, 0xFF);
        [SerializeField] Color warningColor = new Color32(0xEA, 0x4F, 0x36, 0xFF);
        [SerializeField] Color successColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField, Min(0f)] float resultHoldDuration = 1.6f;
        [SerializeField, Min(0f), Tooltip("체력 바가 목표값을 따라가는 속도")] float fillLerpSpeed = 6f;
        [SerializeField, Tooltip("상단에서의 위치(px, 1080 기준)")] float topOffset = 96f;
        [SerializeField, Min(1), Tooltip("패턴 성공 점 개수 (= BossBattleConfig.patternsToClear)")] int successDots = 8;

        Canvas _canvas;
        Image _barBack;
        Image _barFill;
        TMP_Text _nameText;
        TMP_Text _percentText;
        TMP_Text _peakText;
        Image[] _streakDots;
        RectTransform _patternPanel;
        Image _patternBackground;
        TMP_Text _patternTitle;
        TMP_Text _patternBody;
        TMP_Text _patternProgress;
        TMP_Text _patternTimer;
        TMP_Text _bigText;
        TMP_Text _timeText;

        float _targetFill = 1f;
        bool _patternActive;
        bool _peak;
        Coroutine _hideRoutine;

        void OnEnable()
        {
            EventBus.Subscribe<BossHealthChanged>(OnHealth);
            EventBus.Subscribe<BossPatternAnnounced>(OnPatternAnnounced);
            EventBus.Subscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Subscribe<BossPatternProgress>(OnPatternProgress);
            EventBus.Subscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Subscribe<BossPeakTimeEntered>(OnPeakTime);
            EventBus.Subscribe<BossDefeated>(OnDefeated);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<BossHealthChanged>(OnHealth);
            EventBus.Unsubscribe<BossPatternAnnounced>(OnPatternAnnounced);
            EventBus.Unsubscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Unsubscribe<BossPatternProgress>(OnPatternProgress);
            EventBus.Unsubscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Unsubscribe<BossPeakTimeEntered>(OnPeakTime);
            EventBus.Unsubscribe<BossDefeated>(OnDefeated);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_barFill == null) return;

            _barFill.fillAmount = fillLerpSpeed <= 0f
                ? _targetFill
                : Mathf.MoveTowards(_barFill.fillAmount, _targetFill, fillLerpSpeed * Time.unscaledDeltaTime);

            // 남은 공연 시간 (기존 시간 HUD 는 보스전 동안 숨긴다)
            if (_timeText != null)
            {
                float remaining = Mathf.Max(0f, PerformanceTimer.Duration - PerformanceTimer.Elapsed);
                int total = Mathf.CeilToInt(remaining);
                _timeText.text = $"{total / 60}:{total % 60:00}";
                _timeText.color = remaining <= 15f ? warningColor : new Color(1f, 1f, 1f, 0.85f);
            }

            if (_patternActive && _patternBackground != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                Color color = Color.Lerp(new Color(0.08f, 0.05f, 0.1f, 0.92f), warningColor * 0.7f, pulse * 0.6f);
                color.a = 0.92f;
                _patternBackground.color = color;
                _barBack.color = Color.Lerp(new Color(0.1f, 0.08f, 0.12f, 0.95f), warningColor, pulse * 0.35f);
            }
        }

        // ---------------- 이벤트 ----------------

        void OnHealth(BossHealthChanged e)
        {
            EnsureView();
            _canvas.gameObject.SetActive(true);
            _targetFill = e.Normalized;
            _percentText.text = $"{Mathf.CeilToInt(e.Normalized * 100f)}%";
            _peak = e.PeakTime;
            _barFill.color = _peak ? peakColor : healthColor;
            _peakText.gameObject.SetActive(_peak);
        }

        void OnPeakTime(BossPeakTimeEntered e)
        {
            EnsureView();
            _peak = true;
            _barFill.color = peakColor;
            _peakText.gameObject.SetActive(true);
            ShowBig("PEAK TIME", peakColor, 1.4f);
        }

        void OnPatternAnnounced(BossPatternAnnounced e)
        {
            EnsureView();
            StopHide();
            _patternPanel.gameObject.SetActive(false);
            string title = e.Enhanced ? $"{e.Title}  ▲" : e.Title;
            ShowBig(title, _peak ? peakColor : warningColor, Mathf.Max(0.5f, e.DisplaySeconds));
        }

        void OnPatternStarted(BossPatternStarted e)
        {
            EnsureView();
            StopHide();
            if (_bigText != null) _bigText.gameObject.SetActive(false);
            _patternActive = true;
            _patternPanel.gameObject.SetActive(true);
            _patternTitle.text = e.Enhanced ? $"{e.Title}  ▲" : e.Title;
            _patternTitle.color = _peak ? peakColor : warningColor;
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
            _barBack.color = new Color(0.1f, 0.08f, 0.12f, 0.95f);
            _patternBackground.color = new Color(0.08f, 0.05f, 0.1f, 0.92f);

            if (e.Success)
            {
                _patternTitle.text = $"{e.Title}  성공!";
                _patternTitle.color = successColor;
                _patternBody.text = e.FansMoved > 0
                    ? $"상대 팬 {e.FansMoved}명 합류 · 패턴 성공 {e.Streak}/{successDots}"
                    : $"만석! 패턴 성공 {e.Streak}/{successDots}";
            }
            else
            {
                _patternTitle.text = $"{e.Title}  실패";
                _patternTitle.color = warningColor;
                _patternBody.text = e.FansMoved > 0
                    ? $"우리 팬 {e.FansMoved}명이 라이벌 무대로… 보스 회복"
                    : "보스 회복";
            }
            _patternProgress.text = "";
            _patternTimer.text = "";
            UpdateStreak(e.Streak);

            StopHide();
            _hideRoutine = StartCoroutine(HidePatternAfter(resultHoldDuration));
        }

        void OnDefeated(BossDefeated e)
        {
            EnsureView();
            _patternActive = false;
            _patternPanel.gameObject.SetActive(false);
            _targetFill = 0f;
            _percentText.text = "0%";
            ShowBig("라이벌 격파! 앙코르 무대 확보!", successColor, 4f);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 공연이 끝나면(결과창이 뜨기 전) 체력 바를 치운다. 격파 자막은 종료 전 연출 중에 이미 보여줬다
            if (e.Current == GameState.GameOver)
            {
                _patternActive = false;
                if (_canvas != null) _canvas.gameObject.SetActive(false);
                return;
            }
            if (e.Current != GameState.Ready) return;
            _patternActive = false;
            _targetFill = 1f;
            _peak = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        // ---------------- 표시 ----------------

        void UpdateStreak(int streak)
        {
            if (_streakDots == null) return;
            for (int i = 0; i < _streakDots.Length; i++)
                _streakDots[i].color = i < streak ? successColor : new Color(1f, 1f, 1f, 0.25f);
        }

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

            // 체력 바
            RectTransform bar = CreateRect(canvasObject.transform, "HealthBar", new Vector2(0.5f, 1f), new Vector2(0f, -topOffset), new Vector2(900f, 30f));
            _barBack = bar.gameObject.AddComponent<Image>();
            _barBack.color = new Color(0.1f, 0.08f, 0.12f, 0.95f);
            _barBack.raycastTarget = false;

            var fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(bar, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            _barFill = fillObject.GetComponent<Image>();
            _barFill.sprite = CreateSolidSprite();
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillOrigin = 0;
            _barFill.fillAmount = 1f;
            _barFill.color = healthColor;
            _barFill.raycastTarget = false;

            _nameText = CreateText(bar, "Name", resolvedFont, 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 6f), new Vector2(500f, 30f));
            _nameText.text = rivalName;

            _percentText = CreateText(bar, "Percent", resolvedFont, 22f, TextAlignmentOptions.MidlineRight, Color.white);
            SetRect(_percentText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(200f, 30f));
            _percentText.text = "100%";

            _peakText = CreateText(bar, "Peak", resolvedFont, 20f, TextAlignmentOptions.Center, peakColor);
            SetRect(_peakText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(300f, 30f));
            _peakText.text = "PEAK TIME";
            _peakText.gameObject.SetActive(false);

            _timeText = CreateText(bar, "Time", resolvedFont, 22f, TextAlignmentOptions.MidlineRight, new Color(1f, 1f, 1f, 0.85f));
            SetRect(_timeText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, -6f), new Vector2(160f, 28f));
            _timeText.text = "";

            // 패턴 성공 점 (patternsToClear 개, 가운데 정렬)
            _streakDots = new Image[Mathf.Max(1, successDots)];
            float dotsStart = -(_streakDots.Length - 1) * 13f;
            for (int i = 0; i < _streakDots.Length; i++)
            {
                var dot = new GameObject($"Streak_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var dotRect = dot.GetComponent<RectTransform>();
                dotRect.SetParent(bar, false);
                SetRect(dotRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(dotsStart + i * 26f, -6f), new Vector2(16f, 16f));
                _streakDots[i] = dot.GetComponent<Image>();
                _streakDots[i].color = new Color(1f, 1f, 1f, 0.25f);
                _streakDots[i].raycastTarget = false;
            }

            // 패턴 패널
            _patternPanel = CreateRect(canvasObject.transform, "Pattern", new Vector2(0.5f, 1f), new Vector2(0f, -topOffset - 78f), new Vector2(1000f, 118f));
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

            // 큰 자막 (PEAK TIME · 격파)
            _bigText = CreateText(canvasObject.transform, "Big", resolvedFont, 56f, TextAlignmentOptions.Center, successColor);
            SetRect(_bigText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1400f, 90f));
            _bigText.outlineWidth = 0.25f;
            _bigText.outlineColor = new Color32(0x16, 0x12, 0x1C, 0xFF);
            _bigText.gameObject.SetActive(false);
        }

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
        public void EditorConfigure(TMP_FontAsset fontAsset, string name, int dots = 8)
        {
            font = fontAsset;
            if (!string.IsNullOrEmpty(name)) rivalName = name;
            successDots = Mathf.Max(1, dots);
        }
#endif
    }
}
