using System;
using System.Collections.Generic;

namespace ContextStage
{
    /// <summary>
    /// 현재 출시 범위인 직선 경로만 만든다. 분기 확장에 필요한 연결은 nextNodeIds로 표현한다.
    /// </summary>
    public static class LinearTourMapBuilder
    {
        public static bool TryBuild(
            IReadOnlyList<StageDefinition> stages,
            out TourMapState map,
            out string error)
        {
            map = null;
            error = "";

            if (stages == null || stages.Count == 0)
            {
                error = "스테이지 목록이 비어 있습니다.";
                return false;
            }

            var stageIds = new HashSet<string>(StringComparer.Ordinal);
            var built = new TourMapState();

            for (int i = 0; i < stages.Count; i++)
            {
                StageDefinition stage = stages[i];
                if (stage == null)
                {
                    error = $"스테이지 목록의 {i}번 항목이 비어 있습니다.";
                    return false;
                }

                string stageId = stage.StageId;
                if (string.IsNullOrWhiteSpace(stageId))
                {
                    error = $"스테이지 목록의 {i}번 항목에 stageId가 없습니다.";
                    return false;
                }

                if (!stageIds.Add(stageId))
                {
                    error = $"중복 stageId가 있습니다: {stageId}";
                    return false;
                }

                var node = new RunNodeState
                {
                    nodeId = BuildNodeId(i, stageId),
                    stageId = stageId,
                    nodeType = stage.NodeType,
                    status = i == 0 ? RunNodeStatus.Available : RunNodeStatus.Locked
                };
                built.nodes.Add(node);
            }

            for (int i = 0; i < built.nodes.Count - 1; i++)
                built.nodes[i].nextNodeIds.Add(built.nodes[i + 1].nodeId);

            built.startNodeId = built.nodes[0].nodeId;

            if (!TourMapValidator.TryValidate(built, out error)) return false;

            map = built;
            return true;
        }

        static string BuildNodeId(int index, string stageId)
            => $"node_{index + 1:00}_{stageId}";
    }
}
