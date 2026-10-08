var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var show=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageShowDirector>();
var hint=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageLightController>();
var result=new System.Collections.Generic.List<string>{"stage="+ContextStage.StageRuntimeDirector.CurrentStage?.StageId+" managed="+show.IsManagedStage+" state="+GameJamKit.GameManager.Instance.State+" dark="+hint.IsBlackout+" hintmode="+hint.GetType().GetField("_showHintMode",flags).GetValue(hint)};
result.AddRange(UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsSortMode.None).Where(r=>r.enabled).Select(r=>r.name+" shader="+r.sharedMaterial?.shader.name));
result.AddRange(UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>(UnityEngine.FindObjectsSortMode.None).Where(r=>r.enabled&&(r.name.Contains("Light")||r.name.Contains("light")||r.name.Contains("Wash"))).Select(r=>r.name+" alpha="+r.color.a+" sprite="+r.sprite?.name));
result.AddRange(UnityEngine.Object.FindObjectsByType<UnityEngine.SpriteRenderer>(UnityEngine.FindObjectsSortMode.None).Where(r=>r.enabled&&r.sprite!=null&&r.sprite.bounds.size.x>10).Select(r=>r.name+" "+UnityEditor.AssetDatabase.GetAssetPath(r.sprite)));
result.AddRange(UnityEngine.Object.FindFirstObjectByType<ContextStage.StageBackgroundView>().ActiveSet.Lights.Select(r=>"SET LIGHT "+r?.name));
return result;
