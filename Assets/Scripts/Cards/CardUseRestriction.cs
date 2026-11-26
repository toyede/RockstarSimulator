namespace ContextStage
{
    /// <summary>
    /// 카드를 지금 낼 수 없는 이유를 한곳에서 판정한다.
    /// 입력 거부, CardSystem 가드, 손패의 X 표시(CardSealVisual)가 같은 판정을 쓴다.
    /// </summary>
    public static class CardUseRestriction
    {
        /// <summary>보스 KILL SWITCH 봉인 또는 앙코르 조건 미달이면 true.</summary>
        public static bool IsBlocked(CardDefinition card) =>
            KillSwitchPattern.IsCardSealed(card) || !IsEncoreReady(card);

        /// <summary>앙코르가 아닌 카드는 항상 true. 앙코르는 콤보가 비용 이상이고 피버 중이 아닐 때만 true.</summary>
        public static bool IsEncoreReady(CardDefinition card)
        {
            if (card == null || !card.IsEncoreFever) return true;
            if (FeverSystem.HasInstance && FeverSystem.Instance.IsActive) return false;
            int combo = ComboSystem.HasInstance ? ComboSystem.Instance.CurrentCombo : 0;
            return combo >= card.EncoreComboCost;
        }
    }
}
