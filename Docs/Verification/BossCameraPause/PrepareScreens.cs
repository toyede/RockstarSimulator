UnityEditor.EditorApplication.isPaused=false;
GameJamKit.UIManager.Instance.CloseAll();
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(flow!=null)flow.enabled=false;
GameJamKit.GameManager.Instance.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage04.asset"));
UnityEngine.Object.FindFirstObjectByType<ContextStage.StageBackgroundView>().Apply("stage_04");
GameJamKit.GameManager.Instance.StartGame();ContextStage.PerformanceTimer.SetPaused(true);
return "Stage4 ready for capture";
