using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 카드 판정이 끝난 뒤 점수/콤보를 확정하기 직전에 발행하는 순수 연출 이벤트.
    /// 게임 수치에는 관여하지 않으며 카드 임팩트가 즉시 재생되는 기준점으로 쓴다.
    /// </summary>
    public readonly struct CardPresentationStarted
    {
        public CardPresentationStarted(
            string cardId,
            CardRole role,
            HeatStage targetStage,
            Color cardColor)
        {
            CardId = cardId;
            Role = role;
            TargetStage = targetStage;
            CardColor = cardColor;
        }

        public string CardId { get; }
        public CardRole Role { get; }
        public HeatStage TargetStage { get; }
        public Color CardColor { get; }
    }
}
