if(UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit Mode only");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.name!="Title")throw new System.InvalidOperationException("Title only");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{scene=scene.path,dirty=scene.isDirty};
