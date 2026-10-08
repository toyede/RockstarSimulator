var hand=UnityEngine.Object.FindFirstObjectByType<ContextStage.CardHandUI>(UnityEngine.FindObjectsInactive.Include);
var emitter=hand.GetComponent<ContextStage.UIPixelBurstEmitter>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
int active=0;
if(emitter!=null)
{
    var pixels=(System.Collections.IList)emitter.GetType().GetField("_pixels",flags).GetValue(emitter);
    foreach(var p in pixels){var field=p.GetType().GetField("Rect",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public);var rect=(UnityEngine.RectTransform)field.GetValue(p);if(rect.gameObject.activeInHierarchy)active++;}
}
return new{oneCardSnapshotMarked=UnityEditor.SessionState.GetBool("WorkBOneCardConsumed",false),activePixels=active,noLateBurst=active==0};
