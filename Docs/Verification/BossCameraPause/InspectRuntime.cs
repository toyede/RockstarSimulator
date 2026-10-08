var rows=new System.Collections.Generic.List<object>();
foreach(var t in UnityEngine.Object.FindObjectsByType<UnityEngine.RectTransform>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {
 if(t.name.IndexOf("Volume",System.StringComparison.OrdinalIgnoreCase)<0 && t.name.IndexOf("Options",System.StringComparison.OrdinalIgnoreCase)<0 && t.name.IndexOf("Window",System.StringComparison.OrdinalIgnoreCase)<0)continue;
 var path=t.name; var parent=t.parent;while(parent!=null){path=parent.name+"/"+path;parent=parent.parent;}
 rows.Add(new{path,active=t.gameObject.activeInHierarchy,components=System.Array.ConvertAll(t.GetComponents<UnityEngine.Component>(),c=>c?.GetType().Name)});
}
return new{rows};
