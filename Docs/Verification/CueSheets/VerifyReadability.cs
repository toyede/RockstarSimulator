if (!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Play Mode required");
var checks=new System.Collections.Generic.List<string>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var view=UnityEngine.Object.FindFirstObjectByType<ContextStage.ResultNewspaperView>(UnityEngine.FindObjectsInactive.Include);
for(int n=1;n<=5;n++) for(int success=0;success<=1;success++)
{
 var stage=UnityEditor.AssetDatabase.LoadAssetAtPath<ContextStage.StageDefinition>(n==5?"Assets/Settings/Tour/Stage05Boss.asset":"Assets/Settings/Tour/Stage0"+n+".asset");
 var report=new ContextStage.PerformanceReport{targetScore=stage.TargetScore,maxCombo=123,feverCount=12,audienceRemaining=18,audienceCapacity=20,audiencePeak=20,specialSuccess=12,specialEnded=15,crisisCount=2,crisisThreatened=10,crisisRetained=8,stageEventsSucceeded=2,stageEventsTotal=3,isBoss=n==5,bossOutcome=success==1?ContextStage.BossOutcome.Won:ContextStage.BossOutcome.Lost,bossPatternsSucceeded=3,bossPatternsResolved=5,bossFansRecruited=8,bossFansLost=3,bossDrainTotal=4000};
 report.scoreByPreference[1]=23456;
 var result=new ContextStage.StageResult("visual",stage.StageId,success==1,success==1?9999999:1000,success==1?"S":"F",123){report=report};
 view.Show(ContextStage.ResultHeadlineSelector.Compose(result,stage,n,ContextStage.ResultNextAction.Ending,ContextStage.ResultNewspaperCatalog.LoadDefault()),null);view.CompleteImmediately();
 foreach(var field in new[]{"headline","subtitle","mastheadInfo","caption","stampText","scoreText","targetText","ratioText","statCombo","statFever","statAudience","articleTitle","articleBody","primaryLabel"})
 {
  var f=view.GetType().GetField(field,flags);if(f==null)continue;
  var t=f.GetValue(view) as TMPro.TMP_Text;if(t==null)continue;t.ForceMeshUpdate();
  bool fits=!t.isTextOverflowing && t.textBounds.size.y<=t.rectTransform.rect.height+.5f && t.textBounds.size.x<=t.rectTransform.rect.width+.5f;
  checks.Add((fits?"PASS ":"FAIL ")+n+":"+success+":"+field+(fits?"":" "+t.text+" bounds="+t.textBounds.size+" rect="+t.rectTransform.rect.size));
 }
}
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include);
var cases=new[]{
 new[]{"관객 위에 마우스를 올리거나 길게 누르면 관객의 마음을 알 수 있습니다.","말풍선과 테두리색을 통해 좋아하는 행동과 현재 기분을 확인할 수 있습니다. 2초 동안 유지해 힌트를 확인하세요."},
 new[]{"관객을 읽고, 알맞은 행동 카드를 사용하세요.","이 게임은 관객의 복장과 상태를 살펴 호응을 이끌어내는 공연 게임입니다. 옷차림은 취향을, 움직임은 현재 신난 정도를 보여줍니다. (클릭해서 계속)"},
 new[]{"새로운 SINGALONG 관객이 들어왔습니다.","관객 구성이 바뀌면 높은 총 반응을 얻기 좋은 카드도 달라집니다. 새 관객 위에 마우스를 올리거나 길게 누르고 2초 동안 옷차림·움직임·말풍선을 확인하세요."},
 new[]{"10초 안에 1,000점을 획득하세요.","관객의 옷차림·움직임·조명·말풍선을 확인하고, 총 반응이 높은 카드를 선택하세요. 목표 점수에 도달하지 못하면 실패합니다. (클릭해서 시작)"},
 new[]{"튜토리얼 완료! 관객의 맥락을 읽었습니다.","옷차림은 취향, 움직임은 현재 호응 상태, 조명은 다수 관객 성향을 알려줍니다. 이 단서로 높은 총 반응을 만드세요. (클릭해서 진짜 공연 시작)"},
 new[]{"카드를 누른 채 특별 관객에게 가져가세요.","관객의 몸과 겹치면 놓으세요. 움직이는 손 모양을 따라 해보세요."}};
for(int i=0;i<cases.Length;i++)
{
 overlay.SetSpecialLessonLayout(i==5);overlay.ShowMessage(cases[i][0],cases[i][1],true);UnityEngine.Canvas.ForceUpdateCanvases();
 foreach(var field in new[]{"mainText","subText"})
 {
  var t=(UnityEngine.UI.Text)overlay.GetType().GetField(field,flags).GetValue(overlay);
  var settings=t.GetGenerationSettings(t.rectTransform.rect.size);
  settings.verticalOverflow=UnityEngine.VerticalWrapMode.Overflow;
  var gen=new UnityEngine.TextGenerator();gen.Populate(t.text,settings);
  var required=gen.GetPreferredHeight(t.text,settings)/t.pixelsPerUnit;
  bool fits=required<=t.rectTransform.rect.height+.5f;
  checks.Add((fits?"PASS ":"FAIL ")+"tutorial:"+i+":"+field+" height="+required+" available="+t.rectTransform.rect.height);
 }
}
overlay.SetSpecialLessonLayout(false);overlay.HideAll();view.Hide();
return new{pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),failures=checks.Where(x=>x.StartsWith("FAIL")).ToArray(),tutorial=checks.Where(x=>x.Contains("tutorial:")).ToArray()};
