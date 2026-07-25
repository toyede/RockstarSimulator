using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    public enum CardRole
    {
        Normal,
        Special,
        Utility
    }

    public enum HeatStage
    {
        Chill,
        Singalong,
        Mosh
    }

    public enum UtilityCardEffect
    {
        None,
        Draw,
        Reroll
    }

    /// <summary>
    /// 카드 프리팹 한 종류가 보유하는 게임 데이터다.
    /// 외형과 수치를 프리팹에서 함께 수정할 수 있도록 ScriptableObject 대신 프리팹 컴포넌트로 둔다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardDefinition : MonoBehaviour
    {
        [Header("식별")]
        [SerializeField] string id = "card";
        [SerializeField] string displayName = "Card";
        [SerializeField, TextArea(2, 4)] string description = "";

        [Header("역할")]
        [SerializeField] CardRole role;
        [SerializeField] HeatStage targetStage;
        [SerializeField, Tooltip("Permanent audience taste this card appeals to. This is not the current Hype stage.")]
        CrowdPreference targetPreference;
        [SerializeField] UtilityCardEffect utilityEffect;

        [Header("일반 단계 판정")]
        [SerializeField, Min(0)] int exactBaseScore = 100;
        [SerializeField, Min(0)] int adjacentBaseScore = 50;
        [SerializeField, Min(0)] int farBaseScore;
        [SerializeField] float exactHeatDelta = 12f;
        [SerializeField] float adjacentHeatDelta = 4f;
        [SerializeField] float farHeatDelta = -5f;

        [Header("특수 히트")]
        [SerializeField, Min(0)] int specialHitBaseScore = 400;
        [SerializeField] float specialHitHeatDelta = 25f;

        [Header("개별 관객 반응")]
        [SerializeField] AudienceReactionProfile audienceReaction =
            new AudienceReactionProfile();

        [Header("유틸리티")]
        [SerializeField, Min(0)] int drawCount = 2;

        [Header("외형")]
        [SerializeField] Sprite artwork;
        [SerializeField] Color cardColor = Color.white;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public CardRole Role => role;
        public HeatStage TargetStage => targetStage;
        public CrowdPreference TargetPreference => targetPreference;
        public UtilityCardEffect UtilityEffect => utilityEffect;
        public int BaseScore => exactBaseScore;
        public int ExactBaseScore => exactBaseScore;
        public int AdjacentBaseScore => adjacentBaseScore;
        public int FarBaseScore => farBaseScore;
        public float ExactHeatDelta => exactHeatDelta;
        public float AdjacentHeatDelta => adjacentHeatDelta;
        public float FarHeatDelta => farHeatDelta;
        public int SpecialHitBaseScore => specialHitBaseScore;
        public float SpecialHitHeatDelta => specialHitHeatDelta;
        public AudienceReactionProfile AudienceReaction => audienceReaction;
        public int DrawCount => Mathf.Max(0, drawCount);
        public Sprite Artwork => artwork;
        public Color CardColor => cardColor;

        public HypeJudgement PreviewJudgement(float currentHype, HypeConfig config)
            => CardEffectResolver.Resolve(this, currentHype, config, SpecialCardRequest.None).Judgement;

        public float PreviewHeatDelta(float currentHype, HypeConfig config)
            => CardEffectResolver.Resolve(this, currentHype, config, SpecialCardRequest.None).HeatDelta;

        public CrowdReactionGrade PreviewCrowdReaction(
            CrowdCompositionSnapshot composition,
            CrowdCompositionConfig compositionConfig)
            => Role == CardRole.Utility
                ? CrowdReactionGrade.Good
                : compositionConfig == null
                    ? CrowdReactionGrade.Weak
                    : CrowdReactionEvaluator.Evaluate(
                        composition,
                        targetPreference,
                        compositionConfig.GoodReactionThreshold);
    }
}
