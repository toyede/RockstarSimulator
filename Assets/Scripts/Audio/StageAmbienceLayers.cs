using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 겹쳐 까는 앰비언스 한 겹.
    /// <see cref="minCombo"/> 가 0이면 공연 내내 깔리고, 0보다 크면 콤보가 그만큼 쌓였을 때만 들어온다.
    /// </summary>
    [Serializable]
    public class AmbienceLayer
    {
        [Tooltip("에디터에서 알아보기 위한 이름. 로직에는 쓰이지 않는다")]
        public string layerName = "Layer";

        [Tooltip("반복 재생할 클립")]
        public AudioClip clip;

        [Range(0f, 1f), Tooltip("이 겹의 볼륨. 앰비언스 채널·마스터 볼륨과 곱해진다")]
        public float volume = 0.5f;

        [Min(0), Tooltip(
            "이 콤보 이상일 때만 들어온다. 0이면 항상 깔린다. " +
            "콤보가 이 값 아래로 떨어지면 다시 빠진다")]
        public int minCombo;

        [Min(0.01f), Tooltip("들어오고 빠질 때의 페이드 시간(초)")]
        public float fadeDuration = 1.5f;

        [Tooltip("클립 안에서 재생을 시작할 지점(초). 여러 겹이 같은 지점에서 시작해 뭉치는 것을 막는다")]
        [Min(0f)] public float startOffset;

        [NonSerialized] public AudioSource Source;
        [NonSerialized] public float CurrentVolume;
    }

    /// <summary>
    /// 무대 앰비언스를 <b>여러 겹으로</b> 깔아 두는 컴포넌트.
    ///
    /// 겹마다 AudioSource 를 하나씩 갖고 각자 페이드하므로, 기존
    /// <see cref="CrowdAmbienceSystem"/>(호응도 티어를 <b>하나씩 크로스페이드</b>)과 목적이 다르다.
    /// 이쪽은 여러 소리가 동시에 겹쳐 쌓이는 구조다.
    ///
    /// 기본 구성:
    /// <list type="bullet">
    /// <item>amp_noise_ambience — 앰프 잡음. 공연 내내 깔린다</item>
    /// <item>festival_noise_ambience — 축제 웅성거림. 낮은 볼륨으로 항상</item>
    /// <item>crowd_middle — 콤보가 붙기 시작하면 들어온다</item>
    /// <item>crowd_high — 콤보가 더 쌓이면 들어온다</item>
    /// </list>
    ///
    /// 볼륨은 기존 앰비언스 채널(<c>CrowdAmbience.Volume</c>)을 그대로 따르므로
    /// 옵션 슬라이더가 이 소리들에도 함께 적용된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageAmbienceLayers : MonoBehaviour
    {
        [Header("겹")]
        [SerializeField, Tooltip("아래에서 위로 겹쳐 깔린다. 항목을 늘려도 코드는 그대로다")]
        List<AmbienceLayer> layers = new List<AmbienceLayer>();

        [Header("재생")]
        [SerializeField, Tooltip("씬 시작과 동시에 깔기 시작한다")]
        bool playOnStart = true;

        [SerializeField, Tooltip("공연이 끝나면(GameOver) 서서히 걷는다")]
        bool stopOnGameOver = true;

        [SerializeField, Min(0.01f), Tooltip("전체를 걷을 때의 페이드 시간(초)")]
        float stopFadeDuration = 1.5f;

        [SerializeField, Range(0f, 1f), Tooltip("이 컴포넌트 전체에 곱해지는 배율")]
        float masterScale = 1f;

        int _combo;
        bool _running;
        float _globalFade = 1f;   // 전체 페이드아웃용 (1 = 정상, 0 = 무음)
        float _globalTarget = 1f;

        /// <summary>앰비언스 채널 × 마스터 볼륨. 옵션 슬라이더가 그대로 반영된다.</summary>
        float ChannelVolume =>
            CrowdAmbience.Exists ? CrowdAmbience.Volume : 1f;

        float MasterVolume =>
            AudioManager.HasInstance ? AudioManager.Instance.MasterVolume : 1f;

        void Awake() => BuildSources();

        void OnEnable()
        {
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

            _combo = ComboSystem.HasInstance ? ComboSystem.Instance.CurrentCombo : 0;
            if (playOnStart) StartAmbience();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            StopImmediate();
        }

        void BuildSources()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                AmbienceLayer layer = layers[i];
                if (layer == null || layer.Source != null) continue;

                var go = new GameObject($"Ambience_{i:00}_{layer.layerName}");
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                source.clip = layer.clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // 2D — 화면 어디서 나든 같은 크기
                source.volume = 0f;
                layer.Source = source;
                layer.CurrentVolume = 0f;
            }
        }

        // ---------------- 공개 API ----------------

        public void StartAmbience()
        {
            BuildSources();
            _running = true;
            _globalTarget = 1f;
            _globalFade = Mathf.Max(_globalFade, 0.0001f);

            for (int i = 0; i < layers.Count; i++)
            {
                AmbienceLayer layer = layers[i];
                if (layer?.Source == null || layer.clip == null) continue;

                if (layer.Source.isPlaying) continue;
                layer.Source.clip = layer.clip;
                // 겹마다 다른 지점에서 시작해 같은 파형이 겹쳐 울리지 않게 한다
                layer.Source.time = Mathf.Clamp(
                    layer.startOffset,
                    0f,
                    Mathf.Max(0f, layer.clip.length - 0.05f));
                layer.Source.Play();
            }
        }

        /// <summary>서서히 걷는다. 소리는 페이드가 끝난 뒤에 멈춘다.</summary>
        public void StopAmbience() => _globalTarget = 0f;

        public void StopImmediate()
        {
            _running = false;
            _globalFade = 0f;
            _globalTarget = 0f;
            for (int i = 0; i < layers.Count; i++)
            {
                AmbienceLayer layer = layers[i];
                if (layer?.Source == null) continue;
                layer.Source.Stop();
                layer.CurrentVolume = 0f;
                layer.Source.volume = 0f;
            }
        }

        // ---------------- 이벤트 ----------------

        void OnComboChanged(ComboChanged e) => _combo = e.CurrentCombo;

        void OnGameStateChanged(GameStateChanged e)
        {
            switch (e.Current)
            {
                case GameState.Ready:
                case GameState.Playing:
                    _combo = 0;
                    StartAmbience();
                    break;
                case GameState.GameOver:
                    if (stopOnGameOver) StopAmbience();
                    break;
            }
        }

        // ---------------- 매 프레임 ----------------

        void Update()
        {
            // 일시정지(timeScale 0) 중에도 앰비언스는 계속 흐르는 편이 자연스럽다
            float delta = Time.unscaledDeltaTime;

            _globalFade = Mathf.MoveTowards(
                _globalFade,
                _globalTarget,
                delta / Mathf.Max(0.01f, stopFadeDuration));

            if (_globalFade <= 0.0001f && _globalTarget <= 0f && _running)
            {
                StopImmediate();
                return;
            }

            float channel = ChannelVolume * MasterVolume * Mathf.Clamp01(masterScale);

            for (int i = 0; i < layers.Count; i++)
            {
                AmbienceLayer layer = layers[i];
                if (layer?.Source == null) continue;

                // minCombo 0 = 항상, 그 외에는 콤보가 그만큼 쌓였을 때만
                bool active = _running && (layer.minCombo <= 0 || _combo >= layer.minCombo);
                float target = active ? layer.volume : 0f;

                layer.CurrentVolume = Mathf.MoveTowards(
                    layer.CurrentVolume,
                    target,
                    delta / Mathf.Max(0.01f, layer.fadeDuration));

                layer.Source.volume = layer.CurrentVolume * channel * _globalFade;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Start Ambience")] void DebugStart() => StartAmbience();
        [ContextMenu("Debug/Stop Ambience")] void DebugStop() => StopAmbience();

        [ContextMenu("Debug/Force Combo 10")]
        void DebugCombo10() => _combo = 10;

        [ContextMenu("Debug/Force Combo 0")]
        void DebugCombo0() => _combo = 0;
#endif
    }
}
