var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(flow!=null)flow.enabled=false;
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage04.asset"));
GameJamKit.GameManager.Instance.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);
return new{stage=ContextStage.StageRuntimeDirector.CurrentStage.StageId};
