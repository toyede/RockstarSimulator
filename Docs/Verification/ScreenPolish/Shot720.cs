if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play Main required");
UnityEditor.EditorApplication.isPaused=false;
int mode=UnityEditor.SessionState.GetInt("PolishShot",0);
int stage=mode==1?3:mode==3?5:4;
var gm=GameJamKit.GameManager.Instance;GameJamKit.UIManager.Instance.CloseAll();gm.ResetGame();
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(tutorial!=null)tutorial.enabled=false;
UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>().HideAll();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/"+(stage==5?"Stage05Boss":"Stage0"+stage)+".asset"));gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);var roster=ContextStage.AudienceRosterSystem.Instance;roster.SuppressEngagementDecay=true;
if(stage!=5)while(roster.Count<roster.Capacity)if(!roster.TryAddRandom(ContextStage.AudienceJoinReason.RuntimeCommand,out _))break;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
System.Collections.IEnumerator Shot(){
 yield return new UnityEngine.WaitForSeconds(.5f);
 if(mode==0){ContextStage.FeverSystem.Instance.ForceStart();UnityEngine.Object.FindFirstObjectByType<ContextStage.BigScreenRule>().DebugTriggerNow();}
 if(mode==1)GameJamKit.EventBus.Raise(new ContextStage.AudienceCrisisResolved(4,4,0));
 if(mode==2){var rule=UnityEngine.Object.FindFirstObjectByType<ContextStage.ArenaBlackoutRule>();rule.DebugTriggerNow();yield return new UnityEngine.WaitForSeconds(.3f);GameJamKit.EventBus.Raise(new GameJamKit.CardResolved{Judgement=GameJamKit.HypeJudgement.Good});typeof(ContextStage.StageEventRule).GetField("_remaining",flags).SetValue(rule,0f);var vfx=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventPresentationVFX>();float until=UnityEngine.Time.realtimeSinceStartup+3;while(UnityEngine.Time.realtimeSinceStartup<until){var smoke=(UnityEngine.ParticleSystem)vfx.GetType().GetField("_smoke",flags).GetValue(vfx);if(smoke!=null&&smoke.particleCount>0)break;yield return null;}}
 if(mode==3){GameJamKit.EventBus.Raise(new ContextStage.BossPatternStarted("guest_list","GUEST LIST","특별 관객의 요청을 성공시키세요. 카드를 누른 채 요청 관객에게 가져가세요.",8,false));GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted("stadium_contested_fan","스탠딩 팬 쟁탈","SINGALONG 관객을 사로잡아 라이벌에게 빼앗기지 마세요.",8));}
 yield return new UnityEngine.WaitForSeconds(.16f);UnityEngine.Time.timeScale=0;
 UnityEditor.SessionState.SetInt("PolishShot",(mode+1)%4);
 UnityEditor.SessionState.SetString("PolishShotState",Newtonsoft.Json.JsonConvert.SerializeObject(new{mode,width=UnityEngine.Screen.width,height=UnityEngine.Screen.height,roster=roster.Count,particles=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventPresentationVFX>().ParticleCount}));
}
UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>().StartCoroutine(Shot());return new{mode,temporaryFreeze=true};
