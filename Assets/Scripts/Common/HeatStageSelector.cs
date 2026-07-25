namespace ContextStage
{
    public static class HeatStageSelector
    {
        public static T Select<T>(
            this HeatStage stage,
            T chill,
            T singalong,
            T mosh)
        {
            return stage switch
            {
                HeatStage.Singalong => singalong,
                HeatStage.Mosh => mosh,
                _ => chill,
            };
        }
    }
}
