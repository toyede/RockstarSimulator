if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play required");
UnityEditor.EditorApplication.isPaused=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
object Get(object obj,string key)=>obj.GetType().GetField(key,flags).GetValue(obj);
var checks=new System.Collections.Generic.List<string>();
void Save(string state)=>UnityEditor.SessionState.SetString("ScreenPolishVerification",Newtonsoft.Json.JsonConvert.SerializeObject(new{state,pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),checks}));
void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);Save("running");}
var gm=GameJamKit.GameManager.Instance;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(tutorial!=null)tutorial.enabled=false;
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>();overlay.HideAll();
var ui=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>();
var vfx=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventPresentationVFX>();
var notice=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventNoticeUI>();
var score=UnityEngine.Object.FindFirstObjectByType<ContextStage.ScoreUI>();
var presenter=UnityEngine.Object.FindFirstObjectByType<ContextStage.AudienceRosterPresenter>();
void Apply(int n){gm.ResetGame();ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/"+(n==5?"Stage05Boss":"Stage0"+n)+".asset"));gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;}
System.Collections.IEnumerator Test(){
 Apply(2);yield return null;overlay.SetSpecialLessonLayout(true);overlay.ShowMessage("특별 관객의 요청입니다.","머리 위 요청 아이콘과 같은 SPECIAL 카드를 확인하세요.",true);
 Check(((UnityEngine.UI.Text)Get(overlay,"mainText")).color==UnityEngine.Color.black,"tutorial title black");
 Check(((UnityEngine.UI.Text)Get(overlay,"subText")).color==UnityEngine.Color.black,"tutorial body black");
 var hint=(UnityEngine.GameObject)Get(overlay,"continueHint");Check(hint.transform.parent==overlay.PanelImage.transform,"continue hint inside paper");
 Check(((UnityEngine.UI.Text)Get(overlay,"mainText")).text.Contains("<color="),"tutorial retains readable emphasis");overlay.HideAll();
 yield return new UnityEngine.WaitForSeconds(.5f);
 var actors=UnityEngine.Object.FindObjectsByType<ContextStage.AudienceMemberActor>(UnityEngine.FindObjectsSortMode.None).Where(a=>a.IsBound).ToArray();
 float span=actors.Max(a=>a.LayoutLocalPosition.x)-actors.Min(a=>a.LayoutLocalPosition.x);
 Check(span>=10f,"audience uses over 10 world units horizontally");
 Check(actors.All(a=>UnityEngine.Mathf.Abs(a.LayoutLocalPosition.x)<=6.21f),"audience remains inside stage width");
 var roster=ContextStage.AudienceRosterSystem.Instance;var pairs=actors.ToDictionary(a=>a.BoundId.Value,a=>a.LayoutLocalPosition);
 presenter.GetType().GetMethod("Relayout",flags).Invoke(presenter,null);
 Check(actors.All(a=>pairs[a.BoundId.Value]==a.LayoutLocalPosition),"layout jitter stable on repeat");
 Check(actors.Select(a=>a.LayoutLocalPosition.y).Distinct().Count()>1,"audience no longer rigid row");
 foreach(int stage in new[]{3,4}){
  Apply(stage);yield return null;string id=stage==3?"festival_flag_wave":"arena_big_screen";
  int population=roster.Count,points=gm.Score;
  GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted(id,"카메라 큐!","큐시트 순서대로 카드를 사용하세요: CHILL → MOSH → SINGALONG",8));
  yield return new UnityEngine.WaitForSeconds(.1f);int burst=vfx.BurstCount;
  Check(vfx.ParticleCount>0,"stage "+stage+" real particle burst visible");
  GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted(id,"카메라 큐!","큐시트 순서대로 카드를 사용하세요: CHILL → MOSH → SINGALONG",8));
  Check(vfx.BurstCount==burst,"stage "+stage+" duplicate start no second burst");
  var panel=(UnityEngine.RectTransform)Get(notice,"_panel");
  Check(panel.anchorMin==new UnityEngine.Vector2(0,.5f),"stage "+stage+" mission left middle");
  var body=((ContextStage.MissionCueSheetView)Get(notice,"_view")).Body;
  body.ForceMeshUpdate();Check(body.textBounds.size.y<=body.rectTransform.rect.height+2f,"stage "+stage+" body fits");
  gm.TogglePause();int before=vfx.ParticleCount;yield return new UnityEngine.WaitForSecondsRealtime(.15f);
  Check(vfx.ParticleCount==before,"stage "+stage+" particles pause");
  Check(!((UnityEngine.Canvas)Get(notice,"_canvas")).gameObject.activeSelf,"stage "+stage+" mission hidden in pause");
  gm.TogglePause();GameJamKit.EventBus.Raise(new ContextStage.StageEventResolved(id,"카메라 큐",true,"실제 성공 결과"));yield return null;
  Check(vfx.BurstCount==burst+1,"stage "+stage+" success burst once");
  Check(vfx.ParticleCount<=116,"stage "+stage+" world particle cap");
  Check(roster.Count==population&&gm.Score==points,"stage "+stage+" display events do not grant reward");
  yield return new UnityEngine.WaitForSeconds(1f);Check(vfx.ParticleCount==0,"stage "+stage+" burst settles under one second");
 }
 Apply(4);yield return null;
 GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted("arena_blackout","정전","기억하세요",8));yield return null;
 Check(vfx.ParticleCount==0,"blackout start no glowing burst");
 GameJamKit.EventBus.Raise(new ContextStage.StageEventResolved("arena_blackout","정전",true,"복구"));yield return null;
 Check(vfx.ParticleCount>0,"power restored spark burst");
 Apply(5);yield return null;
 Check((float)Get(ui,"_targetRivalRatio")<1f,"boss initial balance survives stage/Ready reset ordering");
 var presentation=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossStagePresentation>();presentation.enabled=false;
 foreach(var balance in new[]{new ContextStage.BossFanBalanceChanged(0,20,20,false),new ContextStage.BossFanBalanceChanged(10,10,20,false),new ContextStage.BossFanBalanceChanged(20,0,20,false),new ContextStage.BossFanBalanceChanged(0,0,0,false)}){
  GameJamKit.EventBus.Raise(balance);yield return new UnityEngine.WaitForSeconds(.22f);
  var ours=(UnityEngine.UI.Image)Get(ui,"_ourFill");var rival=(UnityEngine.UI.Image)Get(ui,"_rivalFill");
  Check(UnityEngine.Mathf.Abs(rival.fillAmount-balance.RivalRatio)<.03f,"rival ratio "+balance.Ours+":"+balance.Rival);
  Check(balance.Total==0?ours.fillAmount==0:UnityEngine.Mathf.Abs(ours.fillAmount+ rival.fillAmount-1)<.001f,"shared fill boundary "+balance.Total);
  Check(ours.fillOrigin==0&&rival.fillOrigin==1,"ours left rival right");
 }
 var back=(UnityEngine.UI.Image)Get(ui,"_barBack");Check(back.sprite.name=="boss_bar_back"&&back.sprite.texture.filterMode==UnityEngine.FilterMode.Point,"boss bar asset and Point filter");
 Check(back.rectTransform.sizeDelta==new UnityEngine.Vector2(606,30),"boss bar native integer scale");
 GameJamKit.EventBus.Raise(new ContextStage.BossDrainCountdown(15,168,true));Check(((TMPro.TMP_Text)Get(ui,"_drainText")).text.Contains("점수 감소"),"countdown describes score not fans");
 score.GetType().GetField("_lastCardPresentationAt",flags).SetValue(score,UnityEngine.Time.unscaledTime);
 gm.SetScore(300);gm.SetScore(200);yield return new UnityEngine.WaitForSecondsRealtime(.15f);
 Check(((UnityEngine.UI.Text)Get(score,"scoreText")).text.Contains("200"),"delayed score never rolls back to old value");
 GameJamKit.EventBus.Raise(new ContextStage.BossDrainApplied(168,14,15));yield return null;
 var loss=(UnityEngine.UI.Text)Get(score,"_drainText");Check(loss!=null&&loss.text.Contains("168"),"actual drain shown beside score");
 Check(!((TMPro.TMP_Text)Get(ui,"_drainPopup")).gameObject.activeSelf,"no duplicate top drain popup");
 Check(gm.Score==200,"drain presentation cannot change score");
 gm.TogglePause();float age=(float)Get(score,"_drainAge");yield return new UnityEngine.WaitForSecondsRealtime(.15f);Check((float)Get(score,"_drainAge")==age,"loss feedback pauses");gm.TogglePause();
 yield return new UnityEngine.WaitForSeconds(.85f);Check(!loss.gameObject.activeSelf,"loss feedback restores");
 GameJamKit.EventBus.Raise(new ContextStage.BossDrainApplied(0,14,15));Check(!loss.gameObject.activeSelf,"zero loss no fake damage");
 presentation.enabled=true;
 gm.ResetGame();Check(vfx.ParticleCount==0,"ready clears event particles");
 Save("complete");
}
Save("starting");UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>().StartCoroutine(Test());return "Started screen polish isolation checks";
