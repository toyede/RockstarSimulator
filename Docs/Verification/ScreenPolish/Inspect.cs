var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var rows=new System.Collections.Generic.List<object>();
foreach(var c in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)){
 if(c==null||!(c is ContextStage.BossBattleUI||c is ContextStage.AudienceRosterPresenter||c is ContextStage.BossStagePresentation||c is ContextStage.ScoreUI||c is ContextStage.StageEventNoticeUI))continue;
 var so=new UnityEditor.SerializedObject(c);var fields=new System.Collections.Generic.Dictionary<string,string>();
 foreach(var name in new[]{"topOffset","patternPanelPosition","patternPanelSize","horizontalSpacing","rowSpacing","maximumColumns","memberRoot","scoreText","peekButtonY","peekButtonSprite"}){var p=so.FindProperty(name);if(p!=null)fields[name]=p.propertyType==UnityEditor.SerializedPropertyType.ObjectReference?(p.objectReferenceValue!=null?p.objectReferenceValue.name:"null"):p.propertyType==UnityEditor.SerializedPropertyType.Vector2?p.vector2Value.ToString():p.propertyType==UnityEditor.SerializedPropertyType.Float?p.floatValue.ToString():p.propertyType==UnityEditor.SerializedPropertyType.Integer?p.intValue.ToString():p.propertyType.ToString();}
 rows.Add(new{type=c.GetType().Name,path=c.transform.parent!=null?c.transform.parent.name+"/"+c.name:c.name,position=c.transform.position.ToString(),fields});
}
return new{scene=scene.path,dirty=scene.isDirty,roots=scene.GetRootGameObjects().Select(g=>g.name).ToArray(),rows,cameras=UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(UnityEngine.FindObjectsSortMode.None).Select(c=>new{c.name,pos=c.transform.position.ToString(),c.orthographicSize,c.aspect}).ToArray()};
