if(UnityEditor.EditorApplication.isPlaying){
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(flow!=null)flow.enabled=false;
foreach(var device in UnityEngine.InputSystem.InputSystem.devices.Where(d=>d.name=="TutorialVerificationMouse").ToArray())UnityEngine.InputSystem.InputSystem.RemoveDevice(device);
ContextStage.TutorialFlow.SuppressForTests=false;ContextStage.PerformanceTimer.SetPaused(false);
if(ContextStage.AudienceRosterSystem.HasInstance)ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=false;
UnityEditor.EditorApplication.isPaused=false;
}
return "Tutorial control, test flags, and virtual mouse released; exit Play Mode next";
