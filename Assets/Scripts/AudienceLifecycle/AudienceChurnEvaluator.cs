namespace ContextStage
{
    /// <summary>
    /// 이탈 판정 순수 함수. 개인 몰입도 시스템이 매 프레임/틱마다 몰입도를 갱신하는 곳에서
    /// 이 함수로 "지금 이탈해야 하는지"만 물어보고, true 면 AudienceLifecycle.RequestExit 를 호출하면 된다.
    ///
    /// 판정과 처리(퇴장 연출, 이벤트 발행, 게임오버 체크)를 분리해 둔 이유:
    /// 몰입도 시스템 쪽에서는 이 함수만 알면 되고, 실제 퇴장 처리 방식이 바뀌어도 영향받지 않는다.
    /// </summary>
    public static class AudienceChurnEvaluator
    {
        /// <summary>기획 3.2: 몰입도 0에서 퇴장.</summary>
        public static bool ShouldExit(float immersion) => immersion <= 0f;
    }
}
