using System;
using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 보스전 연출 담당. 룰(BossBattleRule)이 호출하고, 이 클래스만이 카메라·틴트·손패·입력 잠금을 건드린다.
    ///
    ///   Begin   : 2화면(BossArenaLayout) 표시, 카메라 홈 기록, 엿보기 버튼 표시
    ///   예고    : 카드 잠금 + 타이머 정지 → 틴트·손패 하강 → 라이벌 무대로 팬 → 체류 → 복귀 → 해제 → onComplete (패턴 창 시작)
    ///   엿보기  : 버튼으로 라이벌 무대를 본다. 손패 하강·입력 잠금은 같지만 **타이머·드레인은 멈추지 않는다** (보는 시간이 대가).
    ///             패턴 예고·창 중에는 버튼이 비활성. 다시 버튼을 누르면 돌아온다
    ///   End     : 전부 원상 복구
    ///
    /// 연출 중 공연이 끝나면 즉시 복구한다. 시간은 scaled deltaTime 이라 일시정지 메뉴와 함께 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossArenaLayout))]
    [RequireComponent(typeof(BossCameraDirector))]
    public sealed class BossStagePresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 룰이 Begin 에서 넘겨준다")] BossBattleConfig config;
        [SerializeField, Tooltip("보스전 동안 랭크·점수·시간 HUD 를 숨긴다 (관객 쟁탈전에서는 보여준다)")] bool hideScoreHud = false;
        [SerializeField, Tooltip("틴트 캔버스 Sorting Order (보스 UI 155 아래, 손패 위)")] int tintSortingOrder = 150;

        [Header("엿보기 버튼")]
        [SerializeField, Tooltip("한글 폰트. 비우면 DialogueCatalog 스타일 폰트")] TMP_FontAsset font;
        [SerializeField] string peekLabel = "<  라이벌 무대";
        [SerializeField] string returnLabel = "우리 무대  >";
        [SerializeField, Tooltip("화면 세로 위치(px, 1080 기준, 가운데 0)")] float peekButtonY = 40f;

        BossArenaLayout _arena;
        BossCameraDirector _camera;
        Canvas _tintCanvas;
        Image _tint;
        CardHandUI _hand;
        readonly List<CanvasGroup> _hiddenHud = new List<CanvasGroup>();

        Canvas _peekCanvas;
        Button _peekButton;
        RectTransform _peekRect;
        TMP_Text _peekText;
        Func<bool> _canPeek;

        Coroutine _sequence;
        bool _began;
        bool _locked;
        bool _timerPausedByUs;
        bool _decaySuppressedByUs;
        bool _peeking;

        public bool IsPlaying => _sequence != null;
        public bool IsReady => _began && config != null;
        public bool IsPeeking => _peeking;

        void Awake()
        {
            _arena = GetComponent<BossArenaLayout>();
            _camera = GetComponent<BossCameraDirector>();
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            End();
        }

        void Update()
        {
            if (!_began || _peekButton == null) return;
            bool allowed = _sequence == null && (_canPeek == null || _canPeek());
            _peekButton.interactable = allowed || _peeking;
        }

        // ---------------- 수명 ----------------

        /// <summary>룰 활성. 2화면을 보이고 엿보기 버튼을 켠다. canPeek 은 패턴 중이 아닐 때만 true 를 돌려준다.</summary>
        public void Begin(BossBattleConfig battleConfig, Func<bool> canPeek = null)
        {
            if (battleConfig != null) config = battleConfig;
            _canPeek = canPeek;
            if (_began) return;
            _began = true;

            _arena.Show();
            _hand = FindFirstObjectByType<CardHandUI>();
            if (hideScoreHud) HideScoreHud();
            EnsurePeekButton();
            _peekCanvas.gameObject.SetActive(true);
            SetPeekLabel(false);
        }

        /// <summary>룰 해제·공연 종료. 연출을 끊고 전부 복구한다.</summary>
        public void End()
        {
            if (!_began) return;
            _began = false;

            StopSequence();
            RestoreImmediate();
            _arena.Hide();
            ShowScoreHud();
            if (_peekCanvas != null) _peekCanvas.gameObject.SetActive(false);
        }

        // ---------------- 예고 연출 ----------------

        /// <summary>패턴 예고. 라이벌 무대를 갔다 온 뒤 onComplete (패턴 창 시작). 엿보기 중이면 그 자리에서 이어서 진행.</summary>
        public void PlayPatternAnnounce(Action onComplete)
        {
            if (!IsReady)
            {
                onComplete?.Invoke();
                return;
            }
            StopSequence();
            _peeking = false;
            SetPeekLabel(false);
            _sequence = StartCoroutine(AnnounceSequence(BossCinematicKind.PatternAnnounce, config.CinematicHoldDuration, onComplete));
        }

        /// <summary>[디버그] 왕복만.</summary>
        public void PlayPreview()
        {
            if (!IsReady || _sequence != null || _peeking) return;
            _sequence = StartCoroutine(AnnounceSequence(BossCinematicKind.Preview, config.CinematicHoldDuration, null));
        }

        IEnumerator AnnounceSequence(BossCinematicKind kind, float hold, Action onComplete)
        {
            Lock(pauseTimer: config.PauseTimerDuringCinematic);
            EventBus.Raise(new BossCinematicStarted(kind));

            float fade = config.CinematicFadeDuration;
            EnsureTint();
            StartCoroutine(FadeTint(config.CinematicTintAlpha, fade));
            if (_hand != null) _hand.SetStowed(true, fade, config.HandStowDistance);
            yield return Wait(fade);

            yield return _camera.PanRoutine(BossZone.RivalStage, config.CinematicPanOutDuration);
            yield return Wait(hold);
            yield return _camera.PanRoutine(BossZone.OurStage, config.CinematicPanBackDuration);

            StartCoroutine(FadeTint(0f, fade));
            if (_hand != null) _hand.SetStowed(false, fade, config.HandStowDistance);
            yield return Wait(fade);

            Unlock();
            _sequence = null;
            EventBus.Raise(new BossCinematicEnded(kind));
            onComplete?.Invoke();
        }

        // ---------------- 엿보기 ----------------

        /// <summary>버튼: 라이벌 무대 보기 ↔ 우리 무대로 돌아가기.</summary>
        public void TogglePeek()
        {
            if (!IsReady || _sequence != null) return;
            if (_peeking) EndPeek();
            else if (_canPeek == null || _canPeek()) BeginPeek();
        }

        void BeginPeek()
        {
            _peeking = true;
            SetPeekLabel(true);
            _sequence = StartCoroutine(PeekSequence(true));
        }

        void EndPeek()
        {
            _peeking = false;
            SetPeekLabel(false);
            _sequence = StartCoroutine(PeekSequence(false));
        }

        IEnumerator PeekSequence(bool toRival)
        {
            float fade = config.CinematicFadeDuration;
            EnsureTint();
            if (toRival)
            {
                Lock(pauseTimer: false); // 엿보기는 타이머·드레인을 멈추지 않는다
                EventBus.Raise(new BossCinematicStarted(BossCinematicKind.Peek));
                StartCoroutine(FadeTint(config.CinematicTintAlpha, fade));
                if (_hand != null) _hand.SetStowed(true, fade, config.HandStowDistance);
                yield return Wait(fade);
                yield return _camera.PanRoutine(BossZone.RivalStage, config.PeekPanDuration);
                _sequence = null;
                yield break;
            }

            yield return _camera.PanRoutine(BossZone.OurStage, config.PeekPanDuration);
            StartCoroutine(FadeTint(0f, fade));
            if (_hand != null) _hand.SetStowed(false, fade, config.HandStowDistance);
            yield return Wait(fade);
            Unlock();
            _sequence = null;
            EventBus.Raise(new BossCinematicEnded(BossCinematicKind.Peek));
        }

        void SetPeekLabel(bool peeking)
        {
            if (_peekText != null) _peekText.text = peeking ? returnLabel : peekLabel;
            if (_peekRect != null)
            {
                // 왼쪽 가장자리 ↔ 오른쪽 가장자리
                _peekRect.anchorMin = _peekRect.anchorMax = new Vector2(peeking ? 1f : 0f, 0.5f);
                _peekRect.pivot = new Vector2(peeking ? 1f : 0f, 0.5f);
                _peekRect.anchoredPosition = new Vector2(peeking ? -16f : 16f, peekButtonY);
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator FadeTint(float target, float duration)
        {
            if (_tint == null) yield break;
            _tintCanvas.gameObject.SetActive(true);
            float from = _tint.color.a;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                _tint.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, target, t));
                yield return null;
            }
            _tint.color = new Color(0f, 0f, 0f, target);
            if (target <= 0f) _tintCanvas.gameObject.SetActive(false);
        }

        // ---------------- 잠금 ----------------

        void Lock(bool pauseTimer)
        {
            if (_locked) return;
            _locked = true;
            CardInput.Locked = true;
            if (pauseTimer && !PerformanceTimer.IsPaused)
            {
                PerformanceTimer.SetPaused(true);
                if (HypeSystem.HasInstance) HypeSystem.Instance.SetDecayPaused(true);
                _timerPausedByUs = true;

                // 카드를 못 내는 동안 관객 몰입도가 깎이면 억울하므로 자연 감소도 같이 멈춘다
                AudienceRosterSystem roster = AudienceRosterSystem.HasInstance ? AudienceRosterSystem.Instance : null;
                if (roster != null && !roster.SuppressEngagementDecay)
                {
                    roster.SuppressEngagementDecay = true;
                    _decaySuppressedByUs = true;
                }
            }
        }

        void Unlock()
        {
            if (!_locked) return;
            _locked = false;
            CardInput.Locked = false;
            if (_timerPausedByUs)
            {
                PerformanceTimer.SetPaused(false);
                if (HypeSystem.HasInstance) HypeSystem.Instance.SetDecayPaused(false);
                _timerPausedByUs = false;
            }
            if (_decaySuppressedByUs)
            {
                if (AudienceRosterSystem.HasInstance) AudienceRosterSystem.Instance.SuppressEngagementDecay = false;
                _decaySuppressedByUs = false;
            }
        }

        void StopSequence()
        {
            if (_sequence == null) return;
            StopCoroutine(_sequence);
            _sequence = null;
        }

        /// <summary>연출을 끊고 카메라·틴트·손패·잠금을 즉시 되돌린다.</summary>
        void RestoreImmediate()
        {
            StopAllCoroutines();
            _sequence = null;
            _peeking = false;
            SetPeekLabel(false);
            Unlock();
            _camera.SnapHome();
            if (_tint != null)
            {
                _tint.color = new Color(0f, 0f, 0f, 0f);
                _tintCanvas.gameObject.SetActive(false);
            }
            if (_hand != null) _hand.SetStowed(false, 0f, 0f);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (!_began) return;
            if (e.Current == GameState.GameOver || e.Current == GameState.Ready)
            {
                RestoreImmediate();
                if (_peekCanvas != null) _peekCanvas.gameObject.SetActive(false);
            }
        }

        // ---------------- HUD ----------------

        void HideScoreHud()
        {
            _hiddenHud.Clear();
            HideHudOf(FindFirstObjectByType<ScoreRankUI>());
            HideHudOf(FindFirstObjectByType<ScoreUI>());
            HideHudOf(FindFirstObjectByType<PerformanceTimerUI>());
        }

        void HideHudOf(Component component)
        {
            if (component == null) return;
            GameObject go = component.gameObject;
            if (go.GetComponent<Canvas>() != null) return;
            if (_hand != null && _hand.transform.IsChildOf(go.transform)) return;

            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            _hiddenHud.Add(group);
        }

        void ShowScoreHud()
        {
            for (int i = 0; i < _hiddenHud.Count; i++)
            {
                CanvasGroup group = _hiddenHud[i];
                if (group == null) continue;
                group.alpha = 1f;
                group.blocksRaycasts = true;
                group.interactable = true;
            }
            _hiddenHud.Clear();
        }

        // ---------------- 틴트 · 버튼 ----------------

        void EnsureTint()
        {
            if (_tintCanvas != null) return;

            var canvasObject = new GameObject("BossCinematicTint", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _tintCanvas = canvasObject.GetComponent<Canvas>();
            _tintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _tintCanvas.sortingOrder = tintSortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var imageObject = new GameObject("Tint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = imageObject.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _tint = imageObject.GetComponent<Image>();
            _tint.color = new Color(0f, 0f, 0f, 0f);
            _tint.raycastTarget = false;

            _tintCanvas.gameObject.SetActive(false);
        }

        void EnsurePeekButton()
        {
            if (_peekCanvas != null) return;

            TMP_FontAsset resolvedFont = font;
            if (resolvedFont == null)
            {
                DialogueCatalog catalog = DialogueCatalog.LoadDefault();
                if (catalog != null && catalog.Style != null) resolvedFont = catalog.Style.Font;
            }

            var canvasObject = new GameObject("BossPeekCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _peekCanvas = canvasObject.GetComponent<Canvas>();
            _peekCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _peekCanvas.sortingOrder = tintSortingOrder + 2; // 틴트 위, 보스 UI(155) 아래
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var buttonObject = new GameObject("PeekButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _peekRect = buttonObject.GetComponent<RectTransform>();
            _peekRect.SetParent(canvasObject.transform, false);
            _peekRect.sizeDelta = new Vector2(240f, 56f);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.1f, 0.07f, 0.12f, 0.9f);
            _peekButton = buttonObject.GetComponent<Button>();
            var colors = _peekButton.colors;
            colors.highlightedColor = new Color(0.85f, 0.8f, 0.88f, 1f);
            colors.pressedColor = new Color(0.65f, 0.6f, 0.68f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            _peekButton.colors = colors;
            _peekButton.onClick.AddListener(TogglePeek);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(_peekRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _peekText = textObject.GetComponent<TextMeshProUGUI>();
            if (resolvedFont != null) _peekText.font = resolvedFont;
            _peekText.fontSize = 24f;
            _peekText.alignment = TextAlignmentOptions.Center;
            _peekText.color = Color.white;
            _peekText.raycastTarget = false;

            SetPeekLabel(false);
            _peekCanvas.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(BossBattleConfig battleConfig) => config = battleConfig;
#endif
    }
}
