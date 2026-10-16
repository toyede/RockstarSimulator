var run=ContextStage.TourRunManager.Instance.CurrentRun;
return new{run.phase,run.totalScore,results=run.stageResults.Select(r=>new{r.stageId,r.score,r.cleared,r.rank,r.report}).ToArray(),score=GameJamKit.GameManager.Instance.Score};
