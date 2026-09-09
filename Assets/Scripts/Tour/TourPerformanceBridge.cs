using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 기존 한 공연 결과를 투어 런 결과로 한 번만 전달한다.
    /// 기록은 PerformanceStatsRecorder 가 모으고, 결과 화면은 ResultNewspaperView(신문)가 보여준다.
    /// 신문 뷰가 씬에 없으면 예전 CONTINUE TOUR 오버레이로 대체한다.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class TourPerformanceBridge : MonoBehaviour
    {
        [SerializeField] private string tourHubSceneName = "TourHub";
        [SerializeField, Tooltip("신문 결과창. Tools/Tour/Setup Result Newspaper 가 연결한다")] ResultNewspaperView newspaper;

        int _maxCombo;
        bool _submitted;
        GameObject _resultOverlay;
        Text _resultText;
        PerformanceStatsRecorder _recorder;

        void Awake()
        {
            _recorder = GetComponent<PerformanceStatsRecorder>();
            if (_recorder == null) _recorder = gameObject.AddComponent<PerformanceStatsRecorder>();
            if (newspaper == null) newspaper = FindFirstObjectByType<ResultNewspaperView>(FindObjectsInactive.Include);

            ApplyStageSettings();
            if (HasTourPerformance() && newspaper == null) BuildResultOverlay();
        }

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
        }

        void ApplyStageSettings()
        {
            if (!HasTourPerformance()) return;

            StageDefinition stage = TourRunManager.Instance.CurrentStageDefinition;
            PerformanceTimerSystem timer = PerformanceTimerSystem.Instance;
            if (stage == null || timer == null) return;
            float duration =
                stage.Duration *
                AugmentRuntime.Current.PerformanceDurationMultiplier;
            timer.SetRuntimeStageSettings(duration, stage.TargetScore);
        }

        bool HasTourPerformance()
        {
            return TourRunManager.HasInstance &&
                   TourRunManager.Instance.CurrentRun != null &&
                   TourRunManager.Instance.CurrentRun.phase == RunPhase.Performance;
        }

        void OnComboChanged(ComboChanged e)
        {
            _maxCombo = Mathf.Max(_maxCombo, e.CurrentCombo);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
            {
                _maxCombo = 0;
                _submitted = false;
                ApplyStageSettings();
                if (_resultOverlay != null) _resultOverlay.SetActive(false);
                if (newspaper != null) newspaper.Hide();
                return;
            }

            if (e.Current == GameState.GameOver) SubmitStageResult();
        }

        void SubmitStageResult()
        {
            if (_submitted || !HasTourPerformance()) return;

            TourRunManager manager = TourRunManager.Instance;
            RunNodeState node = manager.CurrentRun.CurrentNode;
            if (node == null) return;

            int score = GameManager.HasInstance ? GameManager.Instance.Score : 0;
            // 스테이지 룰이 판정을 대신 정했으면(보스전) 그것을, 아니면 타이머 판정(목표 점수)을 쓴다
            bool? verdict = StageRuntimeDirector.Active != null ? StageRuntimeDirector.Active.ClearVerdictOverride : null;
            bool cleared = verdict ?? !PerformanceTimer.Failed;

            // 기록 확정 (초기화 전에). 랭크는 애니메이션 중인 HUD 가 아니라 확정 점수와 공통 기준으로 계산한다
            StageDefinition stage = manager.CurrentStageDefinition;
            PerformanceReport report = _recorder != null ? _recorder.Freeze(stage) : null;
            int target = report != null ? report.targetScore : PerformanceTimer.TargetScore;
            string rank = PerformanceStatsRecorder.ComputeRank(score, target);
            int maxCombo = report != null ? Mathf.Max(report.maxCombo, _maxCombo) : _maxCombo;
            if (report != null) report.maxCombo = maxCombo;

            var result = new StageResult(node.nodeId, node.stageId, cleared, score, rank, maxCombo) { report = report };
            if (!manager.ReceiveStageResult(result)) return;

            _submitted = true;
            if (newspaper != null) ShowNewspaper(result, stage, node);
            else ShowResultOverlay(result);
        }

        void ShowNewspaper(StageResult result, StageDefinition stage, RunNodeState node)
        {
            ResultNextAction next = !result.cleared
                ? ResultNextAction.Failed
                : (node.nextNodeIds.Count == 0 ? ResultNextAction.Ending : ResultNextAction.Augment);

            int stageNumber = 1;
            TourRunState run = TourRunManager.Instance.CurrentRun;
            if (run != null) stageNumber = Mathf.Max(1, run.stageResults.Count);

            ResultPresentation presentation = ResultHeadlineSelector.Compose(
                result, stage, stageNumber, next, ResultNewspaperCatalog.LoadDefault());

            if (UIManager.HasInstance) UIManager.Instance.CloseAll();
            newspaper.Show(presentation, ContinueTour);
        }

        void BuildResultOverlay()
        {
            TourPrototypeUIFactory.EnsureEventSystem();
            Canvas canvas = TourPrototypeUIFactory.CreateCanvas("TourResultContinueCanvas", 5000);
            RectTransform panel = TourPrototypeUIFactory.CreatePanel(
                canvas.transform,
                "ContinuePanel",
                new Vector2(760f, 210f),
                new Vector2(0f, -350f),
                new Color32(0x2E, 0x22, 0x2F, 0xF5));

            _resultText = TourPrototypeUIFactory.CreateText(
                panel,
                "ResultText",
                28,
                TextAnchor.MiddleCenter,
                new Color32(0xF9, 0xC2, 0x2B, 0xFF));
            TourPrototypeUIFactory.SetRect(_resultText.rectTransform, new Vector2(0f, 50f), new Vector2(700f, 80f));

            Button button = TourPrototypeUIFactory.CreateButton(
                panel,
                "CONTINUE TOUR",
                ContinueTour,
                new Color32(0x0B, 0x8A, 0x8F, 0xFF));
            TourPrototypeUIFactory.SetRect(button.GetComponent<RectTransform>(), new Vector2(0f, -55f), new Vector2(440f, 64f));

            _resultOverlay = canvas.gameObject;
            _resultOverlay.SetActive(false);
        }

        void ShowResultOverlay(StageResult result)
        {
            if (_resultOverlay == null) BuildResultOverlay();
            if (_resultText != null)
            {
                string outcome = result.cleared ? "CLEAR" : "FAILED";
                _resultText.text = $"{outcome}  ·  {result.rank}  ·  SCORE {result.score:N0}";
            }
            _resultOverlay.SetActive(true);
        }

        void ContinueTour()
        {
            if (SceneLoader.IsLoading || !_submitted || !TourRunManager.HasInstance) return;
            if (!TourRunManager.Instance.ConfirmResult()) return;

            if (newspaper != null) newspaper.Hide();
            if (UIManager.HasInstance) UIManager.Instance.CloseAll();
            if (SpecialAudienceManager.HasInstance) SpecialAudienceManager.Instance.StopSystem();
            if (HypeSystem.HasInstance) HypeSystem.Instance.SetDecayPaused(true);
            if (GameManager.HasInstance) GameManager.Instance.ResetGame();

            SceneLoader.Load(tourHubSceneName);
        }
    }
}
