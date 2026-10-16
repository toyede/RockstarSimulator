if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play Main required");
UnityEditor.EditorApplication.isPaused=false;
var gm=GameJamKit.GameManager.Instance;GameJamKit.UIManager.Instance.CloseAll();gm.ResetGame();
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(flow!=null)flow.enabled=false;
UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>().HideAll();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage04.asset"));gm.StartGame();ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
while(ContextStage.AudienceRosterSystem.Instance.Count<ContextStage.AudienceRosterSystem.Instance.Capacity)if(!ContextStage.AudienceRosterSystem.Instance.TryAddRandom(ContextStage.AudienceJoinReason.RuntimeCommand,out _))break;
var vfx=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventPresentationVFX>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var wave=(System.Action)System.Delegate.CreateDelegate(typeof(System.Action),vfx,vfx.GetType().GetMethod("RecoveryWave",flags));
var results=new System.Collections.Generic.List<object>();
System.Collections.IEnumerator Test(){
 yield return new UnityEngine.WaitForSeconds(.5f);wave();yield return new UnityEngine.WaitForSeconds(1f);
 long bytes=0;double milliseconds=0;
 for(int pass=0;pass<2;pass++){
  vfx.Clear();vfx.enabled=pass==1;
  var frames=new System.Collections.Generic.List<double>(180);var allocations=new System.Collections.Generic.List<long>(180);
  using(var gc=Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory,"GC Allocated In Frame",1)){
   for(int frame=0;frame<180;frame++){
    if(pass==1&&frame%30==0){long start=System.GC.GetAllocatedBytesForCurrentThread();long tick=System.Diagnostics.Stopwatch.GetTimestamp();wave();milliseconds+=(System.Diagnostics.Stopwatch.GetTimestamp()-tick)*1000.0/System.Diagnostics.Stopwatch.Frequency;bytes+=System.GC.GetAllocatedBytesForCurrentThread()-start;}
    yield return null;frames.Add(UnityEngine.Time.unscaledDeltaTime*1000.0);if(gc.Valid)allocations.Add(gc.LastValue);
   }
   frames.Sort();allocations.Sort();results.Add(new{enabled=pass==1,frames=frames.Count,frameMedianMs=frames[90],frameP95Ms=frames[171],gcRecorderValid=gc.Valid,gcMedianBytes=allocations.Count>0?allocations[allocations.Count/2]:-1,gcMaxBytes=allocations.Count>0?allocations.Last():-1});
  }
 }
 vfx.enabled=true;vfx.Clear();UnityEditor.SessionState.SetString("PolishPerformance",Newtonsoft.Json.JsonConvert.SerializeObject(new{scope="Editor 1920x1080, Stage4 max audience, 180 frames per phase, six warm wave calls; whole Editor GC includes other systems",results,waveCalls=6,waveTotalMs=milliseconds,waveAllocatedBytes=bytes}));
}
UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>().StartCoroutine(Test());return "Bounded Editor measurement started; not a WebGL FPS guarantee";
