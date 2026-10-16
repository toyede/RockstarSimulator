var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var backgrounds=new System.Collections.Generic.List<object>();
foreach(var b in UnityEngine.Object.FindObjectsByType<ContextStage.AnimatedBackground>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None))backgrounds.Add(new{b.name,frames=b.Clip.FrameCount,first=UnityEditor.AssetDatabase.GetAssetPath(b.Clip.GetFrameAt(0)),fps=b.Clip.fps,scale=b.transform.localScale.ToString()});
var nonPoint=new System.Collections.Generic.List<string>();int sprites=0;
foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets"})){
 var path=UnityEditor.AssetDatabase.GUIDToAssetPath(guid);var importer=UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
 if(importer==null||importer.textureType!=UnityEditor.TextureImporterType.Sprite)continue;
 sprites++;if(importer.filterMode!=UnityEngine.FilterMode.Point)nonPoint.Add(path);
}
return new{scene=scene.path,dirty=scene.isDirty,backgrounds,sprites,nonPointCount=nonPoint.Count,examples=nonPoint.Take(15).ToArray()};
