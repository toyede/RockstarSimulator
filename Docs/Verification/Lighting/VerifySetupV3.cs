var all=UnityEngine.Object.FindObjectsByType<UnityEngine.RectTransform>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
var before=all.Select(t=>t.GetInstanceID()+":"+t.anchoredPosition+":"+t.sizeDelta).ToArray();
var stage1=UnityEngine.Object.FindObjectsByType<ContextStage.StageSet>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).First(s=>s.StageId=="stage_01");
var props=stage1.GetComponentsInChildren<UnityEngine.Transform>(true).Where(t=>t.GetComponentInParent<ContextStage.StageLightingRig>()==null).ToArray();
var positions=props.Select(t=>t.localPosition.ToString()+t.localScale.ToString()).ToArray();
ContextStage.EditorTools.StageLightingSetup.ApplyVenueCuesV3();
ContextStage.EditorTools.StageLightingSetup.ApplyVenueCuesV3();
var rigs=UnityEngine.Object.FindObjectsByType<ContextStage.StageLightingRig>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
return new {
 uiUnchanged=before.SequenceEqual(all.Select(t=>t.GetInstanceID()+":"+t.anchoredPosition+":"+t.sizeDelta)),
 stage1LayoutUnchanged=positions.SequenceEqual(props.Select(t=>t.localPosition.ToString()+t.localScale.ToString())),
 rigs=rigs.Length,lasers=rigs.First(r=>r.Profile.rival).LaserCount,
 rimTargets=UnityEngine.Object.FindObjectsByType<ContextStage.BandRimLightView>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None).Length,
 shaderErrors=new[]{"ContextStage/Lighting/Band Rim Light","ContextStage/Lighting/Pixel Spotlight 2D","ContextStage/Lighting/Pixel Show Beam"}.Where(n=>UnityEngine.Shader.Find(n)==null || UnityEditor.ShaderUtil.ShaderHasError(UnityEngine.Shader.Find(n))).ToArray()
};
