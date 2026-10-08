if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play Mode required");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var overlay=UnityEngine.Object.FindFirstObjectByType<ContextStage.TutorialOverlayUI>(UnityEngine.FindObjectsInactive.Include);
var cases=new[]{
new[]{"새 관객 위에 마우스를 올려보세요.","모바일에서는 길게 누르세요.\n2초 동안 유지하며 테두리색과 말풍선으로 관객의 마음을 확인하세요."},
new[]{"관객을 읽고, 알맞은 행동 카드를 사용하세요.","이 게임은 관객의 복장과 상태를 살펴 호응을 이끌어내는 공연 게임입니다. 옷차림은 취향을, 움직임은 현재 신난 정도를 보여줍니다. (클릭해서 계속)"},
new[]{"새로운 SINGALONG 관객이 들어왔습니다.","관객 구성이 바뀌면 높은 총 반응을 얻기 좋은 카드도 달라집니다. 새 관객 위에 마우스를 올리거나 길게 누르고 2초 동안 옷차림·움직임·말풍선을 확인하세요."},
new[]{"10초 안에 1,000점을 획득하세요.","관객의 옷차림·움직임·조명·말풍선을 확인하고, 총 반응이 높은 카드를 선택하세요. 목표 점수에 도달하지 못하면 실패합니다. (클릭해서 시작)"},
new[]{"튜토리얼 완료! 관객의 맥락을 읽었습니다.","옷차림은 취향, 움직임은 현재 호응 상태, 조명은 다수 관객 성향을 알려줍니다. 이 단서로 높은 총 반응을 만드세요. (클릭해서 진짜 공연 시작)"},
new[]{"카드를 누른 채 특별 관객에게 가져가세요.","관객의 몸과 겹치면 놓으세요. 움직이는 손 모양을 따라 해보세요."}};
var checks=new System.Collections.Generic.List<string>();
for(int i=0;i<cases.Length;i++){
overlay.SetSpecialLessonLayout(i==5);overlay.ShowMessage(cases[i][0],cases[i][1],true);UnityEngine.Canvas.ForceUpdateCanvases();
foreach(var field in new[]{"mainText","subText"}){
var t=(UnityEngine.UI.Text)overlay.GetType().GetField(field,flags).GetValue(overlay);
var settings=t.GetGenerationSettings(t.rectTransform.rect.size);settings.verticalOverflow=UnityEngine.VerticalWrapMode.Overflow;
var gen=new UnityEngine.TextGenerator();gen.Populate(t.text,settings);
float h=gen.GetPreferredHeight(t.text,settings)/t.pixelsPerUnit;
checks.Add((h<=t.rectTransform.rect.height-8?"PASS ":"FAIL ")+i+":"+field+" vertical spare="+(t.rectTransform.rect.height-h));
}}
overlay.SetSpecialLessonLayout(false);overlay.HideAll();
return new{pass=checks.Count(x=>x.StartsWith("PASS")),fail=checks.Count(x=>x.StartsWith("FAIL")),checks};
