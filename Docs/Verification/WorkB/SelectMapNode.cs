var manager=ContextStage.TourRunManager.Instance;
var available=manager.CurrentRun.map.nodes.First(n=>n.status==ContextStage.RunNodeStatus.Available);
var map=UnityEngine.Object.FindFirstObjectByType<ContextStage.TourMapView>();
if(map==null)throw new System.InvalidOperationException("Scene map missing");
bool started=map.TryTravelTo(available.nodeId);
UnityEditor.SessionState.SetString("WorkBLoopTrace",UnityEditor.SessionState.GetString("WorkBLoopTrace","")+" -> Map("+available.stageId+")");
return new{started,node=available.nodeId,stage=available.stageId};
