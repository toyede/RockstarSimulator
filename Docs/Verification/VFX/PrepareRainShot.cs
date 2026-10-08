UnityEditor.EditorApplication.isPaused=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
ContextStage.TutorialFlow.SuppressForTests=true;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();
if(ContextStage.TutorialFlow.IsRunning)tutorial.GetType().GetMethod("EndTutorial",flags).Invoke(tutorial,new object[]{false,false});
tutorial.enabled=false;
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage03.asset"));
gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var rain=UnityEngine.Object.FindFirstObjectByType<ContextStage.RainShowerRule>();
typeof(ContextStage.StageEventRule).GetField("waitForSpecialRequest",flags).SetValue(rain,false);
rain.DebugTriggerNow();
var p=UnityEngine.Object.FindFirstObjectByType<ContextStage.AudienceRosterPresenter>();
System.Collections.IEnumerator Hold()
{
    yield return new UnityEngine.WaitForSeconds(0.3f);
    var roster=ContextStage.AudienceRosterSystem.Instance;
    foreach(var snapshot in roster.Members.ToArray())
        roster.TrySetEngagement(snapshot.Id,15,ContextStage.AudienceChangeReason.RuntimeCommand,out _);
    yield return new UnityEngine.WaitForSeconds(1.3f);
    UnityEditor.EditorApplication.isPaused=true;
}
p.StartCoroutine(Hold());
return "Rain event running; freeze a rendered frame after 1.3s (no Camera.Render).";
