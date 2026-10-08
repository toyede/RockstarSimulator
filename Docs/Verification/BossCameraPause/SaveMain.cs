var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(UnityEditor.EditorApplication.isPlaying||scene.name!="Main")throw new System.InvalidOperationException("Edit Main only");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{saved=scene.path,dirty=scene.isDirty};
