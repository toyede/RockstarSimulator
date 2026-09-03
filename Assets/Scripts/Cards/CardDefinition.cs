using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    public readonly struct CardRuntimePresentation
    {
        public CardRuntimePresentation(
            Sprite artwork,
            string displayName,
            string description)
        {
            Artwork = artwork;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public Sprite Artwork { get; }
        public string DisplayName { get; }
        public string Description { get; }

        public static CardRuntimePresentation Resolve(
            CardDefinition card,
            CardUpgradeModifiers upgrade)
        {
            if (card == null) return default;

            return new CardRuntimePresentation(
                upgrade.Artwork != null ? upgrade.Artwork : card.Artwork,
                string.IsNullOrWhiteSpace(upgrade.DisplayNameOverride)
                    ? card.DisplayName
                    : upgrade.DisplayNameOverride,
                string.IsNullOrWhiteSpace(upgrade.DescriptionOverride)
                    ? card.Description
                    : upgrade.DescriptionOverride);
        }

        public static CardRuntimePresentation Resolve(CardDefinition card)
        {
            CardUpgradeModifiers upgrade = card == null
                ? default
                : AugmentRuntime.Current.ResolveCardUpgrade(card.Id);
            return Resolve(card, upgrade);
        }
    }

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
        Reroll,
        ExtendPerformanceTime
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

        [Header("개별 관객 반응")]
        [SerializeField] AudienceReactionProfile audienceReaction =
            new AudienceReactionProfile();

        [Header("스페셜 관객 저격 효과")]
        [SerializeField] SpecialCardTargetEffect specialTargetEffect =
            new SpecialCardTargetEffect();

        [Header("유틸리티")]
        [SerializeField, Min(0)] int drawCount = 2;
        [SerializeField, Min(0f)] float performanceTimeBonusSeconds;

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
        public AudienceReactionProfile AudienceReaction => audienceReaction;
        public SpecialCardTargetEffect SpecialTargetEffect => specialTargetEffect;
        public int DrawCount => Mathf.Max(0, drawCount);
        public float PerformanceTimeBonusSeconds =>
            Mathf.Max(0f, performanceTimeBonusSeconds);
        public Sprite Artwork => artwork;
        public Color CardColor => cardColor;

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
