if(!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Play Mode required");
UnityEditor.EditorApplication.isPaused=false;
ContextStage.TutorialFlow.SuppressForTests=true;
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>();overlay.SetSpecialLessonLayout(false);overlay.HideAll();
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
var stage=UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage04.asset");
ContextStage.StageRuntimeDirector.Active.ApplyStage(stage);gm.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var report=new ContextStage.PerformanceReport{targetScore=stage.TargetScore,maxCombo=24,feverCount=3,audienceRemaining=10,audienceCapacity=12,audiencePeak=12,specialSuccess=3,specialEnded=4};
var result=new ContextStage.StageResult("readability",stage.StageId,true,16500,"B",24){report=report};
var data=ContextStage.ResultHeadlineSelector.Compose(result,stage,4,ContextStage.ResultNextAction.Augment,ContextStage.ResultNewspaperCatalog.LoadDefault());
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include);
view.Show(data,null);view.CompleteImmediately();
System.Collections.IEnumerator Shot(){yield return new UnityEngine.WaitForSecondsRealtime(.2f);UnityEditor.EditorApplication.isPaused=true;}
view.StartCoroutine(Shot());return "Actual Main result UI with Pitchfur skin; synthetic report, no score saving";
