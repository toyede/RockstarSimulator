UnityEditor.EditorApplication.isPaused=false;
foreach(var d in UnityEngine.Object.FindObjectsByType<ContextStage.DialoguePanel>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))UnityEngine.Object.Destroy(d.gameObject);
UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>().HideAll();
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage05Boss.asset"));gm.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);
var boss=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var checks=new System.Collections.Generic.List<string>();
void Check(bool pass,string name){if(!pass)throw new System.Exception(name);checks.Add(name);}
GameJamKit.EventBus.Raise(new ContextStage.BossFanBalanceChanged(14,6,20,false));
GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted("stadium_contested_fan","스탠딩석 팬 쟁탈","스탠딩석의 팬이 SINGALONG을(를) 외칩니다! 그 성향 카드로 좋은 반응을!",8));
GameJamKit.EventBus.Raise(new ContextStage.StageEventProgress("stadium_contested_fan",7.9f,8,"요청: SINGALONG"));
GameJamKit.EventBus.Raise(new ContextStage.BossPatternStarted("b2b","B2B","라이벌의 트랙에 답하라 — [무대 장악] 사용!",8,false));
var panel=(UnityEngine.RectTransform)boss.GetType().GetField("_patternPanel",flags).GetValue(boss);
var view=panel.GetComponent<ContextStage.MissionCueSheetView>();UnityEngine.Canvas.ForceUpdateCanvases();
foreach(var t in new[]{view.Title,view.Body,view.Progress,view.Timer,view.Secondary}) {
 t.ForceMeshUpdate();Check(!t.isTextOverflowing&&t.preferredHeight<=t.rectTransform.rect.height+.1f,"dual mission fits: "+t.name);
}
gm.GameOver();Check(!panel.gameObject.activeSelf,"gameover clears mission");
gm.ResetGame();gm.StartGame();
GameJamKit.EventBus.Raise(new ContextStage.BossPatternStarted("b2b","B2B","조건",8,false));
boss.enabled=false;Check(!panel.gameObject.activeSelf,"disable clears mission");boss.enabled=true;
GameJamKit.EventBus.Raise(new ContextStage.BossPatternProgress("b2b",1,8,"stale",false));
Check(!panel.gameObject.activeSelf,"reenable does not restore stale objective");
GameJamKit.EventBus.Raise(new ContextStage.BossPatternStarted("b2b","B2B","조건",8,false));
Check(panel.gameObject.activeSelf,"reenabled subscriptions work");
return Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=checks.Count,checks});
