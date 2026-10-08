if(UnityEngine.Application.isPlaying)throw new System.Exception("Edit mode only");
var style=UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.DialogueStyle>("Assets/Settings/Dialogue/DialogueStyle.asset");
var so=new UnityEditor.SerializedObject(style);
so.FindProperty("arrowOffset").vector2Value=new UnityEngine.Vector2(-80,42);
so.ApplyModifiedProperties();UnityEditor.AssetDatabase.SaveAssetIfDirty(style);
string path="Assets/Resources/Dialogue/DialoguePanel.prefab";
var root=UnityEditor.PrefabUtility.LoadPrefabContents(path);
try {
 var p=root.GetComponent<ContextStage.DialoguePanel>();
 var ps=new UnityEditor.SerializedObject(p);
 var arrow=(UnityEngine.UI.Image)ps.FindProperty("arrowImage").objectReferenceValue;
 arrow.rectTransform.anchoredPosition=new UnityEngine.Vector2(-80,42);
 UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,path);
} finally {UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
return "Arrow offsets include transparent gutter of existing dialogue art";
