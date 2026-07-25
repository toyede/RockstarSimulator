using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Changes the logical crowd composition at authored points during a performance.
    /// The spawner owns all visual transition work.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CrowdCompositionManager))]
    public sealed class CrowdShiftDirector : MonoBehaviour
    {
        [SerializeField] CrowdCompositionManager manager;

        readonly List<string> _presetIds = new List<string>();
        readonly List<string> _shuffleBag = new List<string>();
        System.Random _random;
        float _performanceTime;
        int _nextShiftIndex;
        bool _wasPlaying;

        void Awake()
        {
            if (manager == null) manager = GetComponent<CrowdCompositionManager>();
            ResetSchedule();
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void Update()
        {
            bool playing = GameManager.HasInstance && GameManager.Instance.IsPlaying;
            if (!playing)
            {
                _wasPlaying = false;
                return;
            }

            if (!_wasPlaying)
            {
                _wasPlaying = true;
                _performanceTime = 0f;
                _nextShiftIndex = 0;
            }

            CrowdCompositionConfig config = manager != null ? manager.Config : null;
            if (config == null || !config.AutoShift || _nextShiftIndex >= config.ShiftCount) return;

            _performanceTime += Time.deltaTime;
            float triggerTime = config.GetShiftTime(_nextShiftIndex);
            if (_performanceTime < triggerTime) return;

            ApplyNextShift();
            _nextShiftIndex++;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
                ResetSchedule();
        }

        void ApplyNextShift()
        {
            string presetId = DrawPreset();
            if (string.IsNullOrEmpty(presetId) ||
                !manager.TryGetPreset(presetId, out CrowdCompositionPreset preset))
                return;

            EventBus.Raise(new CrowdShiftStarted
            {
                EventName = string.IsNullOrWhiteSpace(preset.EventName)
                    ? preset.DisplayName
                    : preset.EventName,
                TargetPresetId = presetId,
                Target = preset.Composition
            });
            manager.TryApplyPreset(presetId, "CrowdShift");
        }

        string DrawPreset()
        {
            if (_shuffleBag.Count == 0) RefillBag();
            if (_shuffleBag.Count == 0) return string.Empty;

            int selectedIndex = _shuffleBag.Count - 1;
            if (_shuffleBag[selectedIndex] == manager.CurrentPresetId && _shuffleBag.Count > 1)
                selectedIndex--;

            string selected = _shuffleBag[selectedIndex];
            _shuffleBag.RemoveAt(selectedIndex);
            return selected;
        }

        void RefillBag()
        {
            _shuffleBag.Clear();
            _presetIds.Clear();
            if (manager != null) manager.FillShiftPresetIds(_presetIds);
            _shuffleBag.AddRange(_presetIds);
            for (int i = _shuffleBag.Count - 1; i > 0; i--)
            {
                int swap = _random.Next(i + 1);
                string value = _shuffleBag[i];
                _shuffleBag[i] = _shuffleBag[swap];
                _shuffleBag[swap] = value;
            }
        }

        void ResetSchedule()
        {
            _performanceTime = 0f;
            _nextShiftIndex = 0;
            _wasPlaying = false;
            int seed = manager != null && manager.Config != null
                ? manager.Config.ShiftRandomSeed
                : 0;
            _random = new System.Random(seed);
            RefillBag();
        }
    }
}
