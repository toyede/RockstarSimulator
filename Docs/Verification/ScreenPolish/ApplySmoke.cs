if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Edit only");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!="Assets/Scenes/Main.unity"||scene.isDirty)throw new System.Exception("Clean Main required");
var vfx=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventPresentationVFX>(UnityEngine.FindObjectsInactive.Include);
var sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/_Project/Sprites/VFX/Particle/steam_wispy.png");
if(vfx==null||sprite==null||sprite.texture.filterMode!=UnityEngine.FilterMode.Point)throw new System.Exception("VFX or Point sprite missing");
var so=new UnityEditor.SerializedObject(vfx);so.FindProperty("restoreSmokeSprite").objectReferenceValue=sprite;so.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{scene=scene.path,sprite=sprite.name,dirty=scene.isDirty};
