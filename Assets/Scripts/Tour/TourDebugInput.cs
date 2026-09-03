#if UNITY_EDITOR || DEVELOPMENT_BUILD
using GameJamKit;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 투어 검증용 디버그 입력. 씬에 배치하지 않아도 에디터·개발 빌드에서 스스로 생긴다.
    ///
    ///   공연(Main, Playing)
    ///     Home        : 스테이지 클리어 — 점수를 목표 점수로 맞추고 공연 종료 (D 랭크)
    ///     Shift+Home  : S 랭크 클리어 — 점수를 목표 ×5 로 맞추고 공연 종료
    ///     Delete      : 스테이지 실패 — 점수 0 으로 공연 종료
    ///     (End 는 기존 DebugForceGameOverInput 이 "현재 점수 그대로 종료"로 처리한다)
    ///
    ///   투어 허브(TourHub)
    ///     F10         : 현재 단계를 자동으로 한 칸 진행
    ///                   (Map → 노드 선택 / Dialogue → 완료 / Performance → 공연 없이 클리어 결과 제출 / Result → 확인)
    ///                   증강 선택 화면은 직접 고른다.
    ///
    /// 정식 클리어 조건(제한시간·목표 점수)은 PerformanceTimerSystem 이 담당하며 이 스크립트는 그 판정을
    /// 흉내 내기만 한다. 릴리즈 빌드에서는 컴파일되지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TourDebugInput : MonoBehaviour
    {
        const float SRankRatio = 5f; // ScoreRankUI 기본 티어: S = 목표의 500%

        [SerializeField] bool showOnScreenHelp = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<TourDebugInput>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("[TourDebugInput]");
            DontDestroyOnLoad(go);
            go.AddComponent<TourDebugInput>();
        }

        void Update()
        {
            if (IsUiInputFocused()) return;

            bool home, shift, delete, f10;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            home = keyboard.homeKey.wasPressedThisFrame;
            shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            delete = keyboard.deleteKey.wasPressedThisFrame;
            f10 = keyboard.f10Key.wasPressedThisFrame;
#else
            home = Input.GetKeyDown(KeyCode.Home);
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            delete = Input.GetKeyDown(KeyCode.Delete);
            f10 = Input.GetKeyDown(KeyCode.F10);
#endif

            if (home) ClearStage(shift ? SRankRatio : 1f);
            if (delete) FailStage();
            if (f10) AdvanceTourStep();
        }

        // ---------------- 공연 중 ----------------

        /// <summary>점수를 목표 × ratio 로 맞추고 공연을 끝낸다. 타이머 시간 초과와 같은 종료 경로를 탄다.</summary>
        public static void ClearStage(float targetRatio)
        {
            if (!GameManager.HasInstance) return;
            GameManager gm = GameManager.Instance;
            if (gm.State != GameState.Playing && gm.State != GameState.Paused) return;

            int target = PerformanceTimer.TargetScore;
            int score = target > 0
                ? Mathf.CeilToInt(target * Mathf.Max(1f, targetRatio))
                : Mathf.Max(gm.Score, 1);

            gm.SetScore(score);
            gm.GameOver();
            Debug.Log($"[TourDebug] 스테이지 클리어 강제: 점수 {score:N0} / 목표 {target:N0}");
        }

        /// <summary>점수 0 으로 공연을 끝낸다 (실패).</summary>
        public static void FailStage()
        {
            if (!GameManager.HasInstance) return;
            GameManager gm = GameManager.Instance;
            if (gm.State != GameState.Playing && gm.State != GameState.Paused) return;

            gm.SetScore(0);
            gm.GameOver();
            Debug.Log("[TourDebug] 스테이지 실패 강제: 점수 0");
        }

        // ---------------- 허브 ----------------

        /// <summary>허브에서 투어 상태를 한 칸 자동 진행한다. 공연 단계는 실제 공연 없이 클리어로 처리한다.</summary>
        public static void AdvanceTourStep()
        {
            if (!TourRunManager.HasInstance) return;
            TourRunManager manager = TourRunManager.Instance;
            TourRunState run = manager.CurrentRun;
            if (run == null) return;

            // Main 씬에서 공연 중이면 허브 진행 대신 공연 클리어 키(Home)를 쓰게 한다
            if (GameManager.HasInstance && GameManager.Instance.IsPlaying) return;

            switch (run.phase)
            {
                case RunPhase.Map:
                    for (int i = 0; i < run.map.nodes.Count; i++)
                    {
                        RunNodeState node = run.map.nodes[i];
                        if (node.status != RunNodeStatus.Available) continue;
                        manager.SelectNode(node.nodeId);
                        break;
                    }
                    break;

                case RunPhase.Dialogue:
                    Dialogue.HideImmediate();
                    manager.CompleteDialogue();
                    break;

                case RunPhase.Performance:
                {
                    RunNodeState node = run.CurrentNode;
                    StageDefinition stage = manager.CurrentStageDefinition;
                    if (node == null) return;
                    int score = stage != null ? stage.TargetScore : 0;
                    manager.ReceiveStageResult(new StageResult(node.nodeId, node.stageId, true, score, "D", 0));
                    break;
                }

                case RunPhase.Result:
                    manager.ConfirmResult();
                    break;
            }

            Debug.Log($"[TourDebug] 투어 단계 자동 진행 → {manager.CurrentRun?.phase}");
        }

        // ---------------- 표시 ----------------

        void OnGUI()
        {
            if (!showOnScreenHelp) return;

            string message = BuildHelpMessage();
            if (string.IsNullOrEmpty(message)) return;

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
            };
            const float width = 420f;
            const float height = 96f;
            GUI.Box(new Rect(16f, Screen.height - height - 16f, width, height), message, style);
        }

        string BuildHelpMessage()
        {
            string stageName = "-";
            string phase = "-";
            if (TourRunManager.HasInstance && TourRunManager.Instance.CurrentRun != null)
            {
                TourRunState run = TourRunManager.Instance.CurrentRun;
                phase = run.phase.ToString();
                StageDefinition stage = TourRunManager.Instance.CurrentStageDefinition;
                if (stage != null) stageName = stage.DisplayName;
            }

            bool playing = GameManager.HasInstance && GameManager.Instance.IsPlaying;
            if (playing)
            {
                return $"[TOUR DEBUG] {stageName} · {phase}\n" +
                       "Home: 클리어(목표 점수)   Shift+Home: S랭크 클리어\n" +
                       "Delete: 실패(점수 0)   End: 현재 점수로 종료";
            }

            if (TourRunManager.HasInstance && TourRunManager.Instance.CurrentRun != null)
                return $"[TOUR DEBUG] {stageName} · {phase}\nF10: 현재 단계 자동 진행 (공연은 클리어 처리)";

            return null;
        }

        static bool IsUiInputFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.currentSelectedGameObject != null;
        }
    }
}
#endif
