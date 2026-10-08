var boss=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleRule>();
var rival=UnityEngine.Object.FindFirstObjectByType<ContextStage.RivalStagePlaceholder>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
int lost=(int)boss.GetType().GetMethod("LoseOurFans",flags).Invoke(boss,new object[]{2});
var fans=(System.Collections.IList)rival.GetType().GetField("_fans",flags).GetValue(rival);
return new{lost,logical=boss.RivalFans,visual=fans.Count,totalUnchanged=boss.TotalFans==UnityEditor.SessionState.GetInt("WorkBBossTotal",-1),direction="our audience exits upward toward rival"};
