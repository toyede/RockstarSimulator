var popup=UnityEngine.Object.FindFirstObjectByType<ContextStage.AugmentSelectionPopup>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var slots=(ContextStage.AugmentChoiceView[])popup.GetType().GetField("choiceViews",flags).GetValue(popup);
int first=(int)slots[0].GetType().GetField("_rerollsRemaining",flags).GetValue(slots[0]);
int other=(int)slots[1].GetType().GetField("_rerollsRemaining",flags).GetValue(slots[1]);
bool isolated=first==0&&other==UnityEditor.SessionState.GetInt("WorkBLoopRerollOther",-1);
UnityEditor.SessionState.SetBool("WorkBLoopRerollIsolated",isolated);
((UnityEngine.UI.Button)slots[2].GetType().GetField("selectButton",flags).GetValue(slots[2])).onClick.Invoke();
return new{isolatedReroll=isolated,first,other,selected=2};
