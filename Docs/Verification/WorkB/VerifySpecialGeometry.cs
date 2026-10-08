var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var gm=GameJamKit.GameManager.Instance;
ContextStage.TutorialFlow.SuppressForTests=true;
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>();if(view!=null)view.Hide();
if(gm.State==GameJamKit.GameState.Paused)gm.Resume();
gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage02.asset"));
gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);
var special=ContextStage.SpecialAudienceManager.Instance;special.ForceSpawn(ContextStage.HeatStage.Chill);
var target=special.CurrentDropTarget;
UnityEngine.Physics2D.SyncTransforms();
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,name)=>checks.Add((ok?"PASS ":"FAIL ")+name);
check(target!=null && target.IsActive,"special target active");
var follow=(UnityEngine.Transform)target.GetType().GetField("followTarget",flags).GetValue(target);
if(follow!=null)
{
    follow.position+=new UnityEngine.Vector3(.35f,.2f,0);
    target.GetType().GetMethod("Update",flags).Invoke(target,null);UnityEngine.Physics2D.SyncTransforms();
}
var center=target.HitCollider.bounds.center;
var screen=(UnityEngine.Vector2)target.WorldCamera.WorldToScreenPoint(center);
check(target.ContainsScreenPoint(target.WorldCamera,screen),"moving special drop collider maps back to screen");
check(target.ContainsScreenCircle(screen,0f),"drop-circle center follows rendered target");
check(!target.ContainsScreenPoint(target.WorldCamera,new UnityEngine.Vector2(-999f,-999f)),"outside target rejected");
special.StopSystem();
return new{pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),checks};
