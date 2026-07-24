using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class CrowdCompositionManager : MonoBehaviour
    {
        // CrowdSpawner currently creates 3 rows x 7 members.
        public const int DefaultCrowdSize = 21;

        [Header("Composition")]
        [SerializeField, Min(1)] int expectedCrowdSize = DefaultCrowdSize;
        [SerializeField] string initialPresetId = "balanced";
        [SerializeField] List<CrowdCompositionPreset> presets = new List<CrowdCompositionPreset>
        {
            new CrowdCompositionPreset("balanced", "Balanced", new CrowdCompositionSnapshot(7, 7, 7)),
            new CrowdCompositionPreset("formal", "Formal Night", new CrowdCompositionSnapshot(13, 5, 3)),
            new CrowdCompositionPreset("britpop", "Britpop Wave", new CrowdCompositionSnapshot(4, 13, 4)),
            new CrowdCompositionPreset("hardcore", "Hardcore Surge", new CrowdCompositionSnapshot(3, 5, 13)),
        };

        [Header("Prototype Debug")]
        [SerializeField] bool enableDebugInput = true;
        [SerializeField] bool enableDebugLogs = true;

        CrowdCompositionSnapshot _current;
        string _currentPresetId;
        string _currentPresetDisplayName;
        bool _initialized;

        public CrowdCompositionSnapshot Current => _current;
        public string CurrentPresetId => _currentPresetId;
        public string CurrentPresetDisplayName => _currentPresetDisplayName;
        public int ExpectedCrowdSize => expectedCrowdSize;

        void Awake() => EnsureInitialized();

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EnsureInitialized();
        }

        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void Update()
        {
            if (!enableDebugInput) return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) TryApplyPreset("balanced", "DebugKey");
            else if (keyboard.f2Key.wasPressedThisFrame) TryApplyPreset("formal", "DebugKey");
            else if (keyboard.f3Key.wasPressedThisFrame) TryApplyPreset("britpop", "DebugKey");
            else if (keyboard.f4Key.wasPressedThisFrame) TryApplyPreset("hardcore", "DebugKey");
#else
            if (Input.GetKeyDown(KeyCode.F1)) TryApplyPreset("balanced", "DebugKey");
            else if (Input.GetKeyDown(KeyCode.F2)) TryApplyPreset("formal", "DebugKey");
            else if (Input.GetKeyDown(KeyCode.F3)) TryApplyPreset("britpop", "DebugKey");
            else if (Input.GetKeyDown(KeyCode.F4)) TryApplyPreset("hardcore", "DebugKey");
#endif
        }

        public int GetCount(CrowdPreference preference) => _current.GetCount(preference);
        public float GetRatio(CrowdPreference preference) => _current.GetRatio(preference);
        public CrowdReactionGrade EvaluateReaction(CrowdPreference preference) =>
            CrowdReactionEvaluator.Evaluate(_current, preference);

        public bool TryApplyPreset(string presetId, string source = "Runtime")
        {
            EnsureInitialized();
            if (!TryFindPreset(presetId, out CrowdCompositionPreset preset))
            {
                Debug.LogWarning($"[CrowdComposition] Unknown preset '{presetId}'.", this);
                return false;
            }

            return Apply(
                preset.Composition,
                preset.Id,
                preset.DisplayName,
                source);
        }

        public bool TryApplyComposition(CrowdCompositionSnapshot composition, string source = "Runtime")
        {
            EnsureInitialized();
            return Apply(composition, string.Empty, "Custom", source);
        }

        public void ResetToInitialPreset() => TryApplyPreset(initialPresetId, "Reset");

        bool Apply(
            CrowdCompositionSnapshot composition,
            string presetId,
            string displayName,
            string source)
        {
            if (!composition.IsValid(expectedCrowdSize))
            {
                Debug.LogWarning(
                    $"[CrowdComposition] Rejected {composition.ChillCount}/" +
                    $"{composition.SingalongCount}/{composition.MoshCount}; " +
                    $"counts must total {expectedCrowdSize}.",
                    this);
                return false;
            }

            if (_initialized && composition == _current && presetId == _currentPresetId)
                return false;

            CrowdCompositionSnapshot previous = _current;
            string previousPresetId = _currentPresetId;

            _current = composition;
            _currentPresetId = presetId ?? string.Empty;
            _currentPresetDisplayName = string.IsNullOrEmpty(displayName) ? "Custom" : displayName;
            _initialized = true;

            EventBus.Raise(new CrowdCompositionChanged
            {
                Previous = previous,
                Current = _current,
                PreviousPresetId = previousPresetId,
                CurrentPresetId = _currentPresetId,
                Source = source ?? string.Empty
            });

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[CrowdComposition] {_currentPresetDisplayName}: " +
                    $"{_current.ChillCount}/{_current.SingalongCount}/{_current.MoshCount}",
                    this);
            }

            return true;
        }

        void EnsureInitialized()
        {
            if (_initialized) return;

            if (TryFindPreset(initialPresetId, out CrowdCompositionPreset initial) &&
                initial.Composition.IsValid(expectedCrowdSize))
            {
                Apply(initial.Composition, initial.Id, initial.DisplayName, "Initialization");
                return;
            }

            _current = new CrowdCompositionSnapshot(7, 7, 7);
            _currentPresetId = "balanced";
            _currentPresetDisplayName = "Balanced";
            _initialized = true;
            Debug.LogWarning("[CrowdComposition] Invalid initial preset; using 7/7/7 fallback.", this);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
                ResetToInitialPreset();
        }

        bool TryFindPreset(string id, out CrowdCompositionPreset preset)
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

#if UNITY_EDITOR
        void OnValidate()
        {
            expectedCrowdSize = Mathf.Max(1, expectedCrowdSize);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (presets == null) return;

            for (int i = 0; i < presets.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(presets[i].Id) || !ids.Add(presets[i].Id))
                    Debug.LogWarning($"[CrowdComposition] Empty or duplicate preset id at {i}.", this);
                if (!presets[i].Composition.IsValid(expectedCrowdSize))
                    Debug.LogWarning(
                        $"[CrowdComposition] Preset '{presets[i].Id}' must total {expectedCrowdSize}.",
                        this);
            }
        }
#endif

        [ContextMenu("Debug/Apply Balanced")] void DebugBalanced() => TryApplyPreset("balanced", "Inspector");
        [ContextMenu("Debug/Apply Formal Night")] void DebugFormal() => TryApplyPreset("formal", "Inspector");
        [ContextMenu("Debug/Apply Britpop Wave")] void DebugBritpop() => TryApplyPreset("britpop", "Inspector");
        [ContextMenu("Debug/Apply Hardcore Surge")] void DebugHardcore() => TryApplyPreset("hardcore", "Inspector");

        [ContextMenu("Debug/Run Reaction Evaluator Self Test")]
        void DebugSelfTest()
        {
            LogCase(new CrowdCompositionSnapshot(7, 7, 7));
            LogCase(new CrowdCompositionSnapshot(13, 5, 3));
            LogCase(new CrowdCompositionSnapshot(4, 13, 4));
            LogCase(new CrowdCompositionSnapshot(3, 5, 13));
            LogCase(new CrowdCompositionSnapshot(10, 8, 3));
        }

        static void LogCase(CrowdCompositionSnapshot c)
        {
            Debug.Log(
                $"[CrowdComposition Test] {c.ChillCount}/{c.SingalongCount}/{c.MoshCount}: " +
                $"{CrowdReactionEvaluator.Evaluate(c, CrowdPreference.Chill)}/" +
                $"{CrowdReactionEvaluator.Evaluate(c, CrowdPreference.Singalong)}/" +
                $"{CrowdReactionEvaluator.Evaluate(c, CrowdPreference.Mosh)}");
        }
    }
}
