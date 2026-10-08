var rows=new System.Collections.Generic.List<object>();
foreach(var p in UnityEngine.Object.FindObjectsByType<GameJamKit.UIPopup>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)){
 if(!(p is ContextStage.OptionsPopup)&&!(p is ContextStage.PausePopup))continue;
 var children=new System.Collections.Generic.List<object>();foreach(UnityEngine.Transform child in p.transform)children.Add(new{name=child.name,components=System.Array.ConvertAll(child.GetComponents<UnityEngine.Component>(),c=>c?.GetType().Name)});
 var so=new UnityEditor.SerializedObject(p);rows.Add(new{p.name,p.IsOpen,children,escape=so.FindProperty("closableByEscape").boolValue});
}
return rows;
