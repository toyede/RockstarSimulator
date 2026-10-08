var manager=ContextStage.TourRunManager.Instance;
var run=manager.CurrentRun;
return new {scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,phase=run?.phase.ToString(),stage=manager.CurrentStageDefinition?.StageId,loading=GameJamKit.SceneLoader.IsLoading,owned=run?.ownedAugments.Count,results=run?.stageResults.Count,trace=UnityEditor.SessionState.GetString("WorkBLoopTrace","")};
