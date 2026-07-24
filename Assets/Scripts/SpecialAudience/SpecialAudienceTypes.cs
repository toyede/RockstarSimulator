namespace ContextStage
{
    /// <summary>
    /// 특별 관객이 요구하는 반응 타입.
    ///
    /// 프로젝트 조사 결과 Chill/Singalong/Mosh 를 나타내는 기존 enum 이 없어 새로 만든다.
    /// (CardData 는 열기 구간만 보고 Perfect/Miss 를 판정하므로 맥락 타입 개념이 없었다)
    /// 나중에 카드에 맥락 타입이 생기면 이 enum 을 카드 쪽에서 그대로 재사용하면 된다.
    /// </summary>
    public enum SpecialAudienceRequestType
    {
        Chill,      // 조용히 듣고 싶어함 (기타 솔로 등)
        Singalong,  // 떼창하고 싶어함
        Mosh        // 격렬하게 놀고 싶어함
    }

    /// <summary>특별 관객 요청이 끝난 이유. 하나의 종료 이벤트로 묶어 처리한다.</summary>
    public enum SpecialAudienceEndReason
    {
        SpecialHit,       // 요구와 일치하는 타입이 들어와 성공
        Expired,          // 제한시간 초과
        ReplacedByDebug,  // P 키 디버그 등장으로 교체됨
        StageEnded        // 공연 종료·시스템 정지
    }

    /// <summary>
    /// Special Hit 성공 보상. 특별 관객 시스템은 이 값을 <b>직접 적용하지 않는다.</b>
    /// 점수·열기 담당이 OnSpecialHit 이벤트(또는 EventBus 의 SpecialHitLanded)를 받아 적용한다.
    /// </summary>
    public readonly struct SpecialHitReward
    {
        /// <summary>Special Hit 기본 점수 (열기 배율 적용 전).</summary>
        public int BonusBaseScore { get; }

        /// <summary>Special Hit 추가 열기(호응도).</summary>
        public float BonusHeat { get; }

        public SpecialHitReward(int bonusBaseScore, float bonusHeat)
        {
            BonusBaseScore = bonusBaseScore;
            BonusHeat = bonusHeat;
        }

        public override string ToString() => $"Score+{BonusBaseScore}, Heat+{BonusHeat}";
    }
}
