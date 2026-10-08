var scenes = new System.Collections.Generic.List<object>();
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    scenes.Add(new {scene.path, scene.isLoaded, scene.isDirty});
}
return new {playing=UnityEngine.Application.isPlaying, scenes};
