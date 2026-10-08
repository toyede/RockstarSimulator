UnityEditor.EditorApplication.isPaused=false;
ContextStage.TutorialFlow.SuppressForTests=true;
var gm=GameJamKit.GameManager.Instance;
gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage05Boss.asset"));
gm.StartGame();
ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
ContextStage.PerformanceTimer.SetPaused(true);
var arena=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossArenaLayout>();
var records=new System.Collections.Generic.List<string>();
System.Action<ContextStage.BossLightingHoldProgress> listener=e=>{
    if(e.Active)records.Add(e.Normalized.ToString("F2")+"@"+UnityEngine.Vector3.Distance(UnityEngine.Camera.main.transform.position,arena.AnchorOf(ContextStage.BossZone.RivalStage)+UnityEngine.Vector3.back*10).ToString("F2"));
};
GameJamKit.EventBus.Subscribe(listener);
GameJamKit.EventBus.Raise(new ContextStage.BossPatternAnnounced("drop","검증","드랍",false,1));
UnityEngine.Object.FindFirstObjectByType<ContextStage.BossStagePresentation>().PlayPatternAnnounce(()=>{
    GameJamKit.EventBus.Unsubscribe(listener);
    UnityEditor.SessionState.SetString("LightingBossHoldEvidence",string.Join(",",records));
    UnityEditor.SessionState.SetBool("LightingBossCallback",true);
});
return "Started actual presentation coroutine; cue is synthetic, gameplay mission unchanged.";
