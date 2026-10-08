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
        [SerializeField, Tooltip("미션 패널 위치 — 왼쪽 중단 기준(px, 1920×1080)")] Vector2 patternPanelPosition = new Vector2(32f, 0f);
        [SerializeField, Tooltip("미션 패널 크기(px)")] Vector2 patternPanelSize = new Vector2(512f, 384f);

        Canvas _canvas;
        Image _barBack;
        Image _rivalFill;
        TMP_Text _rivalText;
        TMP_Text _ourText;
        TMP_Text _revengeTag;
        TMP_Text _drainText;
        TMP_Text _drainPopup;
        RectTransform _patternPanel;
        TMP_Text _patternTitle;
        TMP_Text _patternBody;
        TMP_Text _patternProgress;
        TMP_Text _patternTimer;
        TMP_Text _bigText;
        MissionCueSheetView _missionView;

        const string StandingFanEventId = "stadium_contested_fan";
        // 표시는 독립적인 두 채널로 보관한다. 보상/실패 판정에는 관여하지 않는다.
        sealed class MissionDisplay
        {
            public string Id, Title, Body, Progress, Timer;
            public bool Active;
            public bool Achieved;
            public float ResultRemaining;
            public Color Color;
            public bool Visible => Active || ResultRemaining > 0f;
            public void Clear() { Id = null; Active = false; ResultRemaining = 0f; }
        }
        readonly MissionDisplay _pattern = new MissionDisplay();
        readonly MissionDisplay _standing = new MissionDisplay();
        bool _paused;
        int _bigVersion;

        public static bool HandlesStageEvent(string eventId) => eventId == StandingFanEventId &&
            StageRuntimeDirector.CurrentStage != null && StageRuntimeDirector.CurrentStage.IsBoss;

        float _targetRivalRatio = 1f;
        bool _revenge;
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
            EventBus.Subscribe<StageEventStarted>(OnStandingStarted);
            EventBus.Subscribe<StageEventProgress>(OnStandingProgress);
            EventBus.Subscribe<StageEventResolved>(OnStandingResolved);
            EventBus.Subscribe<StageRuntimeApplied>(OnStageApplied);
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
            EventBus.Unsubscribe<StageEventStarted>(OnStandingStarted);
            EventBus.Unsubscribe<StageEventProgress>(OnStandingProgress);
            EventBus.Unsubscribe<StageEventResolved>(OnStandingResolved);
            EventBus.Unsubscribe<StageRuntimeApplied>(OnStageApplied);
            ResetView();
        }

        void Update()
        {
            if (_rivalFill == null) return;

            _rivalFill.fillAmount = fillLerpSpeed <= 0f
                ? _targetRivalRatio
                : Mathf.MoveTowards(_rivalFill.fillAmount, _targetRivalRatio, fillLerpSpeed * Time.unscaledDeltaTime);

            if (!_paused)
            {
                if (_pattern.ResultRemaining > 0f || _standing.ResultRemaining > 0f)
                {
                    _pattern.ResultRemaining = Mathf.Max(0f, _pattern.ResultRemaining - Time.deltaTime);
                    _standing.ResultRemaining = Mathf.Max(0f, _standing.ResultRemaining - Time.deltaTime);
                    RefreshMission();
                }
            }
        }

        // ---------------- 이벤트 ----------------

        void OnBalance(BossFanBalanceChanged e)
        {
            EnsureView();
            _canvas.gameObject.SetActive(!_paused);
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
            _pattern.Clear();
            RefreshMission();
            string title = e.Enhanced ? $"{e.Title}  ▲" : e.Title;
            ShowBig(title, _revenge ? revengeColor : warningColor, Mathf.Max(0.5f, e.DisplaySeconds));
        }

        void OnPatternStarted(BossPatternStarted e)
        {
            EnsureView();
            if (_bigText != null) _bigText.gameObject.SetActive(false);
            _bigVersion++;
            _pattern.Id = e.PatternId;
            _pattern.Active = true; _pattern.ResultRemaining = 0f; _pattern.Achieved = false;
            _pattern.Title = e.Enhanced ? $"{e.Title}  ▲" : e.Title;
            _pattern.Color = _revenge ? revengeColor : warningColor;
            _pattern.Body = e.Instruction;
            _pattern.Progress = "";
            _pattern.Timer = $"{e.Duration:0.0}s";
            RefreshMission();
        }

        void OnPatternProgress(BossPatternProgress e)
        {
            if (!_pattern.Active || _pattern.Id != e.PatternId) return;
            _pattern.Progress = e.ProgressText;
            _pattern.Achieved = e.Achieved;
            _pattern.Timer = $"{e.Remaining:0.0}s";
            RefreshMission();
        }

        void OnPatternResolved(BossPatternResolved e)
        {
            if (!_pattern.Active || _pattern.Id != e.PatternId) return;
            _pattern.Active = false;
            _pattern.ResultRemaining = resultHoldDuration;

            if (e.Success)
            {
                _pattern.Title = $"{e.Title}  성공!";
                _pattern.Color = successColor;
                _pattern.Body = e.FansMoved > 0
                    ? $"라이벌 팬 {e.FansMoved}명이 우리 무대로 · 보너스 +{e.BonusScore:N0}"
                    : $"만석! 보너스 +{e.BonusScore:N0}";
            }
            else
            {
                _pattern.Title = $"{e.Title}  실패";
                _pattern.Color = warningColor;
                _pattern.Body = e.FansMoved > 0
                    ? $"우리 팬 {e.FansMoved}명이 라이벌 무대로… 감소가 커집니다"
                    : "기회를 놓쳤습니다";
            }
            _pattern.Progress = _pattern.Timer = "";
            RefreshMission();
        }

        void OnStandingStarted(StageEventStarted e)
        {
            if (!HandlesStageEvent(e.EventId)) return;
            EnsureView();
            _standing.Id = e.EventId; _standing.Active = true; _standing.ResultRemaining = 0;
            _standing.Title = e.Title; _standing.Body = e.Instruction;
            _standing.Color = warningColor; _standing.Progress = "";
            _standing.Timer = $"{e.Duration:0.0}s";
            RefreshMission();
        }

        void OnStandingProgress(StageEventProgress e)
        {
            if (!HandlesStageEvent(e.EventId) || !_standing.Active || _standing.Id != e.EventId) return;
            _standing.Progress = e.ProgressText;
            _standing.Timer = $"{e.Remaining:0.0}s";
            RefreshMission();
        }

        void OnStandingResolved(StageEventResolved e)
        {
            if (!HandlesStageEvent(e.EventId) || !_standing.Active || _standing.Id != e.EventId) return;
            _standing.Active = false; _standing.ResultRemaining = resultHoldDuration;
            _standing.Title = $"{e.Title} · {(e.Success ? "성공!" : "실패")}";
            _standing.Body = e.ResultText;
            _standing.Color = e.Success ? successColor : warningColor;
            _standing.Progress = _standing.Timer = "";
            RefreshMission();
        }

        void RefreshMission()
        {
            if (_missionView == null) return;
            MissionDisplay primary = _pattern.Active ? _pattern : _standing.Active ? _standing :
                _pattern.Visible ? _pattern : _standing.Visible ? _standing : null;
            _patternPanel.gameObject.SetActive(primary != null);
            if (primary == null) return;
            MissionDisplay other = primary == _pattern ? _standing : _pattern;
            _patternPanel.sizeDelta = new Vector2(patternPanelSize.x, other.Visible ? Mathf.Max(480f, patternPanelSize.y) :
                Mathf.Max(384f, patternPanelSize.y));
            _patternTitle.text = primary.Title; _patternTitle.color = primary.Color;
            _patternBody.text = primary.Body;
            _patternProgress.text = primary.Progress;
            _patternProgress.color = primary.Achieved ? new Color32(0x20, 0x7A, 0x4C, 0xFF) : new Color32(0x1B, 0x64, 0x9C, 0xFF);
            _patternTimer.text = primary.Timer;
            string secondary = other.Visible ? other.Active
                ? $"{other.Body}\n{other.Progress}  {other.Timer}"
                : $"{other.Title}\n{other.Body}" : "";
            _missionView.SetSecondary(secondary);
            _canvas.gameObject.SetActive(!_paused);
        }

        void OnStageApplied(StageRuntimeApplied e) => ResetView();

        void ResetView()
        {
            StopAllCoroutines(); _popupRoutine = null; _bigVersion++;
            _pattern.Clear(); _standing.Clear(); _paused = false;
            _targetRivalRatio = 1f; _revenge = false;
            if (_patternPanel != null) _patternPanel.gameObject.SetActive(false);
            if (_bigText != null) _bigText.gameObject.SetActive(false);
            if (_drainPopup != null) _drainPopup.gameObject.SetActive(false);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.GameOver || e.Current == GameState.Ready) { ResetView(); return; }
            _paused = e.Current == GameState.Paused;
            if (_canvas != null) _canvas.gameObject.SetActive(!_paused && StageRuntimeDirector.CurrentStage != null && StageRuntimeDirector.CurrentStage.IsBoss);
        }

        // ---------------- 표시 ----------------

        void ShowBig(string text, Color color, float hold)
        {
            _bigText.text = text;
            _bigText.color = color;
            _bigText.gameObject.SetActive(true);
            StartCoroutine(HideBigAfter(hold, ++_bigVersion));
        }

        IEnumerator HideBigAfter(float seconds, int version)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_bigText != null && version == _bigVersion) _bigText.gameObject.SetActive(false);
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

            // 미션만 좌측 중단으로 이동. 상단 팬 게이지 배치는 유지한다.
            _patternPanel = CreateRect(canvasObject.transform, "Pattern", new Vector2(0f, .5f), patternPanelPosition, patternPanelSize);
            _patternPanel.pivot = new Vector2(0f, .5f);
            _patternPanel.anchoredPosition = patternPanelPosition;
            _missionView = MissionCueSheetView.Create(_patternPanel, resolvedFont);
            _patternTitle = _missionView.Title; _patternTimer = _missionView.Timer;
            _patternBody = _missionView.Body; _patternProgress = _missionView.Progress;
            _patternPanel.gameObject.SetActive(false);

            // 큰 자막 (예고 · REVENGE)
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
        public void EditorConfigure(TMP_FontAsset fontAsset, string name, int unused = 0)
        {
            font = fontAsset;
            if (!string.IsNullOrEmpty(name)) rivalName = name;
        }
#endif
    }
}
