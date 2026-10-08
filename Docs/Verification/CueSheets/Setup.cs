if (UnityEngine.Application.isPlaying) throw new System.Exception("Edit mode only");
UnityEditor.AssetDatabase.Refresh();
var output = new System.Collections.Generic.List<object>();
foreach (string name in new[]{"MissionSlate","TutorialPaper"}) {
 string path="Assets/Resources/UI/CueSheets/"+name+".png";
 var importer=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
 importer.textureType=UnityEditor.TextureImporterType.Sprite;
 importer.spriteImportMode=UnityEditor.SpriteImportMode.Single;
 importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
 importer.filterMode=UnityEngine.FilterMode.Point;
 importer.textureCompression=UnityEditor.TextureImporterCompression.Uncompressed;
 importer.maxTextureSize=2048; importer.spritePixelsPerUnit=100;
 importer.SaveAndReimport();
 var tex=new UnityEngine.Texture2D(2,2);
 UnityEngine.ImageConversion.LoadImage(tex,System.IO.File.ReadAllBytes(path));
 var pixels=tex.GetPixels32(); int transparent=0,opaque=0;
 for(int i=0;i<pixels.Length;i++) { if(pixels[i].a==0) transparent++; if(pixels[i].a==255) opaque++; }
 output.Add(new {name,width=tex.width,height=tex.height,transparent,opaque});
 UnityEngine.Object.DestroyImmediate(tex);
}
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include);
var so=new UnityEditor.SerializedObject(tutorial);
var panel=(UnityEngine.GameObject)so.FindProperty("panelRoot").objectReferenceValue;
UnityEditor.Undo.RecordObject(panel.GetComponent<UnityEngine.UI.Image>(),"Apply tutorial cue sheet");
panel.GetComponent<UnityEngine.UI.Image>().sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>("Assets/Resources/UI/CueSheets/TutorialPaper.png");
var boss=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>(UnityEngine.FindObjectsInactive.Include);
var bso=new UnityEditor.SerializedObject(boss);
bso.FindProperty("patternPanelPosition").vector2Value=new UnityEngine.Vector2(32,0);
bso.FindProperty("patternPanelSize").vector2Value=new UnityEngine.Vector2(512,288);
bso.ApplyModifiedProperties();
// 프리팹과 자동 생성 경로가 같은 우하단 위치를 사용한다.
string prefabPath="Assets/Resources/Dialogue/DialoguePanel.prefab";
var root=UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
try {
 var dialogue=root.GetComponent<ContextStage.DialoguePanel>();
 var dso=new UnityEditor.SerializedObject(dialogue);
 dso.ApplyModifiedPropertiesWithoutUndo();
 var arrow=(UnityEngine.UI.Image)dso.FindProperty("arrowImage").objectReferenceValue;
 var r=arrow.rectTransform;
 r.anchorMin=r.anchorMax=new UnityEngine.Vector2(1,0);r.pivot=new UnityEngine.Vector2(1,0);
 r.anchoredPosition=new UnityEngine.Vector2(-80,42);
 UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
} finally {UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
UnityEditor.EditorUtility.SetDirty(tutorial);UnityEditor.EditorUtility.SetDirty(boss);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(tutorial.gameObject.scene);
UnityEditor.AssetDatabase.SaveAssets();
return Newtonsoft.Json.JsonConvert.SerializeObject(output);
