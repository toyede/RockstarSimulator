var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>(UnityEngine.FindObjectsInactive.Include);
object Read(object o,string name)=>o.GetType().GetField(name,flags).GetValue(o);
var target=Read(flow,"_inspectionActor") as ContextStage.AudienceMemberActor;
var hover=UnityEngine.Object.FindFirstObjectByType<ContextStage.AudiencePreferenceHoverController>();
return new{playing=GameJamKit.GameManager.Instance.IsPlaying,ContextStage.TutorialFlow.IsRunning,flow.enabled,active=flow.gameObject.activeInHierarchy,phase=Read(flow,"_phase").ToString(),taught=Read(flow,"_hoverTaught"),timer=Read(flow,"_hoverInspectionTimer"),target=target!=null?target.BoundId.ToString():"null",hover=hover.RevealedActor!=null?hover.RevealedActor.BoundId.ToString():"null",mouse=UnityEngine.InputSystem.Mouse.current?.name,scale=UnityEngine.Time.timeScale};
