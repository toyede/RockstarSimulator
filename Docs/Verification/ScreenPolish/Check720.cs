var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var ui=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleUI>();
var view=(ContextStage.MissionCueSheetView)ui.GetType().GetField("_missionView",flags).GetValue(ui);
var checks=new System.Collections.Generic.List<string>();
void Check(bool ok,string label)=>checks.Add((ok?"PASS ":"FAIL ")+label);
Check(UnityEngine.Screen.width==1280&&UnityEngine.Screen.height==720,"actual Game View 1280x720");
foreach(var text in new[]{view.Title,view.Body,view.Secondary,view.Progress,view.Timer}){text.ForceMeshUpdate();Check(text.textBounds.size.y<=text.rectTransform.rect.height+2,text.name+" text height fits");}
var notice=UnityEngine.Object.FindFirstObjectByType<ContextStage.StageEventNoticeUI>();var canvas=(UnityEngine.Canvas)notice.GetType().GetField("_canvas",flags).GetValue(notice);
Check(canvas==null||!canvas.gameObject.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text.Contains("스탠딩")),"no duplicate central standing mission");
var result=new{pass=checks.Count(s=>s.StartsWith("PASS")),fail=checks.Count(s=>s.StartsWith("FAIL")),checks};UnityEditor.SessionState.SetString("Polish720Verification",Newtonsoft.Json.JsonConvert.SerializeObject(result));return result;
