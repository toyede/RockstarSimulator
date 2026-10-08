return new {
 scenes=Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i=>new {name=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name,dirty=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty}).ToArray(),
 layers=UnityEngine.SortingLayer.layers.Select(l=>new {l.name,l.value}).ToArray(),
 renderers=UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Where(r=>r.gameObject.scene.name=="Main").GroupBy(r=>r.sortingLayerName).Select(g=>new {layer=g.Key,min=g.Min(r=>r.sortingOrder),max=g.Max(r=>r.sortingOrder)}).ToArray(),
 canvas=UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Select(c=>new {c.name,mode=c.renderMode.ToString(),c.sortingLayerName,c.sortingOrder,c.overrideSorting}).ToArray(),
 band=UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Where(r=>r.name.Contains("Raccoon") || r.name=="Friends").Select(r=>new {r.name,shader=r.sharedMaterial!=null?r.sharedMaterial.shader.name:null,r.sortingLayerName,r.sortingOrder,parent=r.transform.parent!=null?r.transform.parent.name:null}).ToArray()
};
