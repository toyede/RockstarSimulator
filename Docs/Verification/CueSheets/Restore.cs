UnityEditor.EditorApplication.isPaused=false;
ContextStage.TutorialFlow.SuppressForTests=false;
ContextStage.PerformanceTimer.SetPaused(false);
if(ContextStage.AudienceRosterSystem.HasInstance)ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=false;
return "Verification-only flags restored; exit Play Mode next";
