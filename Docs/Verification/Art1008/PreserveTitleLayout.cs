if(UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit Mode only");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.name!="Title")throw new System.InvalidOperationException("Title only");
// 씬 저장 때 LayoutGroup이 남기는 계산 결과만 원래 직렬화 값으로 되돌린다.
// 원본 YAML은 읽기만 하며, 변경/저장은 Unity SerializedObject API로 수행한다.
var info=new System.Diagnostics.ProcessStartInfo("git","show HEAD:Assets/Scenes/Title.unity"){WorkingDirectory=UnityEngine.Application.dataPath+"/..",UseShellExecute=false,RedirectStandardOutput=true,CreateNoWindow=true};
var process=System.Diagnostics.Process.Start(info);string original=process.StandardOutput.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new System.Exception("Cannot read original Title");
string guid="65bb7ed1b4c62e947801337d5ec57ce4";
string pattern=@"- target: \{fileID: (-?\d+), guid: "+guid+@", type: 3\}\r?\n      propertyPath: (m_Anchor(?:Min|Max)\.[xy]|m_AnchoredPosition\.[xy]|m_SizeDelta\.[xy])\r?\n      value: ([^\r\n]+)";
var values=new System.Collections.Generic.Dictionary<string,string>();foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(original,pattern))values[match.Groups[1].Value+"/"+match.Groups[2].Value]=match.Groups[3].Value;
var groups=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.LayoutGroup>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Where(g=>g.enabled).ToArray();
foreach(var group in groups)group.enabled=false;
int changed=0;
foreach(var rect in UnityEngine.Object.FindObjectsByType<UnityEngine.RectTransform>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)){
 var source=UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(rect);if(source==null)continue;
 if(!UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string sourceGuid,out long id)||sourceGuid!=guid)continue;
 var so=new UnityEditor.SerializedObject(rect);
 foreach(var pair in values){string prefix=id+"/";if(!pair.Key.StartsWith(prefix))continue;var prop=so.FindProperty(pair.Key.Substring(prefix.Length));float value=float.Parse(pair.Value,System.Globalization.CultureInfo.InvariantCulture);if(prop!=null&&prop.floatValue!=value){prop.floatValue=value;changed++;}}
 so.ApplyModifiedPropertiesWithoutUndo();UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
}
foreach(var group in groups)group.enabled=true;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new{restoredLayoutValues=changed};
