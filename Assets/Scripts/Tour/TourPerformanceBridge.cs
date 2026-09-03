using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>기존 한 공연 결과를 투어 런 결과로 한 번만 전달한다.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class TourPerformanceBridge : MonoBehaviour
    {
        [SerializeField] private string tourHubSceneName = "TourHub";

        int _maxCombo;
        bool _submitted;
        GameObject _resultOverlay;
        Text _resultText;

        void Awake()
        {
            ApplyStageSettings();
            if (HasTourPerformance()) BuildResultOverlay();
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
            bool cleared = !PerformanceTimer.Failed;
            ScoreRankUI rankUI = FindFirstObjectByType<ScoreRankUI>();
            string rank = rankUI == null ? (cleared ? "CLEAR" : "F") : rankUI.CurrentRankLabel;

            var result = new StageResult(node.nodeId, node.stageId, cleared, score, rank, _maxCombo);
            if (!manager.ReceiveStageResult(result)) return;

            _submitted = true;
            ShowResultOverlay(result);
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

            if (UIManager.HasInstance) UIManager.Instance.CloseAll();
            if (SpecialAudienceManager.HasInstance) SpecialAudienceManager.Instance.StopSystem();
            if (HypeSystem.HasInstance) HypeSystem.Instance.SetDecayPaused(true);
            if (GameManager.HasInstance) GameManager.Instance.ResetGame();

            SceneLoader.Load(tourHubSceneName);
        }
    }
}
