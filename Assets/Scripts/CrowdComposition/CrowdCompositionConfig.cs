using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "CrowdCompositionConfig",
        menuName = "ContextStage/Crowd Composition Config")]
    public sealed class CrowdCompositionConfig : ScriptableObject
    {
        [Header("Composition")]
        [SerializeField, Min(1)] int expectedCrowdSize = 21;
        [SerializeField] string initialPresetId = "balanced";
        [SerializeField, Range(0f, 1f)] float goodReactionThreshold = 0.10f;

        [SerializeField] List<CrowdCompositionPreset> presets = new List<CrowdCompositionPreset>
        {
            new CrowdCompositionPreset(
                "balanced",
                "Balanced",
                "CROWD RESET",
                new CrowdCompositionSnapshot(7, 7, 7)),
            new CrowdCompositionPreset(
                "formal",
                "Formal Night",
                "FORMAL FANS ARRIVE",
                new CrowdCompositionSnapshot(13, 5, 3)),
            new CrowdCompositionPreset(
                "britpop",
                "Britpop Wave",
                "BRITPOP KIDS RUSH IN",
                new CrowdCompositionSnapshot(4, 13, 4)),
            new CrowdCompositionPreset(
                "hardcore",
                "Hardcore Surge",
                "HARDCORE FANS SURGE",
                new CrowdCompositionSnapshot(3, 5, 13))
        };

        [Header("Automatic Shifts")]
        [SerializeField] bool autoShift = true;
        [SerializeField] List<float> shiftTimes = new List<float> { 25f, 55f };
        [SerializeField] int shiftRandomSeed = 4861;

        public int ExpectedCrowdSize => expectedCrowdSize;
        public string InitialPresetId => initialPresetId;
        public float GoodReactionThreshold => goodReactionThreshold;
        public IReadOnlyList<CrowdCompositionPreset> Presets => presets;
        public bool AutoShift => autoShift;
        public int ShiftCount => shiftTimes != null ? shiftTimes.Count : 0;
        public int ShiftRandomSeed => shiftRandomSeed;

        public float GetShiftTime(int index) =>
            shiftTimes != null && index >= 0 && index < shiftTimes.Count
                ? shiftTimes[index]
                : float.PositiveInfinity;

        public bool TryGetPreset(string id, out CrowdCompositionPreset preset)
        {
            if (presets != null)
            {
                for (int i = 0; i < presets.Count; i++)
                {
                    if (string.Equals(presets[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    {
                        preset = presets[i];
                        return true;
                    }
                }
            }

            preset = default;
            return false;
        }

        public void FillShiftPresetIds(List<string> destination)
        {
            if (destination == null) return;
            destination.Clear();
            if (presets == null) return;

            for (int i = 0; i < presets.Count; i++)
            {
                string id = presets[i].Id;
                if (string.IsNullOrWhiteSpace(id) ||
                    string.Equals(id, initialPresetId, StringComparison.OrdinalIgnoreCase))
                    continue;
                destination.Add(id);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            expectedCrowdSize = Mathf.Max(1, expectedCrowdSize);
            goodReactionThreshold = Mathf.Clamp01(goodReactionThreshold);

            if (shiftTimes != null)
            {
                for (int i = 0; i < shiftTimes.Count; i++)
                    shiftTimes[i] = Mathf.Max(0f, shiftTimes[i]);
                shiftTimes.Sort();
            }

            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (presets == null) return;

            for (int i = 0; i < presets.Count; i++)
            {
                CrowdCompositionPreset preset = presets[i];
                if (string.IsNullOrWhiteSpace(preset.Id) || !ids.Add(preset.Id))
                    Debug.LogWarning($"[CrowdCompositionConfig] Empty or duplicate preset id at {i}.", this);
                if (!preset.Composition.IsValid(expectedCrowdSize))
                {
                    Debug.LogWarning(
                        $"[CrowdCompositionConfig] Preset '{preset.Id}' must total {expectedCrowdSize}.",
                        this);
                }
            }

            if (!TryGetPreset(initialPresetId, out _))
                Debug.LogWarning(
                    $"[CrowdCompositionConfig] Initial preset '{initialPresetId}' does not exist.",
                    this);
        }
#endif
    }
}
