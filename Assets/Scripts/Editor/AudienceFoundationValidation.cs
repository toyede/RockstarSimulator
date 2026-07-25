using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    public static class AudienceFoundationValidation
    {
        [MenuItem("Tools/Validation/Validate Audience Foundation")]
        public static void Validate()
        {
            var failures = new List<string>();
            CollectFailures(failures);

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "Audience foundation validation failed:\n- " +
                    string.Join("\n- ", failures));

            Debug.Log("[Validation] Audience foundation passed.");
        }

        public static void CollectFailures(List<string> failures)
        {
            if (failures == null) throw new ArgumentNullException(nameof(failures));

            var engagement = new AudienceEngagementRules(
                100f,
                30f,
                50f,
                33f,
                66f,
                1f,
                1f);
            var flow = new AudienceFlowRules(
                3,
                10,
                1f,
                1f,
                1f,
                4861,
                0f,
                0f);

            if (!engagement.TryValidate(out string engagementError))
                failures.Add($"Valid engagement rules were rejected: {engagementError}");
            if (!flow.TryValidate(out string flowError))
                failures.Add($"Valid flow rules were rejected: {flowError}");

            ValidateStageBoundaries(engagement, failures);
            ValidateRosterLifecycle(engagement, flow, failures);
            ValidateAudienceReaction(failures);
            ValidateMemberPrefab(failures);
        }

        static void ValidateStageBoundaries(
            AudienceEngagementRules rules,
            List<string> failures)
        {
            Require(
                rules.ResolveStage(1f) == AudienceEngagementStage.Calm &&
                rules.ResolveStage(33f) == AudienceEngagementStage.Calm,
                "Calm stage boundary is not 1..33.",
                failures);
            Require(
                rules.ResolveStage(34f) == AudienceEngagementStage.Middle &&
                rules.ResolveStage(66f) == AudienceEngagementStage.Middle,
                "Middle stage boundary is not 34..66.",
                failures);
            Require(
                rules.ResolveStage(67f) == AudienceEngagementStage.Excited &&
                rules.ResolveStage(100f) == AudienceEngagementStage.Excited,
                "Excited stage boundary is not 67..100.",
                failures);
        }

        static void ValidateRosterLifecycle(
            AudienceEngagementRules engagement,
            AudienceFlowRules flow,
            List<string> failures)
        {
            var model = new AudienceRosterModel(engagement, flow);
            var removed = new List<AudienceSnapshot>();
            var added = new List<AudienceSnapshot>();
            model.Reset(0f, removed, added);

            Require(model.Count == 3, $"Expected 3 initial members, found {model.Count}.", failures);
            Require(added.Count == 3, $"Expected 3 join results, found {added.Count}.", failures);

            var firstRun = new List<AudienceSnapshot>(model.Members);
            for (int i = 0; i < firstRun.Count; i++)
            {
                AudienceSnapshot member = firstRun[i];
                Require(member.Id.Value == i + 1, "Initial IDs are not sequential.", failures);
                Require(
                    member.Engagement >= 30f && member.Engagement <= 50f,
                    $"Initial engagement {member.Engagement} is outside 30..50.",
                    failures);
            }

            model.Reset(10f, removed, added);
            Require(removed.Count == 3, "Reset did not report prior members.", failures);
            Require(added.Count == 3, "Reset did not recreate initial members.", failures);
            for (int i = 0; i < firstRun.Count && i < added.Count; i++)
            {
                Require(
                    firstRun[i].Id == added[i].Id &&
                    firstRun[i].Preference == added[i].Preference &&
                    Mathf.Approximately(firstRun[i].Engagement, added[i].Engagement),
                    "Seeded reset is not deterministic.",
                    failures);
            }

            var changes = new List<AudienceStateChange>();
            var departed = new List<AudienceSnapshot>();
            var beforeDecay = new List<AudienceSnapshot>(model.Members);
            model.ApplyNaturalDecay(1f, changes, departed);
            Require(changes.Count == 3, "One-second decay did not update every member.", failures);
            Require(departed.Count == 0, "Healthy members departed during one-second decay.", failures);

            for (int i = 0; i < beforeDecay.Count; i++)
            {
                Require(
                    model.TryGet(beforeDecay[i].Id, out AudienceSnapshot after) &&
                    Mathf.Approximately(after.Engagement, beforeDecay[i].Engagement - 1f),
                    "Natural decay amount is incorrect.",
                    failures);
            }

            AudienceId firstId = model.Members[0].Id;
            Require(
                model.TrySetEngagement(firstId, 0f, out AudienceStateChange removal, out bool didDepart) &&
                didDepart &&
                Mathf.Approximately(removal.Current.Engagement, 0f) &&
                model.Count == 2,
                "Setting engagement to zero did not remove exactly one member.",
                failures);

            while (!model.IsFull)
            {
                if (!model.TryAddRandom(20f, out _))
                {
                    failures.Add("Roster rejected a member before reaching capacity.");
                    break;
                }
            }

            Require(model.Count == 10, $"Expected capacity 10, found {model.Count}.", failures);
            Require(
                !model.TryAddRandom(20f, out _),
                "Roster accepted an eleventh member.",
                failures);

            var ids = new HashSet<AudienceId>();
            for (int i = 0; i < model.Members.Count; i++)
            {
                Require(
                    ids.Add(model.Members[i].Id),
                    "A departed audience ID was reused during the same run.",
                    failures);
            }

            AudienceSummary summary = model.CreateSummary();
            Require(
                summary.Count == model.Count &&
                summary.CalmCount + summary.MiddleCount + summary.ExcitedCount == model.Count,
                "Audience summary counts do not match the roster.",
                failures);
        }

        static void ValidateAudienceReaction(List<string> failures)
        {
            var profile = new AudienceReactionProfile(
                true,
                10,
                20,
                30,
                100,
                200,
                300,
                1f);
            var audience = new AudienceSnapshot(
                new AudienceId(1),
                CrowdPreference.Mosh,
                80f,
                AudienceEngagementStage.Excited,
                0f);
            AudienceReactionResult result =
                AudienceReactionResolver.Resolve(profile, audience);

            Require(
                result.AudienceId == audience.Id &&
                result.PreferenceScore == 30 &&
                result.StageScore == 300 &&
                result.Value == 330,
                "Audience reaction values were mixed up or capped.",
                failures);

            var disabledProfile = new AudienceReactionProfile(
                false,
                999,
                999,
                999,
                999,
                999,
                999,
                1f);
            AudienceReactionResult disabledResult =
                AudienceReactionResolver.Resolve(disabledProfile, audience);
            Require(
                disabledResult.PreferenceScore == 0 &&
                disabledResult.StageScore == 0 &&
                disabledResult.Value == 0,
                "A card with audience reactions disabled produced a reaction.",
                failures);

            var negativeProfile = new AudienceReactionProfile(
                true,
                -4,
                0,
                3,
                -3,
                1,
                3,
                1f);
            var calmChillAudience = new AudienceSnapshot(
                new AudienceId(2),
                CrowdPreference.Chill,
                20f,
                AudienceEngagementStage.Calm,
                0f);
            AudienceReactionResult negativeResult =
                AudienceReactionResolver.Resolve(
                    negativeProfile,
                    calmChillAudience);
            Require(
                negativeResult.PreferenceScore == -4 &&
                negativeResult.StageScore == -3 &&
                negativeResult.Value == -7,
                "Negative audience reaction values were clamped or mixed up.",
                failures);
        }

        static void ValidateMemberPrefab(List<string> failures)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AudienceFoundationSetup.MemberPrefabPath);
            Require(
                prefab != null,
                "Audience member prefab is missing.",
                failures);
            if (prefab == null) return;

            AudienceMemberActor actor =
                prefab.GetComponent<AudienceMemberActor>();
            AudienceReactionPopup popup =
                prefab.GetComponentInChildren<AudienceReactionPopup>(true);
            Require(
                actor != null,
                "AudienceMemberActor is missing from the member prefab.",
                failures);
            Require(
                popup != null,
                "AudienceReactionPopup is missing from the member prefab.",
                failures);
            if (actor == null || popup == null) return;

            Require(
                actor.ReactionPopup == popup,
                "AudienceMemberActor does not reference its reaction popup.",
                failures);
            Require(
                popup.IsConfigured && popup.ValueText != null,
                "Audience reaction popup text reference is incomplete.",
                failures);
        }

        static void Require(bool condition, string message, List<string> failures)
        {
            if (!condition) failures.Add(message);
        }
    }
}
