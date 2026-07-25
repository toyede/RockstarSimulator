using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ContextStage.EditorTools
{
    public static class ProjectRefactorValidation
    {
        static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Main.unity",
            "Assets/Scenes/Hwi.unity",
            "Assets/Scenes/JWY_Sample.unity",
            "Assets/Scenes/Minyoung.unity",
            "Assets/Scenes/Minyoung2.unity",
        };

        sealed class ValidationTier : IHypeTier
        {
            public float MinNormalized { get; set; }
        }

        readonly struct ValidationEvent
        {
        }

        [MenuItem("Tools/Validation/Validate Refactor Setup")]
        public static void Validate()
        {
            Scene original = EditorSceneManager.GetActiveScene();
            if (original.isDirty)
                throw new InvalidOperationException(
                    "Save the active scene before running refactor validation.");

            string originalPath = original.path;
            var failures = new List<string>();

            try
            {
                ValidatePureRuntimeRules(failures);
                for (int i = 0; i < ScenePaths.Length; i++)
                {
                    EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
                    ValidateOpenScene(ScenePaths[i], failures);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalPath))
                    EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
            }

            if (failures.Count > 0)
                throw new InvalidOperationException(
                    "Refactor validation failed:\n- " + string.Join("\n- ", failures));

            Debug.Log(
                $"[Validation] Refactor setup passed for {ScenePaths.Length} scenes.");
        }

        static void ValidatePureRuntimeRules(List<string> failures)
        {
            var tiers = new List<ValidationTier> { null };
            if (HypeTierUtil.Resolve(tiers, 1f) != -1)
                failures.Add("HypeTierUtil accepted a list containing no valid tiers.");

            int firstCalls = 0;
            int secondCalls = 0;
            Action<ValidationEvent> first = null;
            Action<ValidationEvent> second = _ => secondCalls++;
            first = _ =>
            {
                firstCalls++;
                EventBus.Unsubscribe(first);
            };

            EventBus.Subscribe(first);
            EventBus.Subscribe(second);
            EventBus.Raise(new ValidationEvent());
            EventBus.Raise(new ValidationEvent());
            EventBus.Unsubscribe(first);
            EventBus.Unsubscribe(second);

            if (firstCalls != 1 || secondCalls != 2)
                failures.Add(
                    $"EventBus mutation semantics changed ({firstCalls}, {secondCalls}).");
        }

        static void ValidateOpenScene(string scenePath, List<string> failures)
        {
            string prefix = $"[{scenePath}]";

            CrowdCompositionManager composition =
                UnityEngine.Object.FindFirstObjectByType<CrowdCompositionManager>();
            CrowdSpawner spawner = UnityEngine.Object.FindFirstObjectByType<CrowdSpawner>();
            CardSystem cards = UnityEngine.Object.FindFirstObjectByType<CardSystem>();
            HypeSystem hype = UnityEngine.Object.FindFirstObjectByType<HypeSystem>();
            SpecialAudienceManager special =
                UnityEngine.Object.FindFirstObjectByType<SpecialAudienceManager>();

            Require(composition != null, $"{prefix} CrowdCompositionManager missing.", failures);
            Require(spawner != null, $"{prefix} CrowdSpawner missing.", failures);
            Require(cards != null, $"{prefix} CardSystem missing.", failures);
            Require(hype != null && hype.Config != null, $"{prefix} HypeConfig missing.", failures);
            Require(
                special != null && special.Config != null,
                $"{prefix} SpecialAudienceConfig missing.",
                failures);

            Light2D[] lights = UnityEngine.Object.FindObjectsByType<Light2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int globalLightCount = 0;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].lightType == Light2D.LightType.Global)
                    globalLightCount++;
            }
            // 일부 작업 씬은 조명을 별도 씬에서 공급하므로 0개는 허용하고 중복만 차단한다.
            Require(
                globalLightCount <= 1,
                $"{prefix} expected at most one Global Light 2D, found {globalLightCount}.",
                failures);

            if (composition != null)
            {
                Require(
                    composition.Config != null,
                    $"{prefix} CrowdCompositionConfig missing.",
                    failures);
                ValidateCompositionConfig(prefix, composition.Config, failures);
            }

            if (spawner != null)
            {
                RequireReference(spawner, "compositionManager", prefix, failures);
                RequireReference(spawner, "chillPrefab", prefix, failures);
                RequireReference(spawner, "singalongPrefab", prefix, failures);
                RequireReference(spawner, "moshPrefab", prefix, failures);
            }

            if (cards != null)
                RequireReference(cards, "crowdComposition", prefix, failures);

            SpecialAudienceCrowdActor[] actors =
                UnityEngine.Object.FindObjectsByType<SpecialAudienceCrowdActor>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Require(
                actors.Length == 1,
                $"{prefix} expected one SpecialAudienceCrowdActor, found {actors.Length}.",
                failures);
            for (int i = 0; i < actors.Length; i++)
                RequireReference(actors[i], "crowdSpawner", prefix, failures);

            SpecialAudienceDropTarget[] targets =
                UnityEngine.Object.FindObjectsByType<SpecialAudienceDropTarget>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Require(
                targets.Length == 1,
                $"{prefix} expected one SpecialAudienceDropTarget, found {targets.Length}.",
                failures);
            for (int i = 0; i < targets.Length; i++)
            {
                Require(
                    targets[i].HitCollider != null,
                    $"{prefix} SpecialAudienceDropTarget collider missing.",
                    failures);
            }
        }

        static void ValidateCompositionConfig(
            string prefix,
            CrowdCompositionConfig config,
            List<string> failures)
        {
            if (config == null) return;
            Require(
                config.TryGetPreset(config.InitialPresetId, out CrowdCompositionPreset initial),
                $"{prefix} initial crowd preset missing.",
                failures);
            Require(
                initial.Composition.IsValid(config.ExpectedCrowdSize),
                $"{prefix} initial crowd preset total is invalid.",
                failures);

            for (int i = 0; i < config.Presets.Count; i++)
            {
                CrowdCompositionPreset preset = config.Presets[i];
                Require(
                    preset.Composition.IsValid(config.ExpectedCrowdSize),
                    $"{prefix} crowd preset '{preset.Id}' total is invalid.",
                    failures);
            }
        }

        static void RequireReference(
            UnityEngine.Object target,
            string fieldName,
            string prefix,
            List<string> failures)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            Require(
                property != null && property.objectReferenceValue != null,
                $"{prefix} {target.GetType().Name}.{fieldName} missing.",
                failures);
        }

        static void Require(bool condition, string message, List<string> failures)
        {
            if (!condition) failures.Add(message);
        }
    }
}
