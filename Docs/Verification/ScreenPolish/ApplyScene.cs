if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Edit Mode only");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!="Assets/Scenes/Main.unity")throw new System.Exception("Main only");
var ui=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>(UnityEngine.FindObjectsInactive.Include);
var presenter=UnityEngine.Object.FindFirstObjectByType<ContextStage.AudienceRosterPresenter>(UnityEngine.FindObjectsInactive.Include);
var notice=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventNoticeUI>(UnityEngine.FindObjectsInactive.Include);
if(ui==null||presenter==null||notice==null)throw new System.Exception("Expected hierarchy missing");
var boss=new UnityEditor.SerializedObject(ui);
foreach(var pair in new[]{("fanBarBack","boss_bar_back"),("fanBarOurs","boss_bar_me"),("fanBarRival","boss_bar_boss")}){
 var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Sprites/0930_art/"+pair.Item2+".png");
 if(sprite==null||sprite.texture.filterMode!=UnityEngine.FilterMode.Point)throw new System.Exception("Missing/NonPoint "+pair.Item2);
 boss.FindProperty(pair.Item1).objectReferenceValue=sprite;
}
boss.FindProperty("topOffset").floatValue=54;boss.ApplyModifiedProperties();
var layout=new UnityEditor.SerializedObject(presenter);layout.FindProperty("horizontalSpacing").floatValue=2.6f;layout.FindProperty("rowSpacing").floatValue=1f;layout.ApplyModifiedProperties();
if(notice.GetComponent<ContextStage.StageEventPresentationVFX>()==null)UnityEditor.Undo.AddComponent<ContextStage.StageEventPresentationVFX>(notice.gameObject);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{scene=scene.path,dirty=scene.isDirty,boss=ui.name,audience=presenter.name,eventRoot=notice.name};
