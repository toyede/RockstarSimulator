namespace ContextStage
{
    /// <summary>
    /// 관객이 이탈하는 사유. 지금은 자연 감소로 몰입도가 0이 되는 경우만 있지만,
    /// 스몰토크 카드의 강제 교체 등 다른 사유가 생기면 여기에 값만 추가하면 된다.
    /// </summary>
    public enum AudienceExitReason
    {
        NaturalDecay
    }
}
