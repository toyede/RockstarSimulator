UnityEditor.EditorApplication.isPaused=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,n)=>{checks.Add((ok?"PASS ":"FAIL ")+n);UnityEditor.SessionState.SetString("VfxLifecycle",string.Join("\n",checks));};
UnityEditor.SessionState.SetBool("VfxLifecycleDone",false);
var gm=GameJamKit.GameManager.Instance;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();
ContextStage.TutorialFlow.SuppressForTests=true;tutorial.enabled=false;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage05Boss.asset"));gm.StartGame();
var presentation=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossStagePresentation>();presentation.GetType().GetMethod("RestoreImmediate",flags).Invoke(presentation,null);
ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var rival=UnityEngine.Object.FindFirstObjectByType<ContextStage.RivalStagePlaceholder>();
var camera=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossCameraDirector>();
System.Collections.IEnumerator Run()
{
    foreach(float ratio in new[]{0f,.33f,.3301f,.66f,.6601f,1f})
    {
        rival.GetType().GetMethod("SetAudienceStage",flags).Invoke(rival,new object[]{ratio});
        var expected=ratio<=.33f?ContextStage.AudienceEngagementStage.Calm:ratio<=.66f?ContextStage.AudienceEngagementStage.Middle:ContextStage.AudienceEngagementStage.Excited;
        check(rival.AudienceStage==expected,"exact boss HP boundary "+ratio);
    }
    GameJamKit.EventBus.Raise(new ContextStage.BossFanMoved(true,1,15,5));
    yield return new UnityEngine.WaitForSeconds(1.1f);
    var arrival=rival.GetComponent<ContextStage.RivalArrivalVFX>();
    check(arrival!=null,"real rival fan walk finishes with arrival cue");
    arrival.Emit(UnityEngine.Vector3.zero,1);
    var pixels=arrival.GetComponentsInChildren<UnityEngine.ParticleSystem>().First();
    check(pixels.particleCount>0&&pixels.particleCount<=48,"rival arrival shared bounded particles");
    gm.Pause();check(pixels.isPaused,"rival arrival respects pause");gm.Resume();
    gm.ResetGame();check(pixels.particleCount==0,"rival arrival cleared on restart");
    ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage03.asset"));gm.StartGame();
    ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
    var rain=UnityEngine.Object.FindFirstObjectByType<ContextStage.RainShowerRule>();
    typeof(ContextStage.StageEventRule).GetField("waitForSpecialRequest",flags).SetValue(rain,false);
    rain.DebugTriggerNow();yield return new UnityEngine.WaitForSeconds(0.4f);
    var view=rain.GetComponent<ContextStage.RainShowerVFX>();
    check(view.ParticleCount>0,"rain naturally emits over frames");
    var floor=view.GetComponentsInChildren<UnityEngine.ParticleSystem>().First(x=>x.name=="Rain_Floor");
    check(floor.main.maxParticles==12,"floor splash capacity 12");
    typeof(ContextStage.StageEventRule).GetMethod("Resolve",flags).Invoke(rain,new object[]{true});
    yield return new UnityEngine.WaitForSeconds(2.1f);
    check(view.ParticleCount==0,"normal rain ending leaves no particles");
    rain.DebugTriggerNow();yield return new UnityEngine.WaitForSeconds(0.4f);
    view.enabled=false;check(view.ParticleCount==0,"rain disabled clears all three layers");view.enabled=true;
    gm.ResetGame();check(!rain.IsEventActive&&view.ParticleCount==0,"rain cancellation restores clean state");
    UnityEditor.SessionState.SetBool("VfxLifecycleDone",true);
}
camera.StartCoroutine(Run());return "Started lifecycle and HP boundary tests.";
