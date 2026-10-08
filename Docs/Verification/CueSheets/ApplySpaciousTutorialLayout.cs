var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(UnityEditor.EditorApplication.isPlaying || scene.path!="Assets/Scenes/Main.unity")throw new System.Exception("Main Edit Mode required");
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include);
var so=new UnityEditor.SerializedObject(overlay);
so.FindProperty("titleFontSize").intValue=28;so.FindProperty("titleMinFontSize").intValue=24;
so.FindProperty("bodyFontSize").intValue=22;so.FindProperty("bodyMinFontSize").intValue=20;
so.FindProperty("continueFontSize").intValue=20;so.ApplyModifiedProperties();
foreach(var field in new[]{"mainText","subText"}){
 var text=(UnityEngine.UI.Text)so.FindProperty(field).objectReferenceValue;
 UnityEditor.Undo.RecordObject(text,"Spacious tutorial typography");UnityEditor.Undo.RecordObject(text.rectTransform,"Tutorial reading margins");
 bool title=field=="mainText";
 text.rectTransform.anchorMin=new UnityEngine.Vector2(.10f,title?.49f:.16f);
 text.rectTransform.anchorMax=new UnityEngine.Vector2(.90f,title?.70f:.43f);
 text.rectTransform.offsetMin=text.rectTransform.offsetMax=UnityEngine.Vector2.zero;
 text.fontSize=text.resizeTextMaxSize=title?28:22;text.resizeTextMinSize=title?24:20;
 text.lineSpacing=1.15f;UnityEditor.EditorUtility.SetDirty(text);
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "Saved smaller tutorial fonts and wider inner margins; paper size and position preserved";
