var manager = ContextStage.TourRunManager.Instance;
if (manager == null || manager.CurrentRun == null) throw new System.Exception("Native tour run missing");
foreach (var node in manager.CurrentRun.map.nodes) {
    if (node.status != ContextStage.RunNodeStatus.Available) continue;
    if (!manager.SelectNode(node.nodeId)) throw new System.Exception("Failed selecting available stage");
    return "Native stage selected: " + node.stageId;
}
throw new System.Exception("No available stage");
