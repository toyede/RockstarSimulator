var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(UnityEditor.EditorApplication.isPlaying || scene.path!="Assets/Scenes/Main.unity")throw new System.Exception("Main Edit Mode required");
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include);
var so=new UnityEditor.SerializedObject(view);
foreach(var field in new[]{"statCombo","statFever","statAudience"}){
 var t=(TMPro.TMP_Text)so.FindProperty(field).objectReferenceValue;
 UnityEditor.Undo.RecordObject(t.rectTransform,"Inset result statistic labels below newspaper rule");
 var p=t.rectTransform.anchoredPosition;p.y=-265;t.rectTransform.anchoredPosition=p;
 UnityEditor.EditorUtility.SetDirty(t.rectTransform);
}
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include);
var tso=new UnityEditor.SerializedObject(overlay);
var title=(UnityEngine.UI.Text)tso.FindProperty("mainText").objectReferenceValue;
UnityEditor.Undo.RecordObject(title.rectTransform,"Fit long tutorial title");
title.rectTransform.anchorMax=new UnityEngine.Vector2(.94f,.74f);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "Saved 10px statistic inset and taller tutorial title area";
