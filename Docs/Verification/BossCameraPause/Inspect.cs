var result = new System.Collections.Generic.List<object>();
foreach (var popup in UnityEngine.Object.FindObjectsByType<GameJamKit.UIPopup>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) {
 var groups = new System.Collections.Generic.List<object>();
 foreach(var g in popup.GetComponentsInChildren<UnityEngine.CanvasGroup>(true)) groups.Add(new{name=g.name,alpha=g.alpha,ignore=g.ignoreParentGroups});
 var canvases = new System.Collections.Generic.List<object>();
 foreach(var c in popup.GetComponentsInChildren<UnityEngine.Canvas>(true)) canvases.Add(new{name=c.name,sorting=c.sortingOrder,over=c.overrideSorting});
 result.Add(new{popup=popup.name,type=popup.GetType().Name,open=popup.IsOpen,active=popup.gameObject.activeSelf,parent=popup.transform.parent?.name,groups,canvases});
}
var sets = new System.Collections.Generic.List<object>();
foreach (var set in UnityEngine.Object.FindObjectsByType<ContextStage.StageSet>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) sets.Add(new{id=set.StageId,background=UnityEditor.AssetDatabase.GetAssetPath(set.BaseBackgroundOverride)});
var catalog=ContextStage.StageVisualCatalog.LoadDefault();
var backgrounds=new System.Collections.Generic.List<object>();
foreach(var entry in catalog.Entries) backgrounds.Add(new{id=entry.stageId,path=UnityEditor.AssetDatabase.GetAssetPath(entry.backgroundBase)});
return new{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,dirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty,popups=result,sets,backgrounds};
