UnityEditor.EditorApplication.isPaused=false;
UnityEngine.Time.timeScale=1;
System.Collections.IEnumerator Freeze(){
 var vfx=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventPresentationVFX>();vfx.Clear();
 string id=ContextStage.StageRuntimeDirector.CurrentStage.StageId=="stage_03"?"festival_flag_wave":"arena_big_screen";
 GameJamKit.EventBus.Raise(new ContextStage.StageEventStarted(id,"카메라 큐!","큐시트 순서대로 카드를 쓰세요: CHILL → MOSH → SINGALONG",8));
 yield return new UnityEngine.WaitForSeconds(.12f);UnityEngine.Time.timeScale=0;
}
UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>().StartCoroutine(Freeze());
return "Temporary screenshot freeze without hiding HUD; reset before gameplay checks";
