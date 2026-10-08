var popup=UnityEngine.Object.FindFirstObjectByType<ContextStage.AugmentSelectionPopup>();
if(popup==null||!popup.IsVisible)throw new System.InvalidOperationException("Augment popup missing");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var slots=(ContextStage.AugmentChoiceView[])popup.GetType().GetField("choiceViews",flags).GetValue(popup);
UnityEditor.SessionState.SetInt("WorkBLoopRerollOther",(int)slots[1].GetType().GetField("_rerollsRemaining",flags).GetValue(slots[1]));
((UnityEngine.UI.Button)slots[0].GetType().GetField("rerollButton",flags).GetValue(slots[0])).onClick.Invoke();
return "Triggered slot 0 reroll through existing UI button.";
