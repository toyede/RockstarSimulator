var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
object Read(object o,string name)=>o.GetType().GetField(name,flags).GetValue(o);
object Rect(UnityEngine.RectTransform r)=>new {name=r.name,size=new[]{r.rect.width,r.rect.height},position=new[]{r.anchoredPosition.x,r.anchoredPosition.y},local=new[]{r.localPosition.x,r.localPosition.y},scale=new[]{r.localScale.x,r.localScale.y},world=new[]{r.position.x,r.position.y}};
var data=new System.Collections.Generic.List<object>();
foreach(var d in UnityEngine.Object.FindObjectsByType<ContextStage.DialoguePanel>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) {
 var image=(UnityEngine.UI.Image)Read(d,"arrowImage");
 var box=(UnityEngine.UI.Image)Read(d,"boxBackground");
 data.Add(new {type="Dialogue",arrow=Rect(image.rectTransform),parent=Rect((UnityEngine.RectTransform)image.transform.parent),box=Rect(box.rectTransform),image.enabled,sprite=image.sprite.name});
}
foreach(var view in UnityEngine.Object.FindObjectsByType<ContextStage.MissionCueSheetView>(UnityEngine.FindObjectsSortMode.None)) {
 foreach(var t in new[]{view.Title,view.Body,view.Progress,view.Secondary}) {
 t.ForceMeshUpdate();
 data.Add(new {type="Mission",t.name,t.text,t.fontSize,t.isTextOverflowing,rect=Rect(t.rectTransform),height=t.preferredHeight});
 }
}
return Newtonsoft.Json.JsonConvert.SerializeObject(data);
