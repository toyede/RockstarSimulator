if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play Mode required");
UnityEditor.EditorApplication.isPaused=true;
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage01.asset"));gm.StartGame();flow.StartTutorial();
flow.GetType().GetMethod("EnterHoverHint",flags).Invoke(flow,null);
var cardSystem=ContextStage.CardSystem.Instance;
var input=UnityEngine.Object.FindFirstObjectByType<ContextStage.CardInput>();
var before=System.Linq.Enumerable.Range(0,cardSystem.HandCount).Select(i=>cardSystem.GetCard(i)).ToArray();
var checks=new System.Collections.Generic.List<string>();
int resolved=0;int score=gm.Score;System.Action<GameJamKit.CardResolved> callback=e=>resolved++;
GameJamKit.EventBus.Subscribe<GameJamKit.CardResolved>(callback);
try{
 foreach(int i in System.Linq.Enumerable.Range(0,before.Length))checks.Add((!input.TryUseCard(i)?"PASS ":"FAIL ")+"reject hand slot "+i);
 checks.Add((before.Length==cardSystem.HandCount&&before.Select((c,i)=>c==cardSystem.GetCard(i)).All(x=>x)?"PASS ":"FAIL ")+"hand unchanged");
 checks.Add((resolved==0?"PASS ":"FAIL ")+"no card resolution or scoring event");
 checks.Add((gm.Score==score?"PASS ":"FAIL ")+"score unchanged");
}finally{GameJamKit.EventBus.Unsubscribe<GameJamKit.CardResolved>(callback);}
return new{pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),checks};
