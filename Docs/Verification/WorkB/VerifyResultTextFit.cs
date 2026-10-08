var checks=new System.Collections.Generic.List<string>();
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
for(int n=1;n<=5;n++)
{
    var stage=UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>(n==5?"Assets/Settings/Tour/Stage05Boss.asset":"Assets/Settings/Tour/Stage0"+n+".asset");
    var report=new ContextStage.PerformanceReport{targetScore=stage.TargetScore,maxCombo=123,feverCount=12,audienceRemaining=18,audienceCapacity=20,audiencePeak=20,isBoss=n==5,bossOutcome=ContextStage.BossOutcome.Won,bossPatternsSucceeded=n==5?3:0,bossPatternsResolved=n==5?5:0,bossFansRecruited=n==5?8:0,bossFansLost=n==5?3:0,bossDrainTotal=n==5?4000:0};
    var result=new ContextStage.StageResult("visual",stage.StageId,true,9999999,"S",123){report=report};
    view.Show(ContextStage.ResultHeadlineSelector.Compose(result,stage,n,ContextStage.ResultNextAction.Ending,ContextStage.ResultNewspaperCatalog.LoadDefault()),null);view.CompleteImmediately();
    foreach(var field in new[]{"headline","subtitle","scoreText","targetText","articleTitle","articleBody"})
    {
        var text=(TMPro.TMP_Text)view.GetType().GetField(field,flags).GetValue(view);text.ForceMeshUpdate();
        bool fits=text.textBounds.size.y<=text.rectTransform.rect.height+.5f && text.textBounds.size.x<=text.rectTransform.rect.width+.5f;
        checks.Add((fits?"PASS ":"FAIL ")+n+":"+field+" "+text.textBounds.size.ToString());
    }
}
return new{pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),checks};
