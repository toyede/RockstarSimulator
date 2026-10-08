var b=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossStagePresentation>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
var button=(UnityEngine.UI.Button)b.GetType().GetField("_peekButton",flags).GetValue(b);
return new{b.IsPeeking,b.IsPlaying,b.IsReady,button.interactable,state=GameJamKit.GameManager.Instance.State.ToString(),scale=UnityEngine.Time.timeScale,zone=b.GetComponent<ContextStage.BossCameraDirector>().CurrentZone.ToString(),popup=GameJamKit.UIManager.Instance.TopPopup?.name};
