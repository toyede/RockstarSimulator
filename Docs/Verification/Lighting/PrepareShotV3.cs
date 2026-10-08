UnityEditor.EditorApplication.isPaused=false;
ContextStage.TutorialFlow.SuppressForTests=true;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();
if(ContextStage.TutorialFlow.IsRunning)tutorial.GetType().GetMethod("EndTutorial",flags).Invoke(tutorial,new object[]{false,false});
tutorial.enabled=false;
int stageNumber=UnityEditor.SessionState.GetInt("LightingShotStage",2);
var gm=GameJamKit.GameManager.Instance;
gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage0"+stageNumber+(stageNumber==5?"Boss":"")+".asset"));
gm.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);
ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var director=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageShowDirector>();
director.GetType().GetField("_showAge",flags).SetValue(director,13f);
director.GetType().GetField("_time",flags).SetValue(director,20f);
UnityEngine.Object.FindFirstObjectByType<ContextStage.StageLightController>().SetHeatStage(ContextStage.HeatStage.Chill,true);
if(stageNumber==4)director.GetType().GetField("_rimStart",flags).SetValue(director,19.6f);
if(stageNumber==2)director.GetType().GetField("_washStart",flags).SetValue(director,19.5f);
director.GetType().GetMethod("LateUpdate",flags).Invoke(director,null);
if(stageNumber==5)
{
    var arena=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossArenaLayout>();
    UnityEngine.Camera.main.transform.position=arena.AnchorOf(ContextStage.BossZone.RivalStage)+UnityEngine.Vector3.back*10;
    foreach(var rig in UnityEngine.Object.FindObjectsByType<ContextStage.StageLightingRig>(UnityEngine.FindObjectsSortMode.None))
        if(rig.Profile.rival)rig.Render(20,1,false,true,"drop",false,false,false,true,0,0,false,null,null,0,13,1);
}
foreach(var rim in UnityEngine.Object.FindObjectsByType<ContextStage.BandRimLightView>(UnityEngine.FindObjectsSortMode.None))rim.GetType().GetMethod("LateUpdate",flags).Invoke(rim,null);
UnityEngine.Object.FindFirstObjectByType<ContextStage.StageBackgroundView>().Apply(ContextStage.StageRuntimeDirector.CurrentStage.StageId);
UnityEngine.Camera.main.Render();
UnityEditor.EditorApplication.isPaused=true;
return new {stage=stageNumber,rims=UnityEngine.Object.FindObjectsByType<ContextStage.BandRimLightView>(UnityEngine.FindObjectsSortMode.None).Length};
