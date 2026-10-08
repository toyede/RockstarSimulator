var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>();
if(view==null)throw new System.InvalidOperationException("Newspaper missing");
view.CompleteImmediately();
var button=(UnityEngine.UI.Button)view.GetType().GetField("primaryButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(view);
button.onClick.Invoke();
UnityEditor.SessionState.SetString("WorkBLoopTrace",UnityEditor.SessionState.GetString("WorkBLoopTrace","")+" -> Augment");
return new{button=button.name,phase=ContextStage.TourRunManager.Instance.CurrentRun.phase.ToString()};
