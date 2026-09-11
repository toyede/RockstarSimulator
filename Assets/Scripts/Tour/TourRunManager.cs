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
        [SerializeField] private List<StageDefinition> linearStages = new List<StageDefinition>();

        readonly Dictionary<string, StageDefinition> _activeStages =
            new Dictionary<string, StageDefinition>(StringComparer.Ordinal);

        public TourRunState CurrentRun { get; private set; }
        public bool HasActiveRun => CurrentRun != null &&
                                    CurrentRun.phase != RunPhase.Completed &&
                                    CurrentRun.phase != RunPhase.Failed;
        public StageDefinition CurrentStageDefinition =>
            CurrentRun == null ? null : FindStageDefinition(CurrentRun.CurrentNode?.stageId);

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

            _activeStages.Clear();
            for (int i = 0; i < stages.Count; i++)
                _activeStages.Add(stages[i].StageId, stages[i]);

            CurrentRun = new TourRunState
            {
                seed = seed,
                phase = RunPhase.Map,
                currentNodeId = "",
                travelFromNodeId = "",
                travelToNodeId = "",
                map = map,
                deck = new DeckRunState(),
                ownedAugments = new List<OwnedAugmentState>(),
                stageResults = new List<StageResult>(),
                totalScore = 0
            };

            NotifyStateChanged();
            return true;
        }

        public StageDefinition FindStageDefinition(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId)) return null;
            return _activeStages.TryGetValue(stageId, out StageDefinition stage) ? stage : null;
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
                run.travelFromNodeId = "";
                run.travelToNodeId = "";
                run.phase = RunPhase.Completed;
                NotifyStateChanged();
                return true;
            }

            run.phase = RunPhase.Reward;
            NotifyStateChanged();
            return true;
        }

        public bool SelectAugment(string augmentId, AugmentTier tier)
        {
            if (!RequireRun(out TourRunState run) || run.phase != RunPhase.Reward) return false;
            if (string.IsNullOrWhiteSpace(augmentId))
            {
                Debug.LogWarning("[TourRun] 비어 있는 증강 ID는 선택할 수 없습니다.", this);
                return false;
            }

            AugmentCatalog catalog = AugmentCatalog.LoadDefault();
            if (catalog == null ||
                !catalog.TryGetDefinition(augmentId, out AugmentDefinition definition) ||
                !definition.TryGetTierData(tier, out AugmentTierData tierData))
            {
                Debug.LogWarning($"[TourRun] 등록되지 않은 증강입니다: {augmentId}:{tier}", this);
                return false;
            }

            if (run.HasConflictingCardUpgrade(augmentId))
            {
                Debug.LogWarning($"[TourRun] 같은 카드 계열의 강화를 이미 보유하고 있습니다: {augmentId}:{tier}", this);
                return false;
            }

            if (run.HasAugment(augmentId, tier))
            {
                Debug.LogWarning($"[TourRun] 이미 보유한 증강입니다: {augmentId}:{tier}", this);
                return false;
            }

            if (definition.TierOwnershipPolicy ==
                    AugmentTierOwnershipPolicy.OneTierPerRun &&
                run.HasAugmentDefinition(augmentId))
            {
                Debug.LogWarning(
                    $"[TourRun] 다른 티어를 이미 보유해 함께 획득할 수 없는 증강입니다: " +
                    $"{augmentId}:{tier}",
                    this);
                return false;
            }

            CardDefinition grantedCard = null;
            if (definition.EffectType == AugmentEffectType.GrantCard)
            {
                grantedCard = tierData.GrantedCard;
                CardCatalog cardCatalog = CardCatalog.LoadDefault();
                string cardCatalogError = string.Empty;
                if (grantedCard == null ||
                    cardCatalog == null ||
                    !cardCatalog.TryValidate(out cardCatalogError) ||
                    !cardCatalog.TryGetCard(
                        grantedCard.Id,
                        out CardDefinition catalogCard))
                {
                    Debug.LogWarning(
                        $"[TourRun] 증강 카드가 올바르게 등록되지 않았습니다: " +
                        $"{augmentId}:{tier} " +
                        $"({(grantedCard == null ? "missing tier card" : cardCatalog == null ? CardCatalog.ResourcesPath : cardCatalogError)})",
                        this);
                    return false;
                }

                grantedCard = catalogCard;
            }

            if (definition.EffectType == AugmentEffectType.CardUpgrade)
            {
                CardUpgradeData upgrade = tierData.CardUpgrade;
                CardCatalog cardCatalog = CardCatalog.LoadDefault();
                string cardCatalogError = string.Empty;
                if (upgrade == null ||
                    upgrade.TargetCard == null ||
                    cardCatalog == null ||
                    !cardCatalog.TryValidate(out cardCatalogError) ||
                    !cardCatalog.TryGetCard(
                        upgrade.TargetCard.Id,
                        out _))
                {
                    Debug.LogWarning(
                        $"[TourRun] 강화 대상 카드가 올바르게 등록되지 않았습니다: " +
                        $"{augmentId}:{tier} " +
                        $"({(upgrade?.TargetCard == null ? "missing target card" : cardCatalog == null ? CardCatalog.ResourcesPath : cardCatalogError)})",
                        this);
                    return false;
                }
            }

            RunNodeState currentNode = run.CurrentNode;
            if (currentNode == null) return false;

            RunNodeState travelTarget = FindTravelTarget(run, currentNode);
            if (travelTarget == null)
            {
                Debug.LogWarning(
                    $"[TourRun] 이동할 다음 투어 노드를 찾을 수 없습니다: {currentNode.nodeId}",
                    this);
                return false;
            }

            if (run.ownedAugments == null)
                run.ownedAugments = new List<OwnedAugmentState>();
            run.ownedAugments.Add(new OwnedAugmentState(augmentId, tier));

            if (grantedCard != null)
            {
                if (run.deck == null) run.deck = new DeckRunState();
                if (run.deck.addedCards == null)
                    run.deck.addedCards = new List<RunCardState>();

                run.deck.addedCards.Add(new RunCardState(
                    $"augment:{augmentId}:{tier}",
                    grantedCard.Id,
                    0,
                    augmentId,
                    tier));
            }

            currentNode.status = RunNodeStatus.Cleared;
            run.currentNodeId = "";
            run.travelFromNodeId = currentNode.nodeId;
            run.travelToNodeId = travelTarget.nodeId;
            run.phase = RunPhase.Travel;
            NotifyStateChanged();
            return true;
        }

        public bool CompleteTravel()
        {
            if (!RequireRun(out TourRunState run) || run.phase != RunPhase.Travel) return false;

            RunNodeState fromNode = run.TravelFromNode;
            RunNodeState toNode = run.TravelToNode;
            if (fromNode == null || toNode == null ||
                fromNode.status != RunNodeStatus.Cleared ||
                toNode.status != RunNodeStatus.Locked)
            {
                Debug.LogWarning("[TourRun] 완료할 수 없는 지도 이동 상태입니다.", this);
                return false;
            }

            toNode.status = RunNodeStatus.Available;
            run.travelFromNodeId = "";
            run.travelToNodeId = "";
            run.phase = RunPhase.Map;
            NotifyStateChanged();
            return true;
        }

        public void ResetRun()
        {
            CurrentRun = null;
            _activeStages.Clear();
            NotifyStateChanged();
        }

        bool RequireRun(out TourRunState run)
        {
            run = CurrentRun;
            if (run != null) return true;

            Debug.LogWarning("[TourRun] 진행 중인 투어가 없습니다.", this);
            return false;
        }

        static RunNodeState FindTravelTarget(TourRunState run, RunNodeState currentNode)
        {
            if (run?.map == null || currentNode?.nextNodeIds == null) return null;

            for (int i = 0; i < currentNode.nextNodeIds.Count; i++)
            {
                RunNodeState nextNode = run.map.FindNode(currentNode.nextNodeIds[i]);
                if (nextNode != null && nextNode.status == RunNodeStatus.Locked)
                    return nextNode;
            }

            return null;
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
