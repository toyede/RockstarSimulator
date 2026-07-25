namespace ContextStage
{
    /// <summary>
    /// 기존 부분 디스토션 에셋 호환용 타입.
    /// 새 효과는 LocalScreenEffectProfile을 사용한다.
    /// </summary>
    [System.Obsolete("새 효과는 LocalScreenEffectProfile을 사용하세요.")]
    public sealed class DistortionProfile : LocalScreenEffectProfile
    {
        public float EvaluateStrength(float normalizedTime) => EvaluateIntensity(normalizedTime);
    }
}
