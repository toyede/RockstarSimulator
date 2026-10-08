if(UnityEditor.EditorApplication.isPlaying) throw new System.InvalidOperationException("Edit Mode only");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.name!="Main") throw new System.InvalidOperationException("Main only");
var cameraSprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Sprites/0930_art/boss_camera.png");
var background=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Sprites/Stages/ST04_Arena/ST04_BG_Base.png");
if(cameraSprite==null||background==null)throw new System.InvalidOperationException("Required sprites missing");
int count=0;
foreach(var p in UnityEngine.Object.FindObjectsByType<ContextStage.BossStagePresentation>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)){
 UnityEditor.Undo.RecordObject(p,"Connect boss camera button");
 var so=new UnityEditor.SerializedObject(p);
 so.FindProperty("peekButtonSprite").objectReferenceValue=cameraSprite;
 so.FindProperty("peekLabel").stringValue="라이벌 무대  [Tab]";
 so.FindProperty("returnLabel").stringValue="우리 무대  [Tab]";
 so.ApplyModifiedProperties(); count++;
}
var catalog=ContextStage.StageVisualCatalog.LoadDefault();
if(!catalog.TryGet("stage_04",out var entry))throw new System.InvalidOperationException("Stage 4 missing");
string oldPath=UnityEditor.AssetDatabase.GetAssetPath(entry.backgroundBase);
UnityEditor.Undo.RecordObject(catalog,"Restore Stage 4 background");entry.backgroundBase=background;
UnityEditor.EditorUtility.SetDirty(catalog);
UnityEditor.AssetDatabase.SaveAssetIfDirty(catalog);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{bossComponents=count,button=UnityEditor.AssetDatabase.GetAssetPath(cameraSprite),stage4Before=oldPath,stage4After=UnityEditor.AssetDatabase.GetAssetPath(background)};
