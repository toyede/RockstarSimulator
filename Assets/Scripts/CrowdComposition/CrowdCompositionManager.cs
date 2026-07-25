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
        [Header("Composition")]
        [SerializeField] CrowdCompositionConfig config;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Prototype Debug")]
        [SerializeField] bool enableDebugInput = true;
        [SerializeField] bool enableDebugLogs = true;
#endif

        CrowdCompositionSnapshot _current;
        string _currentPresetId;
        string _currentPresetDisplayName;
        bool _initialized;

        public CrowdCompositionSnapshot Current => _current;
        public string CurrentPresetId => _currentPresetId;
        public string CurrentPresetDisplayName => _currentPresetDisplayName;
        public CrowdCompositionConfig Config => config;
        public int ExpectedCrowdSize => config != null ? config.ExpectedCrowdSize : 0;
        public bool IsConfigured => config != null;

        void Awake()
        {
            if (!EnsureInitialized()) enabled = false;
        }

        void OnEnable()
        {
            if (!EnsureInitialized()) return;
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!enableDebugInput) return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) ApplyDebugPreset(0);
            else if (keyboard.f2Key.wasPressedThisFrame) ApplyDebugPreset(1);
            else if (keyboard.f3Key.wasPressedThisFrame) ApplyDebugPreset(2);
            else if (keyboard.f4Key.wasPressedThisFrame) ApplyDebugPreset(3);
#else
            if (Input.GetKeyDown(KeyCode.F1)) ApplyDebugPreset(0);
            else if (Input.GetKeyDown(KeyCode.F2)) ApplyDebugPreset(1);
            else if (Input.GetKeyDown(KeyCode.F3)) ApplyDebugPreset(2);
            else if (Input.GetKeyDown(KeyCode.F4)) ApplyDebugPreset(3);
#endif
#endif
        }

        public int GetCount(CrowdPreference preference) => _current.GetCount(preference);
        public float GetRatio(CrowdPreference preference) => _current.GetRatio(preference);
        public CrowdReactionGrade EvaluateReaction(CrowdPreference preference)
        {
            if (config == null) return CrowdReactionGrade.Weak;
            return CrowdReactionEvaluator.Evaluate(
                _current,
                preference,
                config.GoodReactionThreshold);
        }

        public bool TryGetPreset(string presetId, out CrowdCompositionPreset preset)
        {
            if (config != null) return config.TryGetPreset(presetId, out preset);
            preset = default;
            return false;
        }

        public void FillShiftPresetIds(List<string> destination)
        {
            if (config == null) destination?.Clear();
            else config.FillShiftPresetIds(destination);
        }

        public bool TryApplyPreset(string presetId, string source = "Runtime")
        {
            if (!EnsureInitialized()) return false;
            if (!config.TryGetPreset(presetId, out CrowdCompositionPreset preset))
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
            if (!EnsureInitialized()) return false;
            return Apply(composition, string.Empty, "Custom", source);
        }

        public void ResetToInitialPreset()
        {
            if (config != null) TryApplyPreset(config.InitialPresetId, "Reset");
        }

        bool Apply(
            CrowdCompositionSnapshot composition,
            string presetId,
            string displayName,
            string source)
        {
            if (!composition.IsValid(config.ExpectedCrowdSize))
            {
                Debug.LogWarning(
                    $"[CrowdComposition] Rejected {composition.ChillCount}/" +
                    $"{composition.SingalongCount}/{composition.MoshCount}; " +
                    $"counts must total {config.ExpectedCrowdSize}.",
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[CrowdComposition] {_currentPresetDisplayName}: " +
                    $"{_current.ChillCount}/{_current.SingalongCount}/{_current.MoshCount}",
                    this);
            }
#endif

            return true;
        }

        bool EnsureInitialized()
        {
            if (_initialized) return true;
            if (config == null)
            {
                Debug.LogError(
                    "[CrowdComposition] CrowdCompositionConfig is required. " +
                    "Run Tools/Crowd/Setup Crowd Composition Prototype.",
                    this);
                return false;
            }
            if (config.TryGetPreset(config.InitialPresetId, out CrowdCompositionPreset initial) &&
                initial.Composition.IsValid(config.ExpectedCrowdSize))
            {
                Apply(initial.Composition, initial.Id, initial.DisplayName, "Initialization");
                return true;
            }

            Debug.LogError(
                $"[CrowdComposition] Initial preset '{config.InitialPresetId}' is missing or invalid.",
                this);
            return false;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
                ResetToInitialPreset();
        }

        void ApplyDebugPreset(int index)
        {
            if (config == null || index < 0 || index >= config.Presets.Count) return;
            TryApplyPreset(config.Presets[index].Id, "DebugKey");
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (config == null)
                Debug.LogWarning("[CrowdComposition] CrowdCompositionConfig is not assigned.", this);
        }
#endif

        [ContextMenu("Debug/Apply Initial Preset")] void DebugInitial() => ResetToInitialPreset();

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
                $"{CrowdReactionEvaluator.Evaluate(c, CrowdPreference.Chill, 0.10f)}/" +
                $"{CrowdReactionEvaluator.Evaluate(c, CrowdPreference.Singalong, 0.10f)}/" +
                $"{CrowdReactionEvaluator.Evaluate(c, CrowdPreference.Mosh, 0.10f)}");
        }
    }
}
