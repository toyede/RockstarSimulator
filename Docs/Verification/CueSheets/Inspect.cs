var scenes = new System.Collections.Generic.List<object>();
for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) {
 var s=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
 scenes.Add(new {s.path,s.isDirty,roots=System.Array.ConvertAll(s.GetRootGameObjects(),g=>g.name)});
}
var result=new System.Collections.Generic.List<object>();
foreach(var c in UnityEngine.Object.FindObjectsByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {
 var so=new UnityEditor.SerializedObject(c); var p=so.FindProperty("panelRoot").objectReferenceValue as UnityEngine.GameObject;
 var r=p.GetComponent<UnityEngine.RectTransform>(); result.Add(new {type="Tutorial",root=p.name,r.anchorMin,r.anchorMax,r.anchoredPosition,r.sizeDelta});
}
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Resources/Dialogue/DialoguePanel.prefab");
var arrow=prefab.GetComponentInChildren<ContextStage.DialoguePanel>(true);
var ar=new UnityEditor.SerializedObject(arrow).FindProperty("arrowImage").objectReferenceValue as UnityEngine.UI.Image;
return Newtonsoft.Json.JsonConvert.SerializeObject(new {scenes,result,arrow=new {ar.rectTransform.anchorMin,ar.rectTransform.anchorMax,ar.rectTransform.pivot,ar.rectTransform.anchoredPosition, parent=ar.transform.parent.name}}, new Newtonsoft.Json.JsonSerializerSettings {ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore});
