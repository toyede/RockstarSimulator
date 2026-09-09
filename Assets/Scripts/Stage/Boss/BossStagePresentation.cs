using System;
using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 보스전 연출 담당. 룰(BossBattleRule)이 호출하고, 이 클래스만이 카메라·틴트·손패·입력 잠금을 건드린다.
    ///
    ///   Begin  : 3화면(BossArenaLayout) 표시, 카메라 홈 기록, 랭크·점수·시간 HUD 숨김
    ///   예고   : 카드 잠금 + 타이머 정지 → 틴트·손패 하강 → 라이벌 무대로 팬 → 체류 → 복귀 → 해제 → onComplete (패턴 창 시작)
    ///   격파   : 같은 순서로 라이벌 무대 소등을 보여준 뒤 복귀 → onComplete (GameOver)
    ///   End    : 전부 원상 복구
    ///
    /// 연출 중 공연이 끝나면(디버그 종료 등) 즉시 복구한다. 시간은 scaled deltaTime 이라 일시정지 메뉴와 함께 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossArenaLayout))]
    [RequireComponent(typeof(BossCameraDirector))]
    public sealed class BossStagePresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 룰이 Begin 에서 넘겨준다")] BossBattleConfig config;
        [SerializeField, Tooltip("보스전 동안 랭크·점수·시간 HUD 를 숨긴다")] bool hideScoreHud = true;
        [SerializeField, Tooltip("틴트 캔버스 Sorting Order (보스 UI 155 아래, 손패 위)")] int tintSortingOrder = 150;

        BossArenaLayout _arena;
        BossCameraDirector _camera;
        Canvas _tintCanvas;
        Image _tint;
        CardHandUI _hand;
        readonly List<CanvasGroup> _hiddenHud = new List<CanvasGroup>();

        Coroutine _sequence;
        bool _began;
        bool _locked;
        bool _timerPausedByUs;
        bool _decaySuppressedByUs;

        public bool IsPlaying => _sequence != null;
        public bool IsReady => _began && config != null;

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

        // ---------------- 수명 ----------------

        /// <summary>룰 활성. 3화면을 보이고 HUD 를 정리한다.</summary>
        public void Begin(BossBattleConfig battleConfig)
        {
            if (battleConfig != null) config = battleConfig;
            if (_began) return;
            _began = true;

            _arena.Show();
            _hand = FindFirstObjectByType<CardHandUI>();
            if (hideScoreHud) HideScoreHud();
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
        }

        // ---------------- 연출 ----------------

        /// <summary>패턴 예고. 라이벌 무대를 갔다 온 뒤 onComplete (패턴 창 시작).</summary>
        public void PlayPatternAnnounce(Action onComplete)
        {
            if (!IsReady)
            {
                onComplete?.Invoke();
                return;
            }
            StopSequence();
            _sequence = StartCoroutine(Sequence(BossCinematicKind.PatternAnnounce, config.CinematicHoldDuration, onComplete));
        }

        /// <summary>격파. 라이벌 무대 소등을 보여준 뒤 onComplete (GameOver).</summary>
        public void PlayDefeat(Action onComplete)
        {
            if (!IsReady)
            {
                onComplete?.Invoke();
                return;
            }
            StopSequence();
            _sequence = StartCoroutine(Sequence(BossCinematicKind.Defeat, config.DefeatHoldDuration, onComplete));
        }

        /// <summary>[디버그] 왕복만.</summary>
        public void PlayPreview()
        {
            if (!IsReady || _sequence != null) return;
            _sequence = StartCoroutine(Sequence(BossCinematicKind.Preview, config.CinematicHoldDuration, null));
        }

        IEnumerator Sequence(BossCinematicKind kind, float hold, Action onComplete)
        {
            Lock();
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

        void Lock()
        {
            if (_locked) return;
            _locked = true;
            CardInput.Locked = true;
            if (config != null && config.PauseTimerDuringCinematic && !PerformanceTimer.IsPaused)
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
            // 연출 도중 공연이 끝나면(디버그 종료·시간 초과) 바로 되돌린다. 격파 연출은 스스로 GameOver 를 부르므로 이미 끝난 뒤다.
            if (e.Current == GameState.GameOver || e.Current == GameState.Ready) RestoreImmediate();
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
            // 캔버스 루트나 손패의 조상을 숨기면 화면 전체가 사라지므로 건너뛴다
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

        // ---------------- 틴트 ----------------

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

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(BossBattleConfig battleConfig) => config = battleConfig;
#endif
    }
}
