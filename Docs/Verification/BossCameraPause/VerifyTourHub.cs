if(!UnityEditor.EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="TourHub")throw new System.Exception("Play in TourHub required");
UnityEditor.EditorApplication.isPaused=false;
GameJamKit.GameManager.Instance.ResetGame();
var manager=ContextStage.TourRunManager.Instance;
var stages=new[]{UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage01.asset"),UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage02.asset")};
manager.StartNewRun(stages,12345);
var runner=UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>();
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("TourVerificationKeyboard");
var checks=new System.Collections.Generic.List<string>();
void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);UnityEditor.SessionState.SetString("TourSkipVerification",Newtonsoft.Json.JsonConvert.SerializeObject(new{checks}));}
System.Collections.IEnumerator F10(){UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F10));yield return null;UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());yield return new UnityEngine.WaitForSecondsRealtime(.5f);}
System.Collections.IEnumerator Test(){
 yield return new UnityEngine.WaitForSecondsRealtime(.6f);
 yield return F10();Check(manager.CurrentRun.phase==ContextStage.RunPhase.Dialogue,"F10 map selects next node");
 yield return F10();Check(manager.CurrentRun.phase==ContextStage.RunPhase.Result,"F10 dialogue skips performance to result");
 Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TourHub"&&!GameJamKit.SceneLoader.IsLoading,"debug skip never starts scene load");
 Check(!ContextStage.TourDebugInput.SkippingPerformance,"skip guard released after transition");
 Check(manager.CurrentRun.stageResults.Count==1&&manager.CurrentRun.LatestStageResult.score==stages[0].TargetScore,"one synthetic result at target score");
 yield return new UnityEngine.WaitForSecondsRealtime(1f);
 Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TourHub"&&manager.CurrentRun.phase==ContextStage.RunPhase.Result,"no delayed dialogue callback loads Main");
 yield return F10();Check(manager.CurrentRun.phase==ContextStage.RunPhase.Reward,"F10 result opens augment reward");
 manager.StartNewRun(new[]{stages[1],stages[0]},54321);yield return new UnityEngine.WaitForSecondsRealtime(.3f);
 yield return F10();yield return F10();
 Check(manager.CurrentRun.phase==ContextStage.RunPhase.Result&&manager.CurrentRun.LatestStageResult.stageId==stages[1].StageId,"another stage skips with correct identity");
 Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TourHub"&&!GameJamKit.SceneLoader.IsLoading,"repeated skip still stays in hub");
 UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
 manager.StartNewRun(new[]{stages[1],stages[0]},45678);
 ContextStage.TourDebugInput.AdvanceTourStep();
 ContextStage.Dialogue.HideImmediate();
 manager.CompleteDialogue();
 Check(GameJamKit.SceneLoader.IsLoading,"normal dialogue completion still loads performance");
 float deadline=UnityEngine.Time.realtimeSinceStartup+30f;
 while(GameJamKit.SceneLoader.IsLoading&&UnityEngine.Time.realtimeSinceStartup<deadline)yield return null;
 yield return null;
 Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main","normal path reaches Main");
 Check(ContextStage.StageRuntimeDirector.CurrentStage!=null&&ContextStage.StageRuntimeDirector.CurrentStage.StageId==stages[1].StageId,"normal path applies chosen stage");
 UnityEditor.SessionState.SetString("TourSkipVerification",Newtonsoft.Json.JsonConvert.SerializeObject(new{pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),checks}));
 UnityEditor.EditorApplication.isPaused=true;
}
runner.StartCoroutine(Test());return "Real F10 input, repeated hub skip and normal scene entry verification started";
