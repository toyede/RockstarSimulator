UnityEditor.EditorApplication.isPaused=false;
var runner=UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>();
var report=Newtonsoft.Json.Linq.JObject.Parse(UnityEditor.SessionState.GetString("TourSkipVerification","{}"));
var checks=report["checks"].Values<string>().Where(s=>!s.StartsWith("FAIL normal path")).ToList();
System.Collections.IEnumerator Finish(){
 float deadline=UnityEngine.Time.realtimeSinceStartup+30f;
 while(GameJamKit.SceneLoader.IsLoading&&UnityEngine.Time.realtimeSinceStartup<deadline)yield return null;
 yield return null;
 checks.Add((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Main"?"PASS ":"FAIL ")+"normal path reaches Main (wait for async completion)");
 checks.Add((ContextStage.StageRuntimeDirector.CurrentStage!=null&&ContextStage.StageRuntimeDirector.CurrentStage.StageId=="stage_02"?"PASS ":"FAIL ")+"normal path applies chosen stage");
 UnityEditor.SessionState.SetString("TourSkipVerification",Newtonsoft.Json.JsonConvert.SerializeObject(new{pass=checks.Count(s=>s.StartsWith("PASS")),fail=checks.Count(s=>s.StartsWith("FAIL")),checks}));
 UnityEditor.EditorApplication.isPaused=true;
}
runner.StartCoroutine(Finish());return "Waiting for actual normal async scene activation";
