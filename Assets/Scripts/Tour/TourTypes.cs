namespace ContextStage
{
    public enum RunNodeType
    {
        Performance,
        ElitePerformance
    }

    public enum RunNodeStatus
    {
        Locked,
        Available,
        Current,
        Cleared,
        Failed
    }

    public enum RunPhase
    {
        Map,
        Dialogue,
        Performance,
        Result,
        Reward,
        Completed,
        Failed,
        Travel
    }
}
