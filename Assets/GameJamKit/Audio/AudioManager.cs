using System.Collections;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// FMOD 없이 Unity 기본 AudioSource 만으로 동작하는 사운드 매니저.
    /// SFX 는 보이스 풀로 돌려쓰고, BGM 은 두 소스로 크로스페이드한다.
    /// 볼륨은 PlayerPrefs 에 자동 저장된다.
    ///
    /// 팀원은 대부분 static 래퍼 Sound 만 쓰면 된다:  Sound.Play("hit");
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        public const string ResourcesLibraryName = "SoundLibrary";

        [SerializeField] SoundLibrary library;
        [SerializeField, Range(1, 32)] int sfxVoices = 12;

        AudioSource[] _sfx;
        AudioSource _bgmA, _bgmB;
        bool _usingA = true;
        int _nextVoice;
        Coroutine _bgmRoutine;

        float _master = 1f, _bgmVolume = 1f, _sfxVolume = 1f;

        public SoundLibrary Library => library;
        public string CurrentBgmId { get; private set; }

        public float MasterVolume
        {
            get => _master;
            set { _master = Mathf.Clamp01(value); Save.SetFloat("vol_master", _master); ApplyBgmVolume(); }
        }

        public float BgmVolume
        {
            get => _bgmVolume;
            set { _bgmVolume = Mathf.Clamp01(value); Save.SetFloat("vol_bgm", _bgmVolume); ApplyBgmVolume(); }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set { _sfxVolume = Mathf.Clamp01(value); Save.SetFloat("vol_sfx", _sfxVolume); }
        }

        protected override void OnAwake()
        {
            if (library == null) library = Resources.Load<SoundLibrary>(ResourcesLibraryName);
            if (library == null)
                Debug.LogWarning($"[AudioManager] Resources/{ResourcesLibraryName}.asset 을 찾지 못했습니다. " +
                                 "Create/GameJamKit/Sound Library 로 만들어 Resources 폴더에 넣어주세요.");

            _master = Save.GetFloat("vol_master", 1f);
            _bgmVolume = Save.GetFloat("vol_bgm", 1f);
            _sfxVolume = Save.GetFloat("vol_sfx", 1f);

            _sfx = new AudioSource[sfxVoices];
            for (int i = 0; i < sfxVoices; i++) _sfx[i] = CreateSource($"SFX_{i}");

            _bgmA = CreateSource("BGM_A");
            _bgmB = CreateSource("BGM_B");
            _bgmA.loop = _bgmB.loop = true;
        }

        AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D 기본
            return src;
        }

        /// <summary>런타임에 라이브러리를 교체/주입할 때 사용.</summary>
        public void SetLibrary(SoundLibrary lib)
        {
            library = lib;
            if (lib != null) lib.InvalidateCache();
        }

        // ---------------- SFX ----------------

        /// <summary>Button.OnClick() 같은 UnityEvent 인스펙터에 바로 연결하기 위한 1-파라미터 오버로드.</summary>
        public void PlaySfx(string id) => PlaySfx(id, 1f);

        public void PlaySfx(string id, float volumeScale = 1f)
        {
            var entry = Resolve(id);
            if (entry == null) return;

            var src = GetFreeVoice();
            ConfigureVoice(src, entry, volumeScale);
            src.spatialBlend = 0f;
            src.Play();
        }

        /// <summary>월드 좌표에서 재생 (거리 감쇠). 2D 게임에서는 보통 PlaySfx 로 충분하다.</summary>
        public void PlaySfxAt(string id, Vector3 worldPosition, float volumeScale = 1f, float spatialBlend = 1f)
        {
            var entry = Resolve(id);
            if (entry == null) return;

            var src = GetFreeVoice();
            ConfigureVoice(src, entry, volumeScale);
            src.transform.position = worldPosition;
            src.spatialBlend = Mathf.Clamp01(spatialBlend);
            src.Play();
        }

        /// <summary>라이브러리에 등록하지 않은 클립을 즉석에서 재생.</summary>
        public void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var src = GetFreeVoice();
            src.transform.localPosition = Vector3.zero;
            src.clip = clip;
            src.loop = false;
            src.volume = Mathf.Clamp01(volume) * _sfxVolume * _master;
            src.pitch = pitch;
            src.spatialBlend = 0f;
            src.Play();
        }

        SoundEntry Resolve(string id)
        {
            if (library == null) return null;

            var entry = library.Find(id);
            if (entry == null)
            {
                Debug.LogWarning($"[AudioManager] 사운드 ID '{id}' 를 라이브러리에서 찾을 수 없습니다.");
                return null;
            }

            if (Time.unscaledTime - entry.LastPlayTime < entry.minInterval) return null; // 같은 프레임 중첩 방지
            entry.LastPlayTime = Time.unscaledTime;
            return entry;
        }

        void ConfigureVoice(AudioSource src, SoundEntry entry, float volumeScale)
        {
            src.transform.localPosition = Vector3.zero;
            src.clip = entry.PickClip();
            src.loop = false;
            src.volume = entry.volume * Mathf.Clamp01(volumeScale) * _sfxVolume * _master;
            src.pitch = entry.PickPitch();
        }

        AudioSource GetFreeVoice()
        {
            for (int i = 0; i < _sfx.Length; i++)
            {
                var src = _sfx[(_nextVoice + i) % _sfx.Length];
                if (!src.isPlaying)
                {
                    _nextVoice = (_nextVoice + i + 1) % _sfx.Length;
                    return src;
                }
            }
            // 전부 재생 중이면 가장 오래된 것을 뺏는다
            var stolen = _sfx[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _sfx.Length;
            stolen.Stop();
            return stolen;
        }

        // ---------------- BGM ----------------

        public void PlayBgm(string id, float fadeDuration = 1f)
        {
            if (CurrentBgmId == id && Active.isPlaying) return;

            var entry = library != null ? library.Find(id) : null;
            var clip = entry?.PickClip();
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] BGM ID '{id}' 를 찾을 수 없습니다.");
                return;
            }

            CurrentBgmId = id;
            float target = entry.volume * _bgmVolume * _master;

            var from = Active;
            var to = Inactive;
            to.clip = clip;
            to.pitch = entry.PickPitch();
            to.volume = 0f;
            to.Play();
            _usingA = !_usingA;

            RestartBgmRoutine(CrossfadeRoutine(from, to, target, fadeDuration));
        }

        public void StopBgm(float fadeDuration = 1f)
        {
            CurrentBgmId = null;
            RestartBgmRoutine(CrossfadeRoutine(Active, null, 0f, fadeDuration));
        }

        public void PauseBgm() { _bgmA.Pause(); _bgmB.Pause(); }
        public void ResumeBgm() { _bgmA.UnPause(); _bgmB.UnPause(); }

        AudioSource Active => _usingA ? _bgmA : _bgmB;
        AudioSource Inactive => _usingA ? _bgmB : _bgmA;

        void ApplyBgmVolume()
        {
            var entry = CurrentBgmId != null && library != null ? library.Find(CurrentBgmId) : null;
            float baseVolume = entry?.volume ?? 1f;
            if (_bgmRoutine == null) Active.volume = baseVolume * _bgmVolume * _master;
        }

        void RestartBgmRoutine(IEnumerator routine)
        {
            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(routine);
        }

        IEnumerator CrossfadeRoutine(AudioSource from, AudioSource to, float targetVolume, float duration)
        {
            float fromStart = from != null ? from.volume : 0f;
            float t = 0f;

            while (t < duration && duration > 0f)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (to != null) to.volume = Mathf.Lerp(0f, targetVolume, k);
                yield return null;
            }

            if (from != null) { from.volume = 0f; from.Stop(); }
            if (to != null) to.volume = targetVolume;
            _bgmRoutine = null;
        }
    }

    /// <summary>
    /// 팀원이 실제로 쓰는 한 줄 API.
    /// Sound.Play("hit");  Sound.Bgm("stage1");  Sound.StopBgm();
    /// </summary>
    public static class Sound
    {
        public static void Play(string id, float volumeScale = 1f) => AudioManager.Instance?.PlaySfx(id, volumeScale);
        public static void PlayAt(string id, Vector3 position, float volumeScale = 1f) => AudioManager.Instance?.PlaySfxAt(id, position, volumeScale);
        public static void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f) => AudioManager.Instance?.PlayClip(clip, volume, pitch);

        public static void Bgm(string id, float fade = 1f) => AudioManager.Instance?.PlayBgm(id, fade);
        public static void StopBgm(float fade = 1f) => AudioManager.Instance?.StopBgm(fade);

        // 볼륨은 자동으로 PlayerPrefs 에 저장된다 (옵션 슬라이더에 바로 연결)
        public static float MasterVolume { get => AudioManager.Instance.MasterVolume; set => AudioManager.Instance.MasterVolume = value; }
        public static float BgmVolume    { get => AudioManager.Instance.BgmVolume;    set => AudioManager.Instance.BgmVolume = value; }
        public static float SfxVolume    { get => AudioManager.Instance.SfxVolume;    set => AudioManager.Instance.SfxVolume = value; }
    }
}
