UnityEditor.EditorApplication.isPaused=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
ContextStage.TutorialFlow.SuppressForTests=true;
UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>().enabled=false;
UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>().HideAll();
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage05Boss.asset"));
gm.StartGame();
foreach(var rule in UnityEngine.Object.FindObjectsByType<ContextStage.StageEventRule>(UnityEngine.FindObjectsSortMode.None))rule.enabled=false;
UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleRule>().enabled=false;
var presentation=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossStagePresentation>();
presentation.GetType().GetMethod("RestoreImmediate",flags).Invoke(presentation,null);
presentation.GetType().GetMethod("ShowScoreHud",flags).Invoke(presentation,null);
ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var boss=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>();
System.Collections.IEnumerator Shot(){
 GameJamKit.EventBus.Raise(new ContextStage.BossFanBalanceChanged(14,6,20,false));
 GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted("stadium_contested_fan","스탠딩석 팬 쟁탈","SINGALONG 카드로 좋은 반응을 이끌어내세요.",8));
 GameJamKit.EventBus.Raise(new ContextStage.StageEventResolved("stadium_contested_fan","스탠딩석 팬 쟁탈",false,"SINGALONG 팬은 라이벌 무대로 갔습니다."));
 GameJamKit.EventBus.Raise(new ContextStage.BossPatternStarted("b2b","B2B","라이벌의 트랙에 답하라 — [무대 장악] 사용!",8,false));
 GameJamKit.EventBus.Raise(new ContextStage.BossPatternProgress("b2b",7.9f,8,"0 / 1",false));
 yield return new UnityEngine.WaitForSeconds(.15f);
 UnityEditor.EditorApplication.isPaused=true;
}
boss.StartCoroutine(Shot());return "Boss unified mission freeze requested";
