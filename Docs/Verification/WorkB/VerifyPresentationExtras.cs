var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,label)=>checks.Add((ok?"PASS ":"FAIL ")+label);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
System.Func<object,string,object> get=(obj,name)=>obj.GetType().GetField(name,flags).GetValue(obj);
var gm=GameJamKit.GameManager.Instance;
ContextStage.TutorialFlow.SuppressForTests=true;
var tutorial=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialFlow>();
if(ContextStage.TutorialFlow.IsRunning)tutorial.GetType().GetMethod("EndTutorial",flags).Invoke(tutorial,new object[]{false,false});
if(tutorial!=null)tutorial.enabled=false;
gm.ResetGame();gm.StartGame();
ContextStage.PerformanceTimer.SetPaused(true);
ContextStage.AudienceRosterSystem.Instance.SuppressEngagementDecay=true;
var presenter=UnityEngine.Object.FindFirstObjectByType<ContextStage.AudienceRosterPresenter>();
check(presenter.TryGetRandomActor(out var actor),"audience exists");
actor.PlayCardMotion("open_mosh_pit",25);
actor.GetType().GetMethod("Update",flags).Invoke(actor,null);
UnityEngine.Physics2D.SyncTransforms();
check(actor.ContainsPreferenceHoverPoint(actor.PreferenceHoverCenter),"moving audience hitbox contains visible hover center");
check(presenter.TryGetPreferenceHoverTarget(actor.PreferenceHoverCenter,out var picked)&&picked==actor,"presenter selects moving visible actor");
var hand=UnityEngine.Object.FindFirstObjectByType<ContextStage.CardHandUI>();
var known=(System.Collections.IList)get(hand,"_knownHand");
var card=ContextStage.CardSystem.Instance.GetCard(0);
ContextStage.CardSystem.Instance.SetHand(new[]{card,card,card});
hand.GetType().GetField("useDissolveOnPlay",flags).SetValue(hand,false);
hand.GetType().GetMethod("OnCardSelected",flags).Invoke(hand,new object[]{new GameJamKit.CardSelected{HandIndex=0,CardId=card.Id}});
check(known.Count==2,"consumed identical card removed from presentation snapshot");
GameJamKit.EventBus.Raise(new GameJamKit.HandChanged());
check(known.Count==3,"identical replacement retained after hand refresh");
hand.GetType().GetField("useDissolveOnPlay",flags).SetValue(hand,true);
var emitter=hand.GetComponent<ContextStage.UIPixelBurstEmitter>();
if(emitter==null)emitter=hand.gameObject.AddComponent<ContextStage.UIPixelBurstEmitter>();
emitter.enabled=true; // Ready cleanup disables it; exercise a real enabled->disabled transition.
var source=(UnityEngine.RectTransform)hand.transform;
emitter.EmitBurst(source,new UnityEngine.Color(1f,1f,1f,.2f),100,0f,0f,3f,3f,1f,1f);
var pixels=(System.Collections.IList)get(emitter,"_pixels");
check(pixels.Count==64,"pixel pool bounded to 64 under burst spam");
var pixel=pixels[0];
var pf=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public;
var image=(UnityEngine.UI.Image)pixel.GetType().GetField("Image",pf).GetValue(pixel);
emitter.GetType().GetMethod("Update",flags).Invoke(emitter,null);
check(image.color.a<=.201f,"transparent dust keeps supplied alpha");
check(!image.raycastTarget,"VFX never intercepts input");
emitter.enabled=false;
bool clean=true;foreach(var p in pixels){var rect=(UnityEngine.RectTransform)p.GetType().GetField("Rect",pf).GetValue(p);clean &= !rect.gameObject.activeSelf;}
check(clean,"disable clears pixel pool");
var sfx=UnityEngine.Object.FindFirstObjectByType<ContextStage.CardSfxPlayer>();
var lib=UnityEngine.Resources.Load<GameJamKit.SoundLibrary>("SoundLibrary");
var soundList=new System.Collections.Generic.List<string>();
foreach(var id in new[]{"guitar_solo","open_mosh_pit","pass_mic","tempo_up","hands_up","response_call"})
{
    string sound=(string)sfx.GetType().GetMethod("ResolveCardSfx",flags).Invoke(sfx,new object[]{id});
    var entry=lib.Find(sound);
    bool valid=entry!=null&&entry.clips!=null&&entry.clips.Length>0&&entry.clips[0]!=null;
    check(valid,"existing card audio wired: "+id);
    soundList.Add(id+"="+sound+"/"+(valid?entry.clips[0].length.ToString("F2"):"missing"));
}
return new{passed=checks.FindAll(s=>s.StartsWith("PASS")).Count,failed=checks.FindAll(s=>s.StartsWith("FAIL")).Count,checks,soundList};
