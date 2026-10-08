var gm=GameJamKit.GameManager.Instance;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();
ContextStage.TutorialFlow.SuppressForTests=true;
if(tutorial!=null)
{
    if(ContextStage.TutorialFlow.IsRunning)tutorial.GetType().GetMethod("EndTutorial",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(tutorial,new object[]{false,false});
    tutorial.enabled=false;
}
if(gm.State==GameJamKit.GameState.Ready)gm.StartGame();
gm.AddScore(UnityEngine.Mathf.Max(0,ContextStage.TourRunManager.Instance.CurrentStageDefinition.TargetScore-gm.Score));
ContextStage.PerformanceTimer.SetPaused(true);
gm.GameOver();
UnityEditor.SessionState.SetString("WorkBLoopTrace",UnityEditor.SessionState.GetString("WorkBLoopTrace","")+" -> Performance -> Result");
return new{score=gm.Score,phase=ContextStage.TourRunManager.Instance.CurrentRun.phase.ToString(),forcedCompletion=true};
