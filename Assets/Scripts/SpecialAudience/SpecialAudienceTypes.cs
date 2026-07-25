namespace ContextStage
{
    // 특별 관객이 요구하는 반응 타입은 카드 시스템의 HeatStage(Chill/Singalong/Mosh)를 그대로 쓴다.
    // (정의 위치: Assets/Scripts/Cards/CardDefinition.cs)
    // 같은 의미의 enum 을 두 벌 두면 변환 코드와 불일치 버그가 생기므로 재사용한다.

    /// <summary>특별 관객 요청이 끝난 이유. 하나의 종료 이벤트로 묶어 처리한다.</summary>
    public enum SpecialAudienceEndReason
    {
        SpecialHit,       // 요구와 일치하는 타입이 들어와 성공
        Expired,          // 제한시간 초과
        ReplacedByDebug,  // P 키 디버그 등장으로 교체됨
        StageEnded        // 공연 종료·시스템 정지
    }

    /// <summary>
    /// 구형 Special Hit 이벤트 계약에 남아 있는 보상 정보.
    /// 현재 카드 시스템은 저격 효과를 SpecialCardTargetEffect로 적용하므로 이 값에는 0을 전달한다.
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
