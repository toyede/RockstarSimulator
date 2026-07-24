using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>사운드 볼륨 채널. 채널을 늘리려면 여기에 항목을 추가하고 AudioVolumeSlider 의 스위치만 채우면 된다.</summary>
    public enum VolumeChannel
    {
        Master,
        Bgm,
        Sfx,
        CrowdAmbience,
    }

    /// <summary>
    /// UI Slider 하나를 볼륨 채널 하나에 연결하는 어댑터.
    /// 옵션 패널의 슬라이더에 붙이고 channel 만 고르면 끝이고, 값은 PlayerPrefs 에 자동 저장된다.
    ///
    /// (킷의 Sound.MasterVolume / 프로젝트의 CrowdAmbience.Volume 을 감싸므로
    ///  UI 담당은 사운드 내부 구조를 몰라도 된다)
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class AudioVolumeSlider : MonoBehaviour
    {
        [SerializeField] VolumeChannel channel = VolumeChannel.Master;

        [SerializeField, Tooltip("옆에 볼륨 퍼센트를 표시할 텍스트 (선택)")]
        Text valueLabel;

        Slider _slider;

        void Awake()
        {
            _slider = GetComponent<Slider>();
            _slider.minValue = 0f;
            _slider.maxValue = 1f;
            _slider.wholeNumbers = false;
        }

        void OnEnable()
        {
            _slider.SetValueWithoutNotify(GetVolume()); // 저장된 값으로 슬라이더 위치 복원
            UpdateLabel(_slider.value);
            _slider.onValueChanged.AddListener(OnSliderChanged);
        }

        void OnDisable() => _slider.onValueChanged.RemoveListener(OnSliderChanged);

        void OnSliderChanged(float value)
        {
            SetVolume(value);
            UpdateLabel(value);
        }

        void UpdateLabel(float value)
        {
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        float GetVolume()
        {
            switch (channel)
            {
                case VolumeChannel.Master:        return AudioManager.Instance.MasterVolume;
                case VolumeChannel.Bgm:           return AudioManager.Instance.BgmVolume;
                case VolumeChannel.Sfx:           return AudioManager.Instance.SfxVolume;
                case VolumeChannel.CrowdAmbience: return ContextStage.CrowdAmbience.Volume;
                default:                          return 1f;
            }
        }

        void SetVolume(float value)
        {
            switch (channel)
            {
                case VolumeChannel.Master:        AudioManager.Instance.MasterVolume = value; break;
                case VolumeChannel.Bgm:           AudioManager.Instance.BgmVolume = value;    break;
                case VolumeChannel.Sfx:           AudioManager.Instance.SfxVolume = value;    break;
                case VolumeChannel.CrowdAmbience: ContextStage.CrowdAmbience.Volume = value;  break;
            }
        }
    }
}
