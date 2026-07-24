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
}
