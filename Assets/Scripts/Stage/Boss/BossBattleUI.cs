using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 보스전 HUD: 상단 팬 쟁탈 바(우리 | 라이벌) + 드레인 카운트다운 + 미션.
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
        [SerializeField, Tooltip("상단에서의 위치(px, 1080 기준)")] float topOffset = 54f;
        [SerializeField] Sprite fanBarBack;
        [SerializeField] Sprite fanBarOurs;
        [SerializeField] Sprite fanBarRival;
        [SerializeField, Tooltip("미션 패널 위치 — 왼쪽 중단 기준(px, 1920×1080)")] Vector2 patternPanelPosition = new Vector2(32f, 0f);
        [SerializeField, Tooltip("미션 패널 크기(px)")] Vector2 patternPanelSize = new Vector2(512f, 384f);

        Canvas _canvas;
        Image _barBack;
        Image _rivalFill;
        Image _ourFill;
        bool _balanceInitialized;
        bool _hasFans = true;
        float _drainAccent;
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

            if (_paused) return;
            _rivalFill.fillAmount = fillLerpSpeed <= 0f
                ? _targetRivalRatio
                : Mathf.MoveTowards(_rivalFill.fillAmount, _targetRivalRatio, fillLerpSpeed * Time.deltaTime);
            _ourFill.fillAmount = 1f - _rivalFill.fillAmount;
            if (!_hasFans) _ourFill.fillAmount = _rivalFill.fillAmount = 0f;
            _drainAccent = Mathf.Max(0f, _drainAccent - Time.deltaTime * 2f);
            _rivalText.color = Color.Lerp(Color.white, warningColor, _drainAccent);

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
            _hasFans = e.Total > 0;
            if (!_balanceInitialized)
            {
                _rivalFill.fillAmount = _targetRivalRatio;
                _ourFill.fillAmount = 1f - _targetRivalRatio;
                _balanceInitialized = true;
            }
            if (e.Total <= 0) { _rivalFill.fillAmount = _ourFill.fillAmount = 0f; }
            _rivalText.text = $"{rivalName}  {e.Rival}";
            _ourText.text = $"우리  {e.Ours}";
            _revenge = e.Revenge;
            _revengeTag.gameObject.SetActive(_revenge);
        }

        void OnDrainCountdown(BossDrainCountdown e)
        {
            if (_drainText == null) return;
            _drainText.text = e.Active
                ? $"다음 점수 감소까지 {Mathf.CeilToInt(e.Remaining) / 60:00}:{Mathf.CeilToInt(e.Remaining) % 60:00} · −{e.Amount:N0}점"
                : "라이벌 팬 없음 · 점수 감소 정지";
            _drainText.color = e.Active && e.Remaining <= 2f
                ? Color.Lerp(warningColor, Color.white, .2f + .2f * Mathf.Sin(Time.time * 10f))
                : new Color(1f, 1f, 1f, 0.9f);
        }

        void OnDrainApplied(BossDrainApplied e)
        {
            EnsureView();
            if (e.Amount <= 0) return;
            _drainAccent = 1f;
            // ScoreUI가 있는 실제 공연에서는 숫자를 점수 옆에서 한 번만 보여준다.
            if (ScoreUI.HasDrainFeedback) return;
            _drainPopup.text = $"−{e.Amount:N0}점";
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
            _missionView.PlayEntrance();
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
            _missionView.PlayResult(e.Success);
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
            _missionView.PlayResult(e.Success);
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
            _patternProgress.color = primary.Achieved ? new Color32(0x65, 0xE6, 0xA0, 0xFF) : new Color32(0x76, 0xD6, 0xFF, 0xFF);
            _patternTimer.text = primary.Timer;
            string secondary = other.Visible ? other.Active
                ? $"{other.Body}\n{other.Progress}  {other.Timer}"
                : $"{other.Title}\n{other.Body}" : "";
            _missionView.SetSecondary(secondary);
            _canvas.gameObject.SetActive(!_paused);
        }

        // 룰의 OnActivate가 StageRuntimeApplied보다 먼저 초기 팬 비율을 발행한다.
        // 그 확정값을 뒤늦은 화면 초기화가 100% 라이벌로 되돌리면 안 된다.
        void OnStageApplied(StageRuntimeApplied e) => ResetView(e.Stage == null || !e.Stage.IsBoss);

        void ResetView(bool clearBalance = true)
        {
            StopAllCoroutines(); _popupRoutine = null; _bigVersion++;
            _pattern.Clear(); _standing.Clear(); _paused = false;
            if (clearBalance) { _targetRivalRatio = 1f; _revenge = false; _balanceInitialized = false; }
            _drainAccent = 0f;
            if (_patternPanel != null) _patternPanel.gameObject.SetActive(false);
            if (_bigText != null) _bigText.gameObject.SetActive(false);
            if (_drainPopup != null) _drainPopup.gameObject.SetActive(false);
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.GameOver || e.Current == GameState.Ready) { ResetView(e.Current == GameState.GameOver); return; }
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

            // 아트 원본 202×10의 정수 3배. 채움 원본의 투명 테두리로 프레임을 보존한다.
            RectTransform bar = CreateRect(canvasObject.transform, "FanBar", new Vector2(0.5f, 1f), new Vector2(0f, -topOffset), new Vector2(606f, 30f));
            _barBack = bar.gameObject.AddComponent<Image>();
            _barBack.sprite = fanBarBack != null ? fanBarBack : CreateSolidSprite();
            _barBack.color = fanBarBack != null ? Color.white : Color.black;
            _barBack.raycastTarget = false;

            _ourFill = CreateFanFill(bar, "OurFill", fanBarOurs, ourColor, 0);

            var fillObject = new GameObject("RivalFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.SetParent(bar, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            _rivalFill = fillObject.GetComponent<Image>();
            _rivalFill.sprite = fanBarRival != null ? fanBarRival : CreateSolidSprite();
            _rivalFill.type = Image.Type.Filled;
            _rivalFill.fillMethod = Image.FillMethod.Horizontal;
            _rivalFill.fillOrigin = 1;
            _rivalFill.fillAmount = 1f;
            _rivalFill.color = fanBarRival != null ? Color.white : rivalColor;
            _rivalFill.raycastTarget = false;

            _rivalText = CreateText(bar, "Rival", resolvedFont, 24f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetRect(_rivalText.rectTransform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(0f, .5f), new Vector2(14f, 0f), new Vector2(250f, 36f));
            _ourText = CreateText(bar, "Ours", resolvedFont, 24f, TextAlignmentOptions.MidlineRight, Color.white);
            SetRect(_ourText.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(1f, .5f), new Vector2(-14f, 0f), new Vector2(180f, 36f));
            _revengeTag = CreateText(bar, "Revenge", resolvedFont, 20f, TextAlignmentOptions.Center, revengeColor);
            SetRect(_revengeTag.rectTransform, new Vector2(.5f,1f), new Vector2(.5f,1f), new Vector2(.5f,0f), new Vector2(0f,4f), new Vector2(300f,26f));
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
            MissionCueSheetLayout.Apply(_patternPanel, patternPanelPosition, patternPanelSize);
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

        static Image CreateFanFill(RectTransform parent, string name, Sprite sprite, Color fallback, int origin)
        {
            var rect = CreateRect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : CreateSolidSprite();
            image.color = sprite != null ? Color.white : fallback;
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = origin; image.fillAmount = 0f; image.raycastTarget = false;
            return image;
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
