using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// [임시] 옵션 UI 가 나오기 전에 키보드로 앰비언스를 확인하는 디버그 컴포넌트.
    ///
    ///   [ / ]  : 앰비언스 볼륨 -/+ 10%
    ///   - / =  : 마스터 볼륨 -/+ 10%
    ///   M      : 앰비언스 음소거 토글
    ///   T      : 티어 강제 전환 (Low → Middle → High → 자동)
    ///
    /// 정식 옵션 패널(AudioVolumeSlider)이 생기면 이 컴포넌트는 비활성화만 해두면 된다.
    /// </summary>
    public class CrowdAmbienceDebugInput : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField, Tooltip("화면 우상단에 현재 티어/볼륨을 표시할지")]
        bool showOnScreenInfo = true;

        [SerializeField, Range(0.01f, 0.5f)] float volumeStep = 0.1f;
#endif

        int _forcedCycle = -1; // -1 = 자동(호응도 추종)

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.leftBracketKey.wasPressedThisFrame)  ShiftAmbienceVolume(-volumeStep);
            if (kb.rightBracketKey.wasPressedThisFrame) ShiftAmbienceVolume(+volumeStep);
            if (kb.minusKey.wasPressedThisFrame)        ShiftMasterVolume(-volumeStep);
            if (kb.equalsKey.wasPressedThisFrame)       ShiftMasterVolume(+volumeStep);
            if (kb.mKey.wasPressedThisFrame)            ToggleMute();
            if (kb.tKey.wasPressedThisFrame)            CycleForcedTier();
#else
            if (Input.GetKeyDown(KeyCode.LeftBracket))  ShiftAmbienceVolume(-volumeStep);
            if (Input.GetKeyDown(KeyCode.RightBracket)) ShiftAmbienceVolume(+volumeStep);
            if (Input.GetKeyDown(KeyCode.Minus))        ShiftMasterVolume(-volumeStep);
            if (Input.GetKeyDown(KeyCode.Equals))       ShiftMasterVolume(+volumeStep);
            if (Input.GetKeyDown(KeyCode.M))            ToggleMute();
            if (Input.GetKeyDown(KeyCode.T))            CycleForcedTier();
#endif
#endif
        }

        void ShiftAmbienceVolume(float delta) => CrowdAmbience.Volume = Mathf.Clamp01(CrowdAmbience.Volume + delta);

        void ShiftMasterVolume(float delta) =>
            AudioManager.Instance.MasterVolume = Mathf.Clamp01(AudioManager.Instance.MasterVolume + delta);

        void ToggleMute()
        {
            if (!CrowdAmbienceSystem.HasInstance) return;
            CrowdAmbienceSystem.Instance.SetMuted(!CrowdAmbienceSystem.Instance.IsMuted);
        }

        /// <summary>티어를 하나씩 강제로 올리다가 마지막을 넘으면 자동(호응도 추종)으로 돌아온다.</summary>
        void CycleForcedTier()
        {
            if (!CrowdAmbienceSystem.HasInstance) return;
            var system = CrowdAmbienceSystem.Instance;
            int count = system.Config != null ? system.Config.TierCount : 0;
            if (count == 0) return;

            _forcedCycle++;
            if (_forcedCycle >= count) _forcedCycle = -1;

            if (_forcedCycle < 0) system.ReleaseForcedTier();
            else system.ForceTier(_forcedCycle);
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showOnScreenInfo || !CrowdAmbienceSystem.HasInstance) return;

            var system = CrowdAmbienceSystem.Instance;
            string mode = _forcedCycle >= 0 ? "강제" : "자동";
            string mute = system.IsMuted ? "  [음소거]" : "";
            string msg = $"관객 앰비언스: {system.CurrentTierName} ({mode})\n" +
                         $"앰비언스 {Mathf.RoundToInt(CrowdAmbience.Volume * 100f)}%  " +
                         $"마스터 {Mathf.RoundToInt(AudioManager.Instance.MasterVolume * 100f)}%{mute}\n" +
                         $"[ ] 앰비언스  - = 마스터  M 음소거  T 티어";

            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.UpperRight };
            GUI.Label(new Rect(Screen.width - 440f, 20f, 420f, 90f), msg, style);
#endif
        }
    }
}
