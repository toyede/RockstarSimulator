var boss=UnityEngine.Object.FindFirstObjectByType<ContextStage.BossBattleRule>();
var rival=UnityEngine.Object.FindFirstObjectByType<ContextStage.RivalStagePlaceholder>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var fans=(System.Collections.IList)rival.GetType().GetField("_fans",flags).GetValue(rival);
var root=(UnityEngine.Transform)rival.GetType().GetField("_root",flags).GetValue(rival);
int children=0;foreach(UnityEngine.Transform t in root)if(t.name.StartsWith("RivalFan_"))children++;
return new{logical=boss.RivalFans,visual=fans.Count,renderers=children,noOrphans=children==fans.Count,totalUnchanged=boss.TotalFans==UnityEditor.SessionState.GetInt("WorkBBossTotal",-1)};
