using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 카드 한 장의 결과에 붙는 관객 반응 소리 한 줄.
    /// <see cref="minScore"/> 이상이면 이 소리를 쓴다 (내림차순으로 평가).
    /// </summary>
    [Serializable]
    public struct AudienceReactionSound
    {
        [Tooltip("에디터에서 알아보기 위한 이름")]
        public string label;

        [Tooltip("획득 점수가 이 값 이상이면 이 소리. 리스트에서 가장 높은 조건이 우선한다")]
        public int minScore;

        [Tooltip("재생할 클립. 비우면 이 구간은 무음이 된다")]
        public AudioClip clip;

        [Range(0f, 1f)] public float volume;

        [Min(0f), Tooltip("클립에서 재생을 시작할 지점(초). 긴 클립의 원하는 부분만 쓸 때")]
        public float startTime;

        [Min(0f), Tooltip(
            "재생 길이(초). 0이면 클립 끝까지. " +
            "귀뚜라미처럼 여러 번 반복되는 클립을 한 번만 쓰려면 여기서 자른다")]
        public float duration;

        [Tooltip("피치 랜덤 범위. 같은 소리가 반복될 때 기계적으로 들리지 않게 한다")]
        public Vector2 pitchRange;
    }

    /// <summary>
    /// 카드 결과에 관객 반응 소리를 붙여 <b>타격감</b>을 만든다.
    ///
    /// 점수 구간별로 소리가 갈린다 — 잘하면 발 구르기·휘파람, 못하면 헛기침·귀뚜라미.
    /// 아주 크게 망하면 그 위에 <b>야유</b>가 겹친다.
    ///
    /// 구간 판정은 <see cref="CardReactionTextUI"/> 의 라벨 구간과 같은 기준
    /// (<c>CardResolved.GainedScore</c>)을 쓰므로, 화면에 뜨는 문구와 소리가 어긋나지 않는다.
    ///
    /// <b>클립을 잘라 쓸 수 있다.</b> 귀뚜라미 클립은 4번 우는 2초짜리라 그대로 쓰면
    /// 카드 한 장에 네 번 운다. <c>startTime</c> · <c>duration</c> 으로 한 번만 쓴다.
    /// (AudioManager 의 원샷 재생은 구간 지정을 지원하지 않아 자체 AudioSource 를 쓴다)
    ///
    /// 카드·점수 시스템을 수정하지 않고 EventBus 만 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudienceReactionSfx : MonoBehaviour
    {
        [Header("점수 구간별 반응 (minScore 높은 순으로 평가)")]
        [SerializeField]
        List<AudienceReactionSound> reactions = new List<AudienceReactionSound>();

        [Header("저격 성공")]
        [SerializeField, Tooltip("Special Hit 은 점수와 무관하게 이 소리를 쓴다. 비우면 점수 구간을 따른다")]
        AudienceReactionSound specialHitReaction;

        [SerializeField, Tooltip("Special Hit 전용 소리를 쓸지")]
        bool useSpecialHitReaction = true;

        [Header("야유 (아주 크게 망했을 때만)")]
        [SerializeField, Tooltip("획득 점수가 이 값 이하이면 반응 소리 위에 야유가 겹친다")]
        int booingMaxScore = -25;

        [SerializeField] AudienceReactionSound booing;

        [SerializeField, Min(0f), Tooltip(
            "야유가 다시 나기까지의 최소 간격(초). 연속으로 망해도 도배되지 않게 한다")]
        float booingCooldown = 8f;

        [Header("재생")]
        [SerializeField, Range(0f, 1f), Tooltip("이 컴포넌트 전체에 곱해지는 배율")]
        float masterScale = 1f;

        [SerializeField, Min(1), Tooltip("동시에 울릴 수 있는 반응 소리 수")]
        int voiceCount = 4;

        [SerializeField, Tooltip("Utility 카드처럼 점수가 0인 결과에도 반응할지")]
        bool reactToZeroScore;

        // ---------------- 상태 ----------------

        sealed class Voice
        {
            public AudioSource Source;
            public float StopAt;      // 0이면 끝까지 재생
        }

        readonly List<Voice> _voices = new List<Voice>();
        float _booingReadyAt;

        float ChannelVolume =>
            AudioManager.HasInstance
                ? AudioManager.Instance.SfxVolume * AudioManager.Instance.MasterVolume
                : 1f;

        void Awake() => BuildVoices();

        void BuildVoices()
        {
            if (_voices.Count > 0) return;

            for (int i = 0; i < Mathf.Max(1, voiceCount); i++)
            {
                var go = new GameObject($"ReactionVoice_{i:00}");
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                // 이 보이스 풀은 관객 반응만 낸다 (환호·휘파람·헛기침·귀뚜라미·야유).
                // 종류가 섞이지 않으므로 생성 시 한 번 고정한다
                source.outputAudioMixerGroup = AudioRouting.Resolve(AudioBus.Crowd);
                _voices.Add(new Voice { Source = source });
            }
        }

        void OnEnable() => EventBus.Subscribe<CardResolved>(OnCardResolved);

        void OnDisable()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            StopAll();
        }

        // ---------------- 판정 ----------------

        void OnCardResolved(CardResolved e)
        {
            if (!reactToZeroScore && e.Role == CardRole.Utility && e.GainedScore == 0) return;

            if (e.IsSpecialHit && useSpecialHitReaction && specialHitReaction.clip != null)
            {
                Play(specialHitReaction);
                return;
            }

            if (TryResolveReaction(e.GainedScore, out AudienceReactionSound reaction))
                Play(reaction);

            // 야유는 반응 소리를 대체하지 않고 그 위에 겹친다 (관객이 등 돌리는 느낌)
            if (e.GainedScore > booingMaxScore || booing.clip == null) return;
            if (Time.unscaledTime < _booingReadyAt) return;

            _booingReadyAt = Time.unscaledTime + Mathf.Max(0f, booingCooldown);
            Play(booing);
        }

        /// <summary>점수 → 반응. 가장 높은 minScore 조건이 이긴다. 클립이 없으면 false.</summary>
        bool TryResolveReaction(int score, out AudienceReactionSound reaction)
        {
            reaction = default;

            int bestMin = int.MinValue;
            bool found = false;
            for (int i = 0; i < reactions.Count; i++)
            {
                AudienceReactionSound candidate = reactions[i];
                if (score < candidate.minScore) continue;
                if (found && candidate.minScore <= bestMin) continue;

                bestMin = candidate.minScore;
                reaction = candidate;
                found = true;
            }

            return found && reaction.clip != null;
        }

        // ---------------- 재생 ----------------

        /// <summary>
        /// 클립의 일부만 재생한다. startTime 에서 시작해 duration 이 지나면 멈춘다.
        /// duration 0 이면 클립 끝까지 간다.
        /// </summary>
        public void Play(AudienceReactionSound reaction)
        {
            if (reaction.clip == null) return;

            BuildVoices();
            Voice voice = RentVoice();
            if (voice == null) return;

            AudioSource source = voice.Source;
            source.Stop();
            source.clip = reaction.clip;
            source.volume = Mathf.Clamp01(reaction.volume) * ChannelVolume * Mathf.Clamp01(masterScale);

            float pitchMin = reaction.pitchRange.x > 0.01f ? reaction.pitchRange.x : 1f;
            float pitchMax = reaction.pitchRange.y > 0.01f ? reaction.pitchRange.y : pitchMin;
            source.pitch = UnityEngine.Random.Range(
                Mathf.Min(pitchMin, pitchMax),
                Mathf.Max(pitchMin, pitchMax));

            float start = Mathf.Clamp(reaction.startTime, 0f, Mathf.Max(0f, reaction.clip.length - 0.02f));
            source.time = start;
            source.Play();

            // 피치를 올리면 같은 구간이 더 빨리 지나가므로 정지 시각도 그만큼 당긴다
            voice.StopAt = reaction.duration > 0f
                ? Time.unscaledTime + reaction.duration / Mathf.Max(0.01f, Mathf.Abs(source.pitch))
                : 0f;
        }

        /// <summary>쉬고 있는 보이스를 먼저 쓰고, 전부 울리는 중이면 가장 오래된 것을 뺏는다.</summary>
        Voice RentVoice()
        {
            for (int i = 0; i < _voices.Count; i++)
                if (!_voices[i].Source.isPlaying) return _voices[i];

            Voice oldest = _voices.Count > 0 ? _voices[0] : null;
            for (int i = 1; i < _voices.Count; i++)
                if (_voices[i].Source.time > oldest.Source.time) oldest = _voices[i];
            return oldest;
        }

        void Update()
        {
            // 잘라 쓰는 클립을 제때 멈춘다. 코루틴을 쓰지 않아 중간에 꺼져도 남는 상태가 없다
            for (int i = 0; i < _voices.Count; i++)
            {
                Voice voice = _voices[i];
                if (voice.StopAt <= 0f) continue;
                if (Time.unscaledTime < voice.StopAt) continue;

                voice.Source.Stop();
                voice.StopAt = 0f;
            }
        }

        void StopAll()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                _voices[i].Source.Stop();
                _voices[i].StopAt = 0f;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Play Great (+30)")] void DebugGreat() => DebugScore(30);
        [ContextMenu("Debug/Play Good (+12)")] void DebugGood() => DebugScore(12);
        [ContextMenu("Debug/Play Bad (-10)")] void DebugBad() => DebugScore(-10);
        [ContextMenu("Debug/Play Awful (-20)")] void DebugAwful() => DebugScore(-20);
        [ContextMenu("Debug/Play Booing (-40)")] void DebugBooing() => DebugScore(-40);

        void DebugScore(int score)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[AudienceReactionSfx] Play Mode 에서 실행하세요.", this);
                return;
            }

            if (TryResolveReaction(score, out AudienceReactionSound reaction)) Play(reaction);
            if (score <= booingMaxScore && booing.clip != null) Play(booing);
        }
#endif
    }
}
