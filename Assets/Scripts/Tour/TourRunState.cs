using System;
using System.Collections.Generic;

namespace ContextStage
{
    [Serializable]
    public sealed class RunCardState
    {
        public string instanceId;
        public string cardId;
        public int upgradeLevel;

        public RunCardState() { }

        public RunCardState(string instanceId, string cardId, int upgradeLevel = 0)
        {
            this.instanceId = instanceId;
            this.cardId = cardId;
            this.upgradeLevel = Math.Max(0, upgradeLevel);
        }
    }

    [Serializable]
    public sealed class DeckRunState
    {
        public List<RunCardState> cards = new List<RunCardState>();
    }

    [Serializable]
    public sealed class StageResult
    {
        public string nodeId;
        public string stageId;
        public bool cleared;
        public int score;
        public string rank;
        public int maxCombo;

        public StageResult() { }

        public StageResult(
            string nodeId,
            string stageId,
            bool cleared,
            int score,
            string rank,
            int maxCombo)
        {
            this.nodeId = nodeId;
            this.stageId = stageId;
            this.cleared = cleared;
            this.score = Math.Max(0, score);
            this.rank = rank ?? "";
            this.maxCombo = Math.Max(0, maxCombo);
        }
    }

    [Serializable]
    public sealed class RunNodeState
    {
        public string nodeId;
        public string stageId;
        public RunNodeType nodeType;
        public RunNodeStatus status;
        public List<string> nextNodeIds = new List<string>();
    }

    [Serializable]
    public sealed class TourMapState
    {
        public string startNodeId;
        public List<RunNodeState> nodes = new List<RunNodeState>();

        public RunNodeState FindNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || nodes == null) return null;

            for (int i = 0; i < nodes.Count; i++)
            {
                RunNodeState node = nodes[i];
                if (node != null && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class TourRunState
    {
        public int seed;
        public RunPhase phase;
        public string currentNodeId;
        public TourMapState map = new TourMapState();
        public DeckRunState deck = new DeckRunState();
        public List<OwnedAugmentState> ownedAugments = new List<OwnedAugmentState>();
        public List<StageResult> stageResults = new List<StageResult>();
        public int totalScore;

        public RunNodeState CurrentNode => map == null ? null : map.FindNode(currentNodeId);

        public StageResult LatestStageResult
        {
            get
            {
                if (stageResults == null || stageResults.Count == 0) return null;
                return stageResults[stageResults.Count - 1];
            }
        }

        public bool HasAugment(string definitionId, AugmentTier tier)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || ownedAugments == null)
                return false;

            for (int i = 0; i < ownedAugments.Count; i++)
            {
                OwnedAugmentState owned = ownedAugments[i];
                if (owned != null && owned.Matches(definitionId, tier))
                    return true;
            }

            return false;
        }
    }
}
