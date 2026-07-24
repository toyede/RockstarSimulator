using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 무대 BGM 재생기. 씬이 시작되면 지정한 BGM 을 페이드인으로 튼다.
    ///
    /// 곡 자체의 볼륨은 SoundLibrary 의 항목 볼륨(big_rock = 0.5)에서 조절하고,
    /// 플레이어가 옵션에서 만지는 값은 AudioVolumeSlider(Bgm 채널)가 담당한다.
    /// 즉 "곡별 밸런스"와 "유저 설정"이 섞이지 않는다.
    ///
    /// 재생은 킷의 AudioManager BGM 채널(크로스페이드 2소스)을 쓰므로,
    /// 다른 곡으로 바꾸면 자동으로 크로스페이드된다.
    ///   Bgm.Play("big_rock");   ← 다른 담당자는 이 한 줄이면 곡 교체 가능
    /// </summary>
    [DisallowMultipleComponent]
    public class StageBgmPlayer : MonoBehaviour
    {
        /// <summary>언제 BGM 을 시작할지. 연출 흐름이 바뀌어도 코드 수정 없이 인스펙터에서 고른다.</summary>
        public enum StartTrigger
        {
            SceneStart,     // 씬이 열리자마자 (타이틀/대기 화면부터 음악)
            PerformanceStart, // 공연 시작(Ready → Playing) 시점부터
            Manual,         // 아무것도 하지 않음. 다른 스크립트가 Play() 호출
        }

        [SerializeField, Tooltip("SoundLibrary 에 등록한 BGM ID")]
        string bgmId = "big_rock";

        [SerializeField, Tooltip("재생을 시작하는 시점")]
        StartTrigger startOn = StartTrigger.SceneStart;

        [SerializeField, Tooltip("페이드인 시간(초)")]
        float fadeInDuration = 1.5f;

        [SerializeField, Tooltip("게임오버 시 BGM 을 멈출지")]
        bool stopOnGameOver = false;

        [SerializeField, Tooltip("게임오버 페이드아웃 시간(초)")]
        float fadeOutDuration = 1.5f;

        public string BgmId => bgmId;

        void Start()
        {
            if (startOn == StartTrigger.SceneStart) Play();
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void OnGameStateChanged(GameStateChanged e)
        {
            if (startOn == StartTrigger.PerformanceStart &&
                e.Previous == GameState.Ready && e.Current == GameState.Playing)
                Play();

            if (stopOnGameOver && e.Current == GameState.GameOver) Stop();
        }

        /// <summary>지정된 BGM 을 재생한다. 이미 같은 곡이 나오는 중이면 킷이 알아서 무시한다.</summary>
        public void Play() => Sound.Bgm(bgmId, fadeInDuration);

        /// <summary>다른 곡으로 교체 (크로스페이드).</summary>
        public void Play(string id)
        {
            bgmId = id;
            Play();
        }

        public void Stop() => Sound.StopBgm(fadeOutDuration);
    }

    /// <summary>
    /// BGM 을 한 줄로 다루는 전역 접근자. (Sound / Hype / CrowdAmbience 와 같은 패턴)
    ///
    ///   Bgm.Play("big_rock");   Bgm.Stop();   Bgm.Volume = 0.5f;
    /// </summary>
    public static class Bgm
    {
        /// <summary>현재 재생 중인 BGM ID (없으면 null).</summary>
        public static string Current => AudioManager.HasInstance ? AudioManager.Instance.CurrentBgmId : null;

        /// <summary>BGM 채널 볼륨(0~1). PlayerPrefs 에 자동 저장된다. (곡별 볼륨은 SoundLibrary 에서)</summary>
        public static float Volume
        {
            get => AudioManager.Instance.BgmVolume;
            set => AudioManager.Instance.BgmVolume = value;
        }

        public static void Play(string id, float fade = 1.5f) => Sound.Bgm(id, fade);
        public static void Stop(float fade = 1.5f) => Sound.StopBgm(fade);
    }
}
