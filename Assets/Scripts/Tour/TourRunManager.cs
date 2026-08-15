using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 전체 투어의 상태와 전이만 담당한다. 한 공연의 규칙과 점수는 기존 GameManager가 담당한다.
    /// </summary>
    public sealed class TourRunManager : MonoSingleton<TourRunManager>
    {
        [Header("Current Linear Tour")]
        [SerializeField] List<StageDefinition> linearStages = new List<StageDefinition>();

        public TourRunState CurrentRun { get; private set; }
        public bool HasActiveRun => CurrentRun != null &&
                                    CurrentRun.phase != RunPhase.Completed &&
                                    CurrentRun.phase != RunPhase.Failed;

        public event Action StateChanged;

        protected override void OnAwake()
        {
            // MonoSingleton은 자식 오브젝트면 자동 영속화하지 않으므로,
            // Title의 Managers 아래에 배치돼도 투어 상태가 Scene 전환에서 유지되게 한다.
            if (transform.parent == null) return;

            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        public bool StartNewRun()
        {
            int seed = Environment.TickCount & int.MaxValue;
            return StartNewRun(linearStages, seed);
        }

        public bool StartNewRun(IReadOnlyList<StageDefinition> stages, int seed)
        {
            if (!LinearTourMapBuilder.TryBuild(stages, out TourMapState map, out string error))
            {
                Debug.LogError($"[TourRun] 새 투어를 만들 수 없습니다. {error}", this);
                return false;
            }

            CurrentRun = new TourRunState
            {
                seed = seed,
                phase = RunPhase.Map,
                currentNodeId = "",
                map = map,
                deck = new DeckRunState(),
                ownedAugmentIds = new List<string>(),
                stageResults = new List<StageResult>(),
                totalScore = 0
            };

            NotifyStateChanged();
            return true;
        }

        public bool SelectNode(string nodeId)
        {
            if (!RequireRun(out TourRunState run)) return false;
            if (run.phase != RunPhase.Map)
            {
                Debug.LogWarning($"[TourRun] Map 단계가 아니므로 노드를 선택할 수 없습니다: {run.phase}", this);
                return false;
            }

            RunNodeState node = run.map.FindNode(nodeId);
            if (node == null || node.status != RunNodeStatus.Available)
            {
                Debug.LogWarning($"[TourRun] 선택할 수 없는 노드입니다: {nodeId}", this);
                return false;
            }

            node.status = RunNodeStatus.Current;
            run.currentNodeId = node.nodeId;
            run.phase = RunPhase.Dialogue;
            NotifyStateChanged();
            return true;
        }

        public bool CompleteDialogue()
        {
            if (!RequireRun(out TourRunState run) || run.phase != RunPhase.Dialogue) return false;

            run.phase = RunPhase.Performance;
            NotifyStateChanged();
            return true;
        }

        public bool ReceiveStageResult(StageResult result)
        {
            if (!RequireRun(out TourRunState run) || run.phase != RunPhase.Performance) return false;
            if (result == null)
            {
                Debug.LogWarning("[TourRun] 비어 있는 공연 결과는 받을 수 없습니다.", this);
                return false;
            }

            RunNodeState currentNode = run.CurrentNode;
            if (currentNode == null ||
                !string.Equals(result.nodeId, currentNode.nodeId, StringComparison.Ordinal) ||
                !string.Equals(result.stageId, currentNode.stageId, StringComparison.Ordinal))
            {
                Debug.LogWarning("[TourRun] 현재 노드와 공연 결과가 일치하지 않습니다.", this);
                return false;
            }

            run.stageResults.Add(result);
            run.totalScore += Math.Max(0, result.score);
            run.phase = RunPhase.Result;
            NotifyStateChanged();
            return true;
        }

        public bool ConfirmResult()
        {
            if (!RequireRun(out TourRunState run) || run.phase != RunPhase.Result) return false;

            StageResult result = run.LatestStageResult;
            RunNodeState currentNode = run.CurrentNode;
            if (result == null || currentNode == null) return false;

            if (!result.cleared)
            {
                currentNode.status = RunNodeStatus.Failed;
                run.phase = RunPhase.Failed;
                NotifyStateChanged();
                return true;
            }

            // 마지막 공연에는 다음 투어를 위한 증강 보상이 없다.
            if (currentNode.nextNodeIds.Count == 0)
            {
                currentNode.status = RunNodeStatus.Cleared;
                run.currentNodeId = "";
                run.phase = RunPhase.Completed;
                NotifyStateChanged();
                return true;
            }

            run.phase = RunPhase.Reward;
            NotifyStateChanged();
            return true;
        }

        public bool SelectAugment(string augmentId)
        {
            if (!RequireRun(out TourRunState run) || run.phase != RunPhase.Reward) return false;
            if (string.IsNullOrWhiteSpace(augmentId))
            {
                Debug.LogWarning("[TourRun] 비어 있는 증강 ID는 선택할 수 없습니다.", this);
                return false;
            }

            RunNodeState currentNode = run.CurrentNode;
            if (currentNode == null) return false;

            run.ownedAugmentIds.Add(augmentId);
            currentNode.status = RunNodeStatus.Cleared;

            for (int i = 0; i < currentNode.nextNodeIds.Count; i++)
            {
                RunNodeState nextNode = run.map.FindNode(currentNode.nextNodeIds[i]);
                if (nextNode != null && nextNode.status == RunNodeStatus.Locked)
                    nextNode.status = RunNodeStatus.Available;
            }

            run.currentNodeId = "";
            run.phase = RunPhase.Map;
            NotifyStateChanged();
            return true;
        }

        public void ResetRun()
        {
            CurrentRun = null;
            NotifyStateChanged();
        }

        bool RequireRun(out TourRunState run)
        {
            run = CurrentRun;
            if (run != null) return true;

            Debug.LogWarning("[TourRun] 진행 중인 투어가 없습니다.", this);
            return false;
        }

        void NotifyStateChanged() => StateChanged?.Invoke();

#if UNITY_EDITOR
        [ContextMenu("Debug/Validate Linear Tour")]
        void ValidateLinearTour()
        {
            if (LinearTourMapBuilder.TryBuild(linearStages, out TourMapState map, out string error))
                Debug.Log($"[TourRun] 선형 투어 검증 성공: {map.nodes.Count}개 노드", this);
            else
                Debug.LogError($"[TourRun] 선형 투어 검증 실패: {error}", this);
        }
#endif
    }
}
