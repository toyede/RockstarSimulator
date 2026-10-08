if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play Mode required");
UnityEditor.EditorApplication.isPaused=false;ContextStage.TutorialFlow.SuppressForTests=true;
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage02.asset"));gm.StartGame();
flow.StartTutorial();var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
flow.GetType().GetMethod("EnterSpecialUse",flags).Invoke(flow,null);
System.Collections.IEnumerator Test(){yield return new UnityEngine.WaitForSecondsRealtime(.7f);
var guide=flow.GetType().GetField("_dragGuide",flags).GetValue(flow) as ContextStage.TutorialDragGuideUI;
var source=guide.GetType().GetField("_source",flags).GetValue(guide) as ContextStage.CardDragHandler;
var target=ContextStage.SpecialAudience.CurrentDropTarget;
var root=(UnityEngine.RectTransform)guide.GetType().GetField("_root",flags).GetValue(guide);
var hint=(UnityEngine.UI.Text)guide.GetType().GetField("_hint",flags).GetValue(guide);
var checks=new[]{source!=null&&source.Card.Role==ContextStage.CardRole.Special&&source.Card.TargetPreference==ContextStage.CrowdPreference.Singalong,target!=null&&target.IsActive&&target.HitCollider!=null,root.gameObject.activeSelf&&!root.GetComponent<UnityEngine.CanvasGroup>().blocksRaycasts,hint.text=="여기로 전달",guide.GetType().GetField("_inspectionActor",flags).GetValue(guide)==null};
UnityEditor.SessionState.SetString("SpecialDragRegression",Newtonsoft.Json.JsonConvert.SerializeObject(new{pass=checks.Count(x=>x),fail=checks.Count(x=>!x),checks}));UnityEditor.EditorApplication.isPaused=true;}
flow.StartCoroutine(Test());return "Stage 2 special drag regression started";
