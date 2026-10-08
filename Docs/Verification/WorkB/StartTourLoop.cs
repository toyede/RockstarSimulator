ContextStage.TutorialFlow.SuppressForTests=true;
var launcher=UnityEngine.Object.FindFirstObjectByType<ContextStage.TourTitleLauncher>();
if(launcher==null)throw new System.InvalidOperationException("Title launcher missing");
UnityEditor.SessionState.SetString("WorkBLoopTrace","Title");
UnityEditor.SessionState.SetInt("WorkBLoopDialogueCallbacks",0);
launcher.StartTour();
return "Started actual Title launcher; no score submitted to leaderboard.";
