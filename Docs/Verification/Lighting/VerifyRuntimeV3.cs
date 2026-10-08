if (!UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Play Mode required");
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,label)=>{checks.Add((ok?"PASS ":"FAIL ")+label);};
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var director=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageShowDirector>();
var rigs=UnityEngine.Object.FindObjectsByType<ContextStage.StageLightingRig>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
var gm=GameJamKit.GameManager.Instance;
ContextStage.TutorialFlow.SuppressForTests=true;
gm.ResetGame();
gm.StartGame();
ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
ContextStage.PerformanceTimer.SetPaused(true);
System.Func<ContextStage.StageLightingRig,string,UnityEngine.Object> field=(rig,name)=>(UnityEngine.Object)rig.GetType().GetField(name,flags).GetValue(rig);
for(int n=1;n<=5;n++)
{
    var stage=UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage0"+n+(n==5?"Boss":"")+".asset");
    ContextStage.StageRuntimeDirector.Active.ApplyStage(stage);
    check(rigs.Count(r=>r.gameObject.activeInHierarchy)==(n==5?2:1),"stage "+n+" active rig count");
    foreach(var rig in rigs.Where(r=>r.gameObject.activeInHierarchy))
    {
        rig.Render(5,1.3f,true,true,"drop",false,true,false,true,1,1,false,null,UnityEngine.Color.cyan,1,13,1);
        check(rig.GetComponentsInChildren<ContextStage.PixelLaserRenderer>().All(b=>!b.GetComponent<UnityEngine.MeshRenderer>().enabled),"blackout all beams "+rig.name);
        check(rig.GetComponentsInChildren<UnityEngine.SpriteRenderer>().All(s=>s.color.a<0.001f || s.color==UnityEngine.Color.black),"blackout fixtures/wash/LED "+rig.name);
        check(rig.ParticleCount==0,"blackout smoke "+rig.name);
        rig.Render(5,1,false,false,null,false,false,false,true,0,0,false,null,UnityEngine.Color.cyan,0,13,-1);
        check(rig.GetComponentsInChildren<ContextStage.PixelLaserRenderer>().All(b=>b.GetComponent<UnityEngine.MeshRenderer>().sortingLayerName=="Effects"),"world lighting layer "+rig.name);
        if(rig.Profile.venue==2)
        {
            var wash=(UnityEngine.SpriteRenderer)field(rig,"audienceWash");
            var baseAlpha=wash.color.a;
            rig.Render(5,1,false,false,null,false,false,false,true,0,0,false,null,UnityEngine.Color.cyan,1,13,-1);
            check(wash.color.a>baseAlpha && wash.color.g==1 && wash.color.b==1,"basement wash uses hint and pulse");
        }
        if(rig.Profile.venue==3)
        {
            var sweep=(ContextStage.PixelLaserRenderer)field(rig,"audienceSweep");
            rig.Render(5,1,false,false,null,false,false,false,true,0,0,false,null,UnityEngine.Color.cyan,0,1,-1);
            check(!sweep.GetComponent<UnityEngine.MeshRenderer>().enabled,"festival no initial sweep");
            rig.Render(5,1,false,false,null,false,false,false,true,0,0,false,null,UnityEngine.Color.cyan,0,13,-1);
            check(sweep.GetComponent<UnityEngine.MeshRenderer>().enabled,"festival scheduled sweep");
        }
        if(rig.Profile.venue==4)
        {
            var center=(ContextStage.PixelLaserRenderer)field(rig,"studioCenter");
            rig.Render(5,1,false,false,null,false,false,false,true,0,0,false,null,UnityEngine.Color.cyan,0,.6f,-1);
            check(center.GetComponent<UnityEngine.MeshRenderer>().enabled,"studio center second step");
            rig.Render(5,1,false,false,null,false,false,false,true,0,0,false,null,UnityEngine.Color.cyan,0,2,-1);
            check(!center.GetComponent<UnityEngine.MeshRenderer>().enabled,"studio center settles");
        }
        if(rig.Profile.rival)
        {
            var lasers=(ContextStage.PixelLaserRenderer[])rig.GetType().GetField("lasers",flags).GetValue(rig);
            check(lasers.Count(l=>l.GetComponent<UnityEngine.MeshRenderer>().enabled)==4,"boss normal 4 lasers");
            rig.Render(5,1,false,true,"drop",false,false,false,true,0,0,false,null,null,0,13,.2f);
            check(lasers.All(l=>!l.GetComponent<UnityEngine.MeshRenderer>().enabled),"boss pre-drop quiet");
            rig.Render(5,1,false,true,"drop",false,false,false,true,0,0,false,null,null,0,13,1);
            check(lasers.Count(l=>l.GetComponent<UnityEngine.MeshRenderer>().enabled)==8,"boss drop reveal 8 lasers");
            rig.Render(5,1,false,false,"drop",false,false,false,true,0,0,false,null,null,0,13,-1);
            check(lasers.Count(l=>l.GetComponent<UnityEngine.MeshRenderer>().enabled)==6,"boss active drop 6 lasers");
        }
    }
}
var hint=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageLightController>();
hint.SetBlackout(true);
foreach(var fever in UnityEngine.Object.FindObjectsByType<ContextStage.FeverSpotlight>(UnityEngine.FindObjectsSortMode.None))
{
    fever.GetType().GetMethod("ApplyLight",flags).Invoke(fever,new object[]{1f});
    check(fever.GetComponent<UnityEngine.Rendering.Universal.Light2D>().intensity==0,"blackout Fever priority");
}
hint.SetBlackout(false);
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage04.asset"));
director.GetType().GetField("_time",flags).SetValue(director,20f);
GameJamKit.EventBus.Raise(new GameJamKit.CardResolved{CardId="guitar_solo"});
check((float)director.GetType().GetField("_rimStart",flags).GetValue(director)==20,"studio real card event rim trigger");
gm.Pause();
float frozen=director.ShowTime;
director.GetType().GetMethod("LateUpdate",flags).Invoke(director,null);
check(director.ShowTime==frozen,"pause freezes show clock");
gm.Resume();
gm.ResetGame();
check(director.PerformanceShowAge==-1,"restart resets startup sequence");
check((float)director.GetType().GetField("_rimStart",flags).GetValue(director)==-100,"restart clears rim cue");
var lux=rigs.First(r=>r.Profile.rival);
for(int i=0;i<100;i++)lux.EmitSmoke(100+i,4);
check(lux.ParticleCount<=lux.GetComponentsInChildren<UnityEngine.ParticleSystem>().Sum(p=>p.main.maxParticles),"bounded fog");
for(int i=0;i<3;i++){director.enabled=false;director.enabled=true;}
check(lux.ParticleCount==0,"disable/re-enable clears fog");
var rims=UnityEngine.Object.FindObjectsByType<ContextStage.BandRimLightView>(UnityEngine.FindObjectsSortMode.None);
check(rims.Length>=2 && rims.All(r=>r.transform.Cast<UnityEngine.Transform>().Count(c=>c.name=="[Band Rim Light]")==1),"persistent active rim overlays not duplicated");
check(UnityEngine.Object.FindObjectsByType<ContextStage.AudienceReactionPopup>(UnityEngine.FindObjectsSortMode.None).All(p=>p.GetComponentInChildren<TMPro.TextMeshPro>().renderer.sortingLayerName=="Effects"),"reaction text above lighting");
return new {passed=checks.Count(c=>c.StartsWith("PASS")),failed=checks.Count(c=>c.StartsWith("FAIL")),checks};
