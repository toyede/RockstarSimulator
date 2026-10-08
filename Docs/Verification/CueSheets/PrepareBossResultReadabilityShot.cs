if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play Mode required");
UnityEditor.EditorApplication.isPaused=false;
ContextStage.TutorialFlow.SuppressForTests=true;
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>();overlay.SetSpecialLessonLayout(false);overlay.HideAll();
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
var stage=UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage05Boss.asset");
ContextStage.StageRuntimeDirector.Active.ApplyStage(stage);gm.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var report=new ContextStage.PerformanceReport{targetScore=stage.TargetScore,maxCombo=123,feverCount=12,audienceRemaining=18,audienceCapacity=20,audiencePeak=20,stageEventsSucceeded=2,stageEventsTotal=3,isBoss=true,bossOutcome=ContextStage.BossOutcome.Won,bossPatternsSucceeded=3,bossPatternsResolved=5,bossFansRecruited=8,bossFansLost=3,bossDrainTotal=4000};report.scoreByPreference[1]=23456;
var result=new ContextStage.StageResult("readability",stage.StageId,true,23456,"A",123){report=report};
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include);
view.Show(ContextStage.ResultHeadlineSelector.Compose(result,stage,5,ContextStage.ResultNextAction.Ending,ContextStage.ResultNewspaperCatalog.LoadDefault()),null);view.CompleteImmediately();
System.Collections.IEnumerator Shot(){yield return new UnityEngine.WaitForSecondsRealtime(.2f);UnityEditor.EditorApplication.isPaused=true;}
view.StartCoroutine(Shot());return "Boss long report, verdict moved to subtitle; preview only, no score saving";
