if(!UnityEditor.EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Title")throw new System.Exception("Play Title required");
UnityEditor.EditorApplication.isPaused=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
object Get(object o,string key)=>o.GetType().GetField(key,flags).GetValue(o);
void Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,flags).Invoke(o,args);
var checks=new System.Collections.Generic.List<string>();
void Save(string state)=>UnityEditor.SessionState.SetString("PolishTourVerification",Newtonsoft.Json.JsonConvert.SerializeObject(new{state,pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),checks}));
void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);Save("running");if(!ok)throw new System.Exception(label);}
var gm=GameJamKit.GameManager.Instance;
string scoreKey=(string)Get(gm,"highScoreKey");bool had=GameJamKit.Save.Has(scoreKey);int previous=GameJamKit.Save.GetInt(scoreKey);
UnityEditor.SessionState.SetString("PolishScoreBackup",Newtonsoft.Json.JsonConvert.SerializeObject(new{scoreKey,had,previous}));
System.Collections.IEnumerator WaitScene(string scene){float until=UnityEngine.Time.realtimeSinceStartup+35;while((GameJamKit.SceneLoader.IsLoading||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!=scene)&&UnityEngine.Time.realtimeSinceStartup<until)yield return null;yield return null;Check(!GameJamKit.SceneLoader.IsLoading&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name==scene,"scene settled "+scene);}
System.Collections.IEnumerator FinishDialogue(){float until=UnityEngine.Time.realtimeSinceStartup+5;while(ContextStage.Dialogue.IsPlaying&&UnityEngine.Time.realtimeSinceStartup<until){ContextStage.Dialogue.SkipCurrent();yield return new UnityEngine.WaitForSecondsRealtime(.12f);}Check(!ContextStage.Dialogue.IsPlaying,"dialogue callback finished");}
System.Collections.IEnumerator Test(){
 try{
  // Prevent only the automatic local high-score update; preserve exact pre-test key in finally.
  GameJamKit.Save.SetInt(scoreKey,int.MaxValue);
  var launcher=UnityEngine.Object.FindFirstObjectByType<ContextStage.TourTitleLauncher>();Check(launcher!=null,"real Title tour launcher exists");launcher.StartTour();yield return WaitScene("TourHub");
  var manager=ContextStage.TourRunManager.Instance;Check(manager.CurrentRun.map.nodes.Count==5,"title starts five-stage tour");
  for(int stage=1;stage<=5;stage++){
   Check(manager.CurrentRun.phase==ContextStage.RunPhase.Map,"Stage "+stage+" map available");
   var node=manager.CurrentRun.map.nodes.First(n=>n.status==ContextStage.RunNodeStatus.Available);
   Check(manager.SelectNode(node.nodeId),"Stage "+stage+" map selection");
   yield return new UnityEngine.WaitForSecondsRealtime(.2f);Check(ContextStage.Dialogue.IsPlaying,"Stage "+stage+" dialogue shown");yield return FinishDialogue();yield return WaitScene("Main");
   gm=GameJamKit.GameManager.Instance;
   Check((string)Get(gm,"highScoreKey")==scoreKey,"Stage "+stage+" save key unchanged");
   Check(ContextStage.StageRuntimeDirector.CurrentStage.StageId==node.stageId,"Stage "+stage+" correct definition applied");
   var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(tutorial!=null)tutorial.enabled=false;
   var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>();if(overlay!=null)overlay.HideAll();
   GameJamKit.UIManager.Instance.CloseAll();if(gm.State!=GameJamKit.GameState.Ready)gm.ResetGame();gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
   yield return new UnityEngine.WaitForSecondsRealtime(.25f);
   // Accelerated verification endpoint, not natural play or a balance result.
   gm.SetScore(ContextStage.PerformanceTimer.TargetScore);gm.GameOver();yield return new UnityEngine.WaitForSecondsRealtime(.3f);
   Check(manager.CurrentRun.phase==ContextStage.RunPhase.Result&&manager.CurrentRun.stageResults.Count==stage,"Stage "+stage+" result submitted exactly once");
   var paper=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include);Check(paper!=null&&paper.gameObject.activeInHierarchy,"Stage "+stage+" newspaper visible");
   var bridge=UnityEngine.Object.FindFirstObjectByType<ContextStage.TourPerformanceBridge>();Call(bridge,"ContinueTour");yield return WaitScene("TourHub");
   if(stage==5)break;
   Check(manager.CurrentRun.phase==ContextStage.RunPhase.Reward,"Stage "+stage+" reward reached");
   var popup=UnityEngine.Object.FindFirstObjectByType<ContextStage.AugmentSelectionPopup>();Check(popup!=null&&popup.IsVisible,"Stage "+stage+" augment popup visible");
   var choices=(ContextStage.AugmentChoiceView[])Get(popup,"choiceViews");int[] remaining=choices.Select(c=>(int)Get(c,"_rerollsRemaining")).ToArray();
   Check(remaining.All(n=>n>0),"Stage "+stage+" reroll initialized per slot");
   ((UnityEngine.UI.Button)Get(choices[0],"rerollButton")).onClick.Invoke();yield return new UnityEngine.WaitForSecondsRealtime(2f);
   Check((int)Get(choices[0],"_rerollsRemaining")==remaining[0]-1&&(int)Get(choices[1],"_rerollsRemaining")==remaining[1],"Stage "+stage+" reroll changes only one slot");
   int owned=manager.CurrentRun.ownedAugments.Count;
   ((UnityEngine.UI.Button)Get(choices[1],"selectButton")).onClick.Invoke();
   float until=UnityEngine.Time.realtimeSinceStartup+15;while(manager.CurrentRun.phase!=ContextStage.RunPhase.Map&&UnityEngine.Time.realtimeSinceStartup<until)yield return null;
   Check(manager.CurrentRun.phase==ContextStage.RunPhase.Map,"Stage "+stage+" reward travel returns to map");
   Check(manager.CurrentRun.ownedAugments.Count==owned+1,"Stage "+stage+" selected augment persists");
  }
  Check(manager.CurrentRun.phase==ContextStage.RunPhase.Completed,"boss result reaches completed tour");yield return FinishDialogue();
  Check(manager.CurrentRun.stageResults.Count==5&&manager.CurrentRun.ownedAugments.Count==4,"final run preserves five results and four rewards");
  Check(UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsSortMode.None).Any(b=>b.gameObject.activeInHierarchy&&b.name.Contains("Button")),"ending summary buttons exist");
  Save("complete");
 }finally{
  if(had)GameJamKit.Save.SetInt(scoreKey,previous);else GameJamKit.Save.Delete(scoreKey);GameJamKit.Save.Flush();
  UnityEditor.SessionState.SetBool("PolishScoreRestored",GameJamKit.Save.Has(scoreKey)==had&&GameJamKit.Save.GetInt(scoreKey)==previous);
 }
}
Save("starting");UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>().StartCoroutine(Test());return "Title-to-ending accelerated transition verification; no ranking submission";
