using GameJamKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 드롭다운에서 무테 전체 화면과 창 모드를 선택한다.
    /// 선택한 모드는 PlayerPrefs에 저장되어 다음 실행 시 복원된다.
    /// </summary>
    [RequireComponent(typeof(Dropdown))]
    public sealed class DisplayModeDropdown : MonoBehaviour, IPointerClickHandler
    {
        const string FullscreenSaveKey = "display_fullscreen";
        const int FullscreenOptionIndex = 0;
        const int WindowedOptionIndex = 1;

        [SerializeField] Dropdown dropdown;

        [SerializeField] string fullscreenLabel = "전체 화면";
        [SerializeField] string windowedLabel = "창 모드";
        [SerializeField] string clickSoundId = "ui_click_wooden";

        [Header("Windowed")]
        [SerializeField, Min(640)] int windowedWidth = 1280;
        [SerializeField, Min(360)] int windowedHeight = 720;

        bool _initialized;
        bool _isFullscreen;

        void Awake()
        {
            if (dropdown == null) dropdown = GetComponent<Dropdown>();
            ConfigureOptions();
        }

        void OnEnable()
        {
            if (!_initialized)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // Browsers own the WebGL canvas size. Applying a saved desktop
                // resolution here desynchronizes Unity's render target from the
                // canvas until the browser emits a resize event.
                _isFullscreen = IsFullscreen();
                SyncDropdown();
#else
                _isFullscreen = Save.Has(FullscreenSaveKey)
                    ? Save.GetBool(FullscreenSaveKey)
                    : IsFullscreen();

                ApplyDisplayMode(savePreference: false);
#endif
                _initialized = true;
            }
            else
            {
                // Alt+Enter 등 외부에서 바뀐 상태도 옵션을 다시 열 때 반영한다.
                _isFullscreen = IsFullscreen();
                Save.SetBool(FullscreenSaveKey, _isFullscreen);
                SyncDropdown();
            }

            dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
        }

        void OnDisable()
        {
            if (dropdown != null)
                dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && dropdown.interactable)
                PlayClickSound();
        }

        void ConfigureOptions()
        {
            dropdown.ClearOptions();
            dropdown.options.Add(new Dropdown.OptionData(fullscreenLabel));
            dropdown.options.Add(new Dropdown.OptionData(windowedLabel));
            dropdown.RefreshShownValue();
        }

        void OnDropdownValueChanged(int selectedIndex)
        {
            bool shouldUseFullscreen = selectedIndex == FullscreenOptionIndex;
            if (_isFullscreen == shouldUseFullscreen)
            {
                SyncDropdown();
                return;
            }

            _isFullscreen = shouldUseFullscreen;
            ApplyDisplayMode(savePreference: true);
            PlayClickSound();
        }

        void ApplyDisplayMode(bool savePreference)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // A browser fullscreen request must originate from the user's input.
            // Do not call Screen.SetResolution on WebGL: the HTML/CSS canvas is
            // responsible for sizing and Unity synchronizes its render target to it.
            Screen.fullScreen = _isFullscreen;

            if (savePreference) Save.SetBool(FullscreenSaveKey, _isFullscreen);
            SyncDropdown();
            return;
#else
            if (_isFullscreen)
            {
                Resolution nativeResolution = Screen.currentResolution;
                Screen.SetResolution(
                    nativeResolution.width,
                    nativeResolution.height,
                    FullScreenMode.FullScreenWindow);
            }
            else
            {
                Screen.SetResolution(
                    windowedWidth,
                    windowedHeight,
                    FullScreenMode.Windowed);
            }

            if (savePreference) Save.SetBool(FullscreenSaveKey, _isFullscreen);
            SyncDropdown();
#endif
        }

        void SyncDropdown()
        {
            dropdown.SetValueWithoutNotify(
                _isFullscreen ? FullscreenOptionIndex : WindowedOptionIndex);
            dropdown.RefreshShownValue();
        }

        void PlayClickSound()
        {
            if (!string.IsNullOrEmpty(clickSoundId))
                Sound.Play(clickSoundId);
        }

        static bool IsFullscreen() => Screen.fullScreenMode != FullScreenMode.Windowed;

#if UNITY_EDITOR
        void OnValidate()
        {
            windowedWidth = Mathf.Max(640, windowedWidth);
            windowedHeight = Mathf.Max(360, windowedHeight);

            if (dropdown == null)
                dropdown = GetComponent<Dropdown>();
        }
#endif
    }
}
