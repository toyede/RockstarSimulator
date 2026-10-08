UnityEditor.EditorApplication.isPaused=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
ContextStage.TutorialFlow.SuppressForTests=true;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();tutorial.enabled=false;
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage05Boss.asset"));
gm.StartGame();
var presentation=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossStagePresentation>();
presentation.GetType().GetMethod("RestoreImmediate",flags).Invoke(presentation,null);
presentation.GetType().GetMethod("ShowScoreHud",flags).Invoke(presentation,null);
ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var camera=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossCameraDirector>();
System.Collections.IEnumerator Shot()
{
    yield return camera.PanRoutine(ContextStage.BossZone.RivalStage,0.4f);
    GameJamKit.EventBus.Raise(new ContextStage.BossFanBalanceChanged(4,16,20,false));
    GameJamKit.EventBus.Raise(new ContextStage.BossPatternStarted("drop","DROP!","검증 장면",6,false));
    yield return new UnityEngine.WaitForSeconds(0.22f);
    UnityEditor.EditorApplication.isPaused=true;
}
camera.StartCoroutine(Shot());
return "Boss HP state, real pan, Drop start at peak; freeze after render.";
