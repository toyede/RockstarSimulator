namespace GameJamKit
{
    public struct CrowdCompositionChanged
    {
        public ContextStage.CrowdCompositionSnapshot Previous;
        public ContextStage.CrowdCompositionSnapshot Current;
        public string PreviousPresetId;
        public string CurrentPresetId;
        public string Source;
    }

    public struct CrowdShiftStarted
    {
        public string EventName;
        public string TargetPresetId;
        public ContextStage.CrowdCompositionSnapshot Target;
    }
}
