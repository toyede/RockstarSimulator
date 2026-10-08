UnityEditor.EditorApplication.isPaused=false;
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var gm=GameJamKit.GameManager.Instance;gm.ResetGame();
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage01.asset"));gm.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var boss=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>();if(boss!=null)boss.gameObject.SetActive(false); // isolate prior synthetic boss UI, not a scene edit
var presenter=UnityEngine.Object.FindFirstObjectByType<ContextStage.AudienceRosterPresenter>();
System.Collections.IEnumerator Shot()
{
    yield return new UnityEngine.WaitForSeconds(0.4f);
    var roster=ContextStage.AudienceRosterSystem.Instance;
    var first=roster.Members[0];roster.TrySetEngagement(first.Id,15,ContextStage.AudienceChangeReason.RuntimeCommand,out _);
    var actor=UnityEngine.Object.FindObjectsByType<ContextStage.AudienceMemberActor>(UnityEngine.FindObjectsSortMode.None).First(x=>x.BoundId.Equals(first.Id));
    actor.SetLayout(new UnityEngine.Vector3(-3f,0.3f,0),0.8f,5);
    yield return new UnityEngine.WaitForSeconds(0.5f);
    var fill=(UnityEngine.SpriteRenderer)actor.GetType().GetField("warningFillRenderer",flags).GetValue(actor);
    UnityEditor.SessionState.SetString("VfxBarShot",fill.color.ToString()+" bounds="+fill.bounds.ToString()+" layer="+fill.sortingLayerName+" order="+fill.sortingOrder+" material="+fill.sharedMaterial.name+" snapshot="+actor.Snapshot.Engagement);
    UnityEditor.EditorApplication.isPaused=true;
}
presenter.StartCoroutine(Shot());return "Isolated low-engagement bar shot: only actor presentation position moved during Play Mode.";
