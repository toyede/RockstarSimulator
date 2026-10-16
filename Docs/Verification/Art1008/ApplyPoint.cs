if(UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit Mode only");
var changed=new System.Collections.Generic.List<string>();
UnityEditor.AssetDatabase.StartAssetEditing();
try {
 foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets"})) {
  var path=UnityEditor.AssetDatabase.GUIDToAssetPath(guid);var importer=UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
  if(importer==null||importer.textureType!=UnityEditor.TextureImporterType.Sprite||importer.filterMode==UnityEngine.FilterMode.Point)continue;
  importer.filterMode=UnityEngine.FilterMode.Point;
  UnityEditor.AssetDatabase.WriteImportSettingsIfDirty(path);
  UnityEditor.AssetDatabase.ImportAsset(path);
  changed.Add(path);
 }
} finally {UnityEditor.AssetDatabase.StopAssetEditing();}
UnityEditor.SessionState.SetString("Art1008PointChanges",Newtonsoft.Json.JsonConvert.SerializeObject(changed));
return new{count=changed.Count,changed};
