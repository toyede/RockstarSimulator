var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
object Read(object owner, string name) => owner.GetType().GetField(name, flags)?.GetValue(owner);
object Rect(UnityEngine.RectTransform r) => r == null ? null : new {size = new[] {r.rect.width, r.rect.height},
    position = new[] {r.anchoredPosition.x, r.anchoredPosition.y}, min = new[] {r.anchorMin.x,r.anchorMin.y},
    max = new[] {r.anchorMax.x,r.anchorMax.y}, scale = new[] {r.lossyScale.x,r.lossyScale.y}};
var scenes = new System.Collections.Generic.List<object>();
for (int i=0; i<UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {
    var s=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); scenes.Add(new {s.path,s.isDirty});
}
var items=new System.Collections.Generic.List<object>();
foreach(var overlay in UnityEngine.Object.FindObjectsByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {
    var root=(UnityEngine.GameObject)Read(overlay,"panelRoot");
    items.Add(new {type="TutorialPanel",root.activeSelf,rect=Rect(root.GetComponent<UnityEngine.RectTransform>())});
    foreach(var t in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        items.Add(new {type="LegacyText",t.name,t.text,t.fontSize,t.resizeTextForBestFit,font=t.font?.name,t.lineSpacing,rect=Rect(t.rectTransform),height=t.preferredHeight});
}
foreach(var view in UnityEngine.Object.FindObjectsByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {
    var paper=(UnityEngine.RectTransform)Read(view,"paper");
    items.Add(new {type="ResultPaper",rect=Rect(paper),rotation=paper.localEulerAngles.z,restTilt=Read(view,"paperRestTilt")});
    foreach(var t in view.GetComponentsInChildren<TMPro.TMP_Text>(true)) {
        t.ForceMeshUpdate(true);
        items.Add(new {type="ResultText",t.name,t.text,t.fontSize,t.fontSizeMin,t.fontSizeMax,t.enableAutoSizing,font=t.font?.name,
            material=t.fontSharedMaterial?.name,t.fontStyle,t.isTextOverflowing,rect=Rect(t.rectTransform),height=t.preferredHeight});
    }
}
foreach(var boss in UnityEngine.Object.FindObjectsByType<ContextStage.BossBattleUI>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {
    var r=(UnityEngine.RectTransform)Read(boss,"_patternPanel");
    items.Add(new {type="Mission",rect=Rect(r),savedSize=Read(boss,"patternPanelSize")});
}
var catalog=ContextStage.ResultNewspaperCatalog.LoadDefault();
var skins=(System.Collections.Generic.List<ContextStage.ResultNewspaperCatalog.Skin>)Read(catalog,"skins");
foreach(var skin in skins) items.Add(new {type="Skin",skin.stageId,skin.paperName,paper=UnityEditor.AssetDatabase.GetAssetPath(skin.paper),
    dimensions=new[]{skin.paper.rect.width,skin.paper.rect.height},headline=new[]{skin.headlineOffset.x,skin.headlineOffset.y},records=new[]{skin.recordsOffset.x,skin.recordsOffset.y}});
return Newtonsoft.Json.JsonConvert.SerializeObject(new {paused=UnityEditor.EditorApplication.isPaused,scenes,items},
    new Newtonsoft.Json.JsonSerializerSettings{ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore});
