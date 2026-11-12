namespace ContextStage
{
    /// <summary>English presentation keyed by gameplay ID, not by the editable Korean wording.</summary>
    internal static class AugmentEnglish
    {
        public static string Name(AugmentDefinition definition) => GameLanguage.Pick(definition.DisplayName, definition.AugmentId switch
        {
            "audience_promotion" => "Show Promotion",
            "bandmate_cover" => "Bandmate Cover",
            "breathing_room" => "Breathing Room",
            "draw_audience_boost" => "Draw Support",
            "draw_plus" => "Draw Upgrade",
            "extra_hand" => "One More Card",
            "fever_duration" => "Extended Fever",
            "high_risk_double_tempo" => "Reckless Tempo",
            "high_risk_perfect_clear" => "Perfectionist",
            "high_risk_short_set" => "All-Out Sprint",
            "initial_audience" => "Early Promotion",
            "performance_duration" => "Encore",
            "preference_insight" => "Read the Room",
            "reroll_audience_boost" => "Reroll Support",
            "reroll_plus" => "Reroll Upgrade",
            "stage_control" => "Stage Control",
            _ => definition.DisplayName,
        });

        public static string Description(AugmentDefinition definition, AugmentTierData tier, string original, bool card = false)
        {
            if (!GameLanguage.English) return original;
            var upgrade = tier.CardUpgrade;
            int reaction = upgrade?.AudienceReactionBonus ?? 0;
            int stageReaction = tier.GrantedCard?.AudienceReaction?.FixedReactionValue ?? 0;
            float encoreSeconds = tier.GrantedCard?.PerformanceTimeBonusSeconds ?? 0f;
            int drawCount = (upgrade?.TargetCard?.DrawCount ?? 0) + (upgrade?.ExtraCardsAfterUse ?? 0);
            return definition.AugmentId switch
            {
                "audience_promotion" => $"Reduce the time between ordinary fan arrivals by {tier.Value:0.#}s.",
                "bandmate_cover" => tier.Value == 1 ? "Prevent one combo break per show." : $"Prevent {tier.Value:0} combo breaks per show.",
                "breathing_room" => $"Draw a card every {tier.Value:0.#}s you go without playing a card.",
                "draw_audience_boost" => card ? $"Draw {drawCount} cards. Give every fan +{reaction} reaction." : $"Draw now gives every fan +{reaction} reaction.",
                "draw_plus" => card ? $"Draw {drawCount} cards." : $"Draw now gives you {drawCount} cards.",
                "extra_hand" => "Increase your base hand size from 3 to 4.",
                "fever_duration" => $"Fever lasts {tier.Value:0.#}s longer.",
                "high_risk_double_tempo" => "Base hand size drops from 3 to 2. Successful plays add 2 combo instead of 1.",
                "high_risk_perfect_clear" => "Double your final score if your combo never breaks before time runs out.",
                "high_risk_short_set" => "Play for half the time and earn triple the score.",
                "initial_audience" => tier.Value == 1 ? "Start each show with one extra ordinary fan." : $"Start each show with {tier.Value:0} extra ordinary fans.",
                "performance_duration" => card ? $"Add {encoreSeconds:0.#}s to the show." : $"Gain an Encore card that adds {encoreSeconds:0.#}s when played.",
                "preference_insight" => "Always show ordinary fans' preference outlines.",
                "reroll_audience_boost" => card ? $"Redraw your hand. Give every fan +{reaction} reaction." : $"Reroll now gives every fan +{reaction} reaction.",
                "reroll_plus" => card ? "Redraw your hand and draw one extra card." : "Reroll now gives you one extra card.",
                "stage_control" => card ? $"Give every fan +{stageReaction} reaction." : $"Gain a Stage Control card that gives every fan +{stageReaction} reaction.",
                _ => original,
            };
        }

        public static string CardName(string augmentId, string original) => GameLanguage.Pick(original, augmentId switch
        {
            "draw_audience_boost" => "Draw: Spotlight",
            "draw_plus" => "Draw+",
            "performance_duration" => "Encore",
            "reroll_audience_boost" => "Reroll: Spotlight",
            "reroll_plus" => "Reroll+",
            "stage_control" => "Stage Control",
            _ => original,
        });
    }
}
