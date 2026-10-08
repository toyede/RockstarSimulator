var panel=UnityEngine.Object.FindFirstObjectByType<ContextStage.DialoguePanel>();
if(panel==null||!panel.IsPlaying)throw new System.InvalidOperationException("Dialogue not active");
panel.Skip();panel.Advance();
UnityEditor.SessionState.SetString("WorkBLoopTrace",UnityEditor.SessionState.GetString("WorkBLoopTrace","")+" -> Dialogue");
return "Skipped through actual dialogue/rule confirmation; waiting for scene transition.";
