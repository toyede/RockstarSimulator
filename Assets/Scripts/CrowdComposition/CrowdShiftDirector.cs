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
    public sealed class CrowdShiftDirector : MonoBehaviour
    {
        [SerializeField] CrowdCompositionManager manager;
        [SerializeField] bool autoShift = true;
        [SerializeField] float firstShiftTime = 25f;
        [SerializeField] float secondShiftTime = 55f;
        [SerializeField] int randomSeed = 4861;

        readonly string[] _presetIds = { "formal", "britpop", "hardcore" };
        readonly List<string> _shuffleBag = new List<string>();
        System.Random _random;
        float _performanceTime;
        int _nextShiftIndex;
        bool _wasPlaying;

        void Awake()
        {
            if (manager == null) manager = GetComponent<CrowdCompositionManager>();
            if (manager == null) manager = FindFirstObjectByType<CrowdCompositionManager>();
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

            if (!autoShift || manager == null || _nextShiftIndex >= 2) return;

            _performanceTime += Time.deltaTime;
            float triggerTime = _nextShiftIndex == 0 ? firstShiftTime : secondShiftTime;
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
            CrowdCompositionSnapshot target = PreviewPreset(presetId);
            EventBus.Raise(new CrowdShiftStarted
            {
                EventName = DisplayName(presetId),
                TargetPresetId = presetId,
                Target = target
            });
            manager.TryApplyPreset(presetId, "CrowdShift");
        }

        string DrawPreset()
        {
            if (_shuffleBag.Count == 0) RefillBag();

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
            _random = new System.Random(randomSeed);
            RefillBag();
        }

        static string DisplayName(string presetId)
        {
            switch (presetId)
            {
                case "formal": return "FORMAL FANS ARRIVE";
                case "britpop": return "BRITPOP KIDS RUSH IN";
                case "hardcore": return "HARDCORE FANS SURGE";
                default: return "CROWD SHIFT";
            }
        }

        static CrowdCompositionSnapshot PreviewPreset(string presetId)
        {
            switch (presetId)
            {
                case "formal": return new CrowdCompositionSnapshot(13, 5, 3);
                case "britpop": return new CrowdCompositionSnapshot(4, 13, 4);
                case "hardcore": return new CrowdCompositionSnapshot(3, 5, 13);
                default: return new CrowdCompositionSnapshot(7, 7, 7);
            }
        }
    }
}
