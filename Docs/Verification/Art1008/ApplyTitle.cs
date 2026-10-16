if(UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit Mode only");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="Title")throw new System.InvalidOperationException("Title only");
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.AnimatedBackground>(UnityEngine.FindObjectsInactive.Include);
if(view==null)throw new System.InvalidOperationException("AnimatedBackground missing");
var frames=new System.Collections.Generic.List<UnityEngine.Sprite>();
for(int i=0;i<29;i++){
 string path=$"Assets/Sprites/1008_art/시작화면_배경/BG_{i:D4}.png";
 var sprite=UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.Sprite>().SingleOrDefault();
 if(sprite==null)throw new System.InvalidOperationException("Expected one sprite at "+path);
 frames.Add(sprite);
}
var old=view.Clip.GetFrameAt(0);
if(old.rect.size!=frames[0].rect.size||old.pixelsPerUnit!=frames[0].pixelsPerUnit)throw new System.InvalidOperationException($"Frame geometry differs: old {old.rect.size}/{old.pixelsPerUnit}, new {frames[0].rect.size}/{frames[0].pixelsPerUnit}");
UnityEditor.Undo.RecordObject(view,"Replace title frames");
var so=new UnityEditor.SerializedObject(view);var prop=so.FindProperty("clip.frames");prop.arraySize=frames.Count;
for(int i=0;i<frames.Count;i++)prop.GetArrayElementAtIndex(i).objectReferenceValue=frames[i];so.ApplyModifiedProperties();
var renderer=so.FindProperty("spriteRenderer").objectReferenceValue as UnityEngine.SpriteRenderer;
var image=so.FindProperty("targetImage").objectReferenceValue as UnityEngine.UI.Image;
if(renderer!=null){UnityEditor.Undo.RecordObject(renderer,"Replace title preview");renderer.sprite=frames[0];UnityEditor.EditorUtility.SetDirty(renderer);}
if(image!=null){UnityEditor.Undo.RecordObject(image,"Replace title preview");image.sprite=frames[0];UnityEditor.EditorUtility.SetDirty(image);}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{frames=frames.Count,fps=view.Clip.fps,first=UnityEditor.AssetDatabase.GetAssetPath(frames[0]),last=UnityEditor.AssetDatabase.GetAssetPath(frames[28])};
