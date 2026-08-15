using System;
using System.Collections.Generic;

namespace ContextStage
{
    public static class TourMapValidator
    {
        public static bool TryValidate(TourMapState map, out string error)
        {
            error = "";
            if (map == null)
            {
                error = "TourMapState가 없습니다.";
                return false;
            }

            if (map.nodes == null || map.nodes.Count == 0)
            {
                error = "투어 맵에 노드가 없습니다.";
                return false;
            }

            var byId = new Dictionary<string, RunNodeState>(StringComparer.Ordinal);
            for (int i = 0; i < map.nodes.Count; i++)
            {
                RunNodeState node = map.nodes[i];
                if (node == null)
                {
                    error = $"투어 맵의 {i}번 노드가 비어 있습니다.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(node.nodeId))
                {
                    error = $"투어 맵의 {i}번 노드에 nodeId가 없습니다.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(node.stageId))
                {
                    error = $"노드 '{node.nodeId}'에 stageId가 없습니다.";
                    return false;
                }

                if (!byId.TryAdd(node.nodeId, node))
                {
                    error = $"중복 nodeId가 있습니다: {node.nodeId}";
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(map.startNodeId) || !byId.ContainsKey(map.startNodeId))
            {
                error = "유효한 시작 노드가 없습니다.";
                return false;
            }

            foreach (RunNodeState node in map.nodes)
            {
                if (node.nextNodeIds == null)
                {
                    error = $"노드 '{node.nodeId}'의 nextNodeIds가 null입니다.";
                    return false;
                }

                var uniqueTargets = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < node.nextNodeIds.Count; i++)
                {
                    string nextId = node.nextNodeIds[i];
                    if (string.Equals(nextId, node.nodeId, StringComparison.Ordinal))
                    {
                        error = $"노드 '{node.nodeId}'가 자기 자신을 가리킵니다.";
                        return false;
                    }

                    if (string.IsNullOrWhiteSpace(nextId) || !byId.ContainsKey(nextId))
                    {
                        error = $"노드 '{node.nodeId}'가 존재하지 않는 다음 노드 '{nextId}'를 가리킵니다.";
                        return false;
                    }

                    if (!uniqueTargets.Add(nextId))
                    {
                        error = $"노드 '{node.nodeId}'에 중복 연결 '{nextId}'가 있습니다.";
                        return false;
                    }
                }
            }

            if (HasCycle(map.startNodeId, byId))
            {
                error = "투어 맵에 순환 연결이 있습니다.";
                return false;
            }

            if (!AllNodesReachable(map.startNodeId, byId))
            {
                error = "시작 노드에서 도달할 수 없는 노드가 있습니다.";
                return false;
            }

            return true;
        }

        static bool HasCycle(string startNodeId, Dictionary<string, RunNodeState> byId)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            return Visit(startNodeId, byId, visiting, visited);
        }

        static bool Visit(
            string nodeId,
            Dictionary<string, RunNodeState> byId,
            HashSet<string> visiting,
            HashSet<string> visited)
        {
            if (visiting.Contains(nodeId)) return true;
            if (visited.Contains(nodeId)) return false;

            visiting.Add(nodeId);
            RunNodeState node = byId[nodeId];
            for (int i = 0; i < node.nextNodeIds.Count; i++)
            {
                if (Visit(node.nextNodeIds[i], byId, visiting, visited)) return true;
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);
            return false;
        }

        static bool AllNodesReachable(string startNodeId, Dictionary<string, RunNodeState> byId)
        {
            var reached = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(startNodeId);

            while (pending.Count > 0)
            {
                string nodeId = pending.Pop();
                if (!reached.Add(nodeId)) continue;

                RunNodeState node = byId[nodeId];
                for (int i = 0; i < node.nextNodeIds.Count; i++)
                    pending.Push(node.nextNodeIds[i]);
            }

            return reached.Count == byId.Count;
        }
    }
}
