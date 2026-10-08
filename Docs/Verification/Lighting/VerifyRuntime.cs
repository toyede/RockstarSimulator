var results=new System.Collections.Generic.List<string>();
var director=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageShowDirector>();
var rigs=UnityEngine.Object.FindObjectsByType<ContextStage.StageLightingRig>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None);
for(int n=1;n<=5;n++)
{
    string path="Assets/Settings/Tour/Stage0"+n+(n==5?"Boss":"")+".asset";
    ContextStage.StageRuntimeDirector.Active.ApplyStage(UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>(path));
    int active=rigs.Count(r=>r.gameObject.activeInHierarchy);
    results.Add("Stage"+n+" active="+active+" expected="+(n==5?2:1));
    foreach(var rig in rigs.Where(r=>r.gameObject.activeInHierarchy))
    {
        rig.Render(2,1,false,false,null,false,true,false,true,0,0,false);
        bool black=rig.GetComponentsInChildren<ContextStage.PixelLaserRenderer>().All(b=>!b.GetComponent<UnityEngine.MeshRenderer>().enabled);
        results.Add(rig.Profile.stageId+(rig.Profile.rival?" rival":" ours")+" blackout="+black);
        rig.Render(3,1,true,false,"drop",false,false,false,true,0.2f,1,false);
    }
}
var lux=rigs.First(r=>r.Profile.rival);
for(int n=0;n<100;n++)lux.EmitSmoke(100+n,4);
results.Add("smoke="+lux.ParticleCount+" cap="+lux.GetComponentsInChildren<UnityEngine.ParticleSystem>().Sum(p=>p.main.maxParticles));
for(int n=0;n<3;n++){director.enabled=false;director.enabled=true;}
results.Add("after disable/enable particles="+lux.ParticleCount);
return results.ToArray();
