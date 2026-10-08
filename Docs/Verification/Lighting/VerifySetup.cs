var ui=UnityEngine.Object.FindObjectsByType<UnityEngine.RectTransform>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Where(t=>t.GetComponentInParent<ContextStage.StageLightingRig>()==null).ToArray();
var before=ui.Select(t=>t.name+":"+t.anchoredPosition+":"+t.sizeDelta).ToArray();
var sets=UnityEngine.Object.FindObjectsByType<ContextStage.StageSet>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
var props=sets.First(s=>s.StageId=="stage_01").GetComponentsInChildren<UnityEngine.Transform>(true).Where(t=>!t.name.Contains("Lighting") && t.GetComponent<ContextStage.PixelLaserRenderer>()==null).ToArray();
var positions=props.Select(t=>t.localPosition.ToString()+t.localScale.ToString()).ToArray();
ContextStage.EditorTools.StageLightingSetup.Apply();ContextStage.EditorTools.StageLightingSetup.Apply();
return new {
    rigCount=UnityEngine.Object.FindObjectsByType<ContextStage.StageLightingRig>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Length,
    uiUnchanged=before.SequenceEqual(ui.Select(t=>t.name+":"+t.anchoredPosition+":"+t.sizeDelta)),
    stage1PropsUnchanged=positions.SequenceEqual(props.Select(t=>t.localPosition.ToString()+t.localScale.ToString())),
    shadersOkay=new[]{"ContextStage/Lighting/Pixel Show Beam","ContextStage/Lighting/Stage Show Sprite"}.All(n=>UnityEngine.Shader.Find(n)!=null && !UnityEditor.ShaderUtil.ShaderHasError(UnityEngine.Shader.Find(n))),
    spriteAssetsSaved=UnityEngine.Object.FindObjectsByType<ContextStage.StageLightingRig>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).All(r=>r.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true).All(s=>s.sprite!=null && UnityEditor.AssetDatabase.Contains(s.sprite)))
};
