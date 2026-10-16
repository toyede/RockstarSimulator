if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play required");
UnityEditor.EditorApplication.isPaused=false;
var flow=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();if(flow!=null)flow.enabled=false;
var gm=GameJamKit.GameManager.Instance;GameJamKit.UIManager.Instance.CloseAll();gm.ResetGame();
foreach(string name in new[]{"DistortionProbeBase","DistortionProbeWash"}){var old=UnityEngine.GameObject.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old);}
ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>("Assets/Settings/Tour/Stage02.asset"));gm.StartGame();
var owner=new object();gm.AcquirePause(owner);
var material=new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
UnityEngine.SpriteRenderer Marker(string name,string layer,int order,UnityEngine.Color color){
 var go=new UnityEngine.GameObject(name);var renderer=go.AddComponent<UnityEngine.SpriteRenderer>();renderer.sprite=ContextStage.SquareSprite.Get();renderer.sharedMaterial=material;renderer.sortingLayerName=layer;renderer.sortingOrder=order;renderer.color=color;go.transform.localScale=new UnityEngine.Vector3(4,4,1);return renderer;
}
var ground=Marker("DistortionProbeBase","Default",9000,new UnityEngine.Color(.15f,.15f,.15f,1));
var wash=Marker("DistortionProbeWash","Effects",-150,new UnityEngine.Color(.02f,.8f,1,.7f));
var profile=UnityEngine.ScriptableObject.CreateInstance<ContextStage.LocalScreenEffectProfile>();
var data=new UnityEditor.SerializedObject(profile);data.FindProperty("shape").enumValueIndex=(int)ContextStage.DistortionShape.RoundedBox;data.FindProperty("strength").floatValue=0;data.ApplyModifiedPropertiesWithoutUndo();
var checks=new System.Collections.Generic.List<string>();
void Check(bool value,string name)=>checks.Add((value?"PASS ":"FAIL ")+name);
UnityEngine.Color Sample(){
 var texture=UnityEngine.ScreenCapture.CaptureScreenshotAsTexture();var uv=UnityEngine.Camera.main.WorldToViewportPoint(UnityEngine.Vector3.zero);var color=texture.GetPixel((int)(uv.x*texture.width),(int)(uv.y*texture.height));UnityEngine.Object.Destroy(texture);return color;
}
float Diff(UnityEngine.Color a,UnityEngine.Color b)=>UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(a.r-b.r),UnityEngine.Mathf.Abs(a.g-b.g),UnityEngine.Mathf.Abs(a.b-b.b));
System.Collections.IEnumerator Test(){
 yield return new UnityEngine.WaitForEndOfFrame();yield return new UnityEngine.WaitForEndOfFrame();var before=Sample();
 var handle=ContextStage.ScreenEffectSystem.Instance.Play(new ContextStage.ScreenEffectRequest(profile,UnityEngine.Vector3.zero,new UnityEngine.Vector2(5,5),durationOverride:20,useUnscaledTime:false));
 yield return new UnityEngine.WaitForEndOfFrame();yield return new UnityEngine.WaitForEndOfFrame();var after=Sample();
 var instance=(UnityEngine.Component)typeof(ContextStage.ScreenEffectHandle).GetField("_instance",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(handle);
 var effect=instance.GetComponent<UnityEngine.MeshRenderer>();
 Check(handle.IsValid,"distortion instance is actually active");
 Check(before.g-before.r>.15f,"probe has visible cyan lighting");
 Check(effect.sortingOrder==-200&&effect.sortingOrder<wash.sortingOrder,"distortion renders before light overlay");
 Check(Diff(before,after)<.03f,"GPU sampled lighting color retained during distortion");
 effect.sortingOrder=0;yield return new UnityEngine.WaitForEndOfFrame();yield return new UnityEngine.WaitForEndOfFrame();var legacy=Sample();
 Check(Diff(before,legacy)>.15f,"old sorting reproduces lost lighting color");
 effect.sortingOrder=-200;handle.Stop();
 var partial=new UnityEngine.GameObject("PartialProbe");partial.SetActive(false);partial.AddComponent<ContextStage.PartialDistortionEffect>();partial.SetActive(true);
 Check(partial.GetComponent<UnityEngine.MeshRenderer>().sortingOrder==-200,"non-pooled distortion uses same ordering");
 UnityEngine.Object.Destroy(partial);UnityEngine.Object.Destroy(ground.gameObject);UnityEngine.Object.Destroy(wash.gameObject);UnityEngine.Object.Destroy(material);UnityEngine.Object.Destroy(profile);gm.ReleasePause(owner);
 UnityEditor.SessionState.SetString("DistortionLightingVerification",Newtonsoft.Json.JsonConvert.SerializeObject(new{pass=checks.Count(s=>s.StartsWith("PASS")),fail=checks.Count(s=>s.StartsWith("FAIL")),before=before.ToString(),after=after.ToString(),legacy=legacy.ToString(),checks}));
 UnityEditor.EditorApplication.isPaused=true;
}
UnityEngine.Object.FindFirstObjectByType<ContextStage.TourDebugInput>().StartCoroutine(Test());return "GPU composition check scheduled (new ordering + old ordering control)";
