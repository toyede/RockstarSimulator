using System;
using GameJamKit;

namespace ContextStage
{
    /// <summary>향후 특수 관객 시스템이 카드 판정에 넘길 최소 컨텍스트.</summary>
    public readonly struct SpecialCardRequest
    {
        public static SpecialCardRequest None => default;

        public SpecialCardRequest(HeatStage requestedStage)
        {
            IsActive = true;
            RequestedStage = requestedStage;
        }

        public bool IsActive { get; }
        public HeatStage RequestedStage { get; }
    }

    public readonly struct CardEffectResult
    {
        public CardEffectResult(
            HypeJudgement judgement,
            int baseScore,
            float heatDelta,
            bool isSpecialHit)
        {
            Judgement = judgement;
            BaseScore = baseScore;
            HeatDelta = heatDelta;
            IsSpecialHit = isSpecialHit;
        }

        public HypeJudgement Judgement { get; }
        public int BaseScore { get; }
        public float HeatDelta { get; }
        public bool IsSpecialHit { get; }
    }

    /// <summary>프리팹에 저장된 값과 현재 열기만으로 카드 결과를 계산하는 순수 판정기.</summary>
    public static class CardEffectResolver
    {
        public static CardEffectResult Resolve(
            CardDefinition card,
            float currentHype,
            HypeConfig config,
            SpecialCardRequest specialRequest)
        {
            if (card == null)
                return new CardEffectResult(HypeJudgement.Miss, 0, 0f, false);

            if (card.Role == CardRole.Utility)
                return new CardEffectResult(HypeJudgement.Good, 0, 0f, false);

            // 특수 카드는 "전부 아니면 전무"다.
            // 특별 관객이 요구하는 타입으로, 그 관객에게 정확히 냈을 때만 성공한다.
            // 그 외(관객이 없을 때·요구가 다를 때·엉뚱한 곳에 냈을 때)는 실패로 처리한다.
            // 이렇게 하지 않으면 특별 관객과 무관하게 열기 단계만 맞으면 Perfect 가 나와
            // "그냥 써도 성공"이 되어 버린다.
            if (card.Role == CardRole.Special)
            {
                if (specialRequest.IsActive && specialRequest.RequestedStage == card.TargetStage)
                {
                    return new CardEffectResult(
                        HypeJudgement.Perfect,
                        card.SpecialHitBaseScore,
                        card.SpecialHitHeatDelta,
                        true);
                }

                // 실패: 위험을 감수하고 던진 애드리브가 빗나간 것. 수치는 카드의 far 값으로 튜닝한다.
                return new CardEffectResult(
                    HypeJudgement.RiskMiss,
                    card.FarBaseScore,
                    card.FarHeatDelta,
                    false);
            }

            HeatStage currentStage = config != null
                ? config.ResolveStage(currentHype)
                : ResolveDefaultStage(currentHype);
            int distance = Math.Abs((int)currentStage - (int)card.TargetStage);

            switch (distance)
            {
                case 0:
                    return new CardEffectResult(
                        HypeJudgement.Perfect,
                        card.ExactBaseScore,
                        card.ExactHeatDelta,
                        false);
                case 1:
                    return new CardEffectResult(
                        HypeJudgement.Good,
                        card.AdjacentBaseScore,
                        card.AdjacentHeatDelta,
                        false);
                default:
                    return new CardEffectResult(
                        HypeJudgement.Miss,
                        card.FarBaseScore,
                        card.FarHeatDelta,
                        false);
            }
        }

        static HeatStage ResolveDefaultStage(float currentHype)
        {
            if (currentHype >= 80f) return HeatStage.Mosh;
            if (currentHype >= 50f) return HeatStage.Singalong;
            return HeatStage.Chill;
        }
    }
}
