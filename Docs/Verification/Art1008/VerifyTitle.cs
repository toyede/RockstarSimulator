if(!UnityEditor.EditorApplication.isPlaying)throw new System.InvalidOperationException("Play required");
UnityEditor.EditorApplication.isPaused=false;
var background=UnityEngine.Object.FindFirstObjectByType<ContextStage.AnimatedBackground>();
var renderer=background.GetComponent<UnityEngine.SpriteRenderer>();var image=background.GetComponent<UnityEngine.UI.Image>();
UnityEngine.Sprite Current()=>renderer!=null?renderer.sprite:image.sprite;
var first=Current();
System.Collections.IEnumerator Verify(){
 yield return new UnityEngine.WaitForSecondsRealtime(.21f);
 var second=Current();
 var checks=new System.Collections.Generic.List<string>();
 void Check(bool ok,string name)=>checks.Add((ok?"PASS ":"FAIL ")+name);
 Check(background.Clip.FrameCount==29,"29 title frames");
 Check(background.Clip.frames.All(f=>f!=null&&UnityEditor.AssetDatabase.GetAssetPath(f).StartsWith("Assets/Sprites/1008_art/시작화면_배경/")),"all frames from new folder");
 Check(first!=second,"title animation advances");
 Check(background.Clip.fps==12f&&background.Clip.loop,"existing speed and loop retained");
 Check(background.Clip.frames.All(f=>f.texture.filterMode==UnityEngine.FilterMode.Point),"all title textures Point");
 Check(UnityEditor.AssetDatabase.GetAssetPath(second).StartsWith("Assets/Sprites/1008_art/"),"renderer displays new art");
 UnityEditor.SessionState.SetString("Art1008TitleVerification",Newtonsoft.Json.JsonConvert.SerializeObject(new{checks}));
}
background.StartCoroutine(Verify());return "Title animation verification scheduled";
