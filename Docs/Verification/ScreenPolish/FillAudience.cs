var roster=ContextStage.AudienceRosterSystem.Instance;
int guard=0;while(roster.Count<roster.Capacity&&guard++<50)roster.TryAddRandom(ContextStage.AudienceJoinReason.RuntimeCommand,out var added);
var actors=UnityEngine.Object.FindObjectsByType<ContextStage.AudienceMemberActor>(UnityEngine.FindObjectsSortMode.None).Where(a=>a.IsBound).ToArray();
return new{roster.Count,roster.Capacity,bounds=actors.Select(a=>new{a.name,position=a.LayoutLocalPosition.ToString()}).ToArray()};
