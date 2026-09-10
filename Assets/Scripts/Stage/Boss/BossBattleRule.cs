using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 보스전 (boss_battle). 월드 스타디움에서 LUX//FAUNA 와 앙코르 무대를 두고 겨룬다.
    ///
    ///   체력   = 목표 점수 × healthMultiplier(3). 점수가 그대로 피해 (체력 = 최대 + 회복 − 점수)
    ///   패턴   = B2B · GUEST LIST · BEATMATCH · KILL SWITCH (+ PEAK TIME 에서 DROP)
    ///   성공   = 최대 체력 / patternsToClear 만큼 점수 가산 + 상대 팬 합류 / 실패 = 체력 5% 회복 + 우리 팬 이탈
    ///   PEAK   = 체력 50% 이하: 강화 패턴, 간격 단축, 진입 즉시 DROP
    ///   클리어 = 체력 0 → 즉시 종료 (남은 초 × 보너스 점수). 시간 초과 = 실패
    ///
    /// 점수는 GameManager 가, 관객은 AudienceRosterSystem 이 담당하고 이 룰은 판정과 이동만 한다.
    /// 클리어 판정은 StageRuntimeDirector.ClearVerdictOverride 로 TourPerformanceBridge 에 넘긴다.
    ///
    /// 연출: 같은 오브젝트에 BossStagePresentation 이 있으면 패턴 예고·격파 때 라이벌 무대 왕복 연출을 먼저 돌리고,
    /// 돌아온 뒤에 패턴 창을 연다(LeadIn). 없으면 예전처럼 즉시 시작한다.
    /// </summary>
    public sealed class BossBattleRule : StageRuleBehaviour
    {
        [SerializeField] BossBattleConfig config;
        [SerializeField, Tooltip("비우면 같은 오브젝트에서 찾는다. 없으면 연출 없이 진행")] BossStagePresentation presentation;

        [Header("통계 (읽기 전용)")]
        [SerializeField] int patternsResolved;
        [SerializeField] int patternsSucceeded;
        [SerializeField] int fansRecruited;
        [SerializeField] int fansLost;

        readonly List<BossPattern> _basePool = new List<BossPattern>();
        readonly List<BossPattern> _peakPool = new List<BossPattern>();
        readonly List<AudienceId> _idBuffer = new List<AudienceId>(4);

        BossPattern _active;
        BossPattern _pending;   // 예고 연출 중인 패턴 (LeadIn)
        BossPattern _lastPattern;
        DropPattern _drop;
        System.Random _random;

        float _damage;      // 디버그 피해 누적 (F7)
        float _healed;      // 실패 회복 누적
        float _lastHealth = -1f;
        float _remaining;
        float _nextPatternAt;
        int _rivalFans;
        bool _peakTime;
        bool _defeated;
        bool _forceDropNext;

        public BossBattleConfig Config => config;
        public bool IsPatternActive => _active != null;

        /// <summary>패턴 예고 연출 중 (창은 아직 안 열림).</summary>
        public bool IsLeadIn => _pending != null;
        public bool IsPeakTime => _peakTime;
        public bool IsDefeated => _defeated;

        /// <summary>지금까지 성공한 패턴 수 (예전 연속 성공 대신).</summary>
        public int Streak => patternsSucceeded;
        public int RivalFansWaiting => _rivalFans;
        /// <summary>최대 체력 = 목표 점수 × 배율. 카드 점수만으로는 다 깎기 힘들고 패턴 성공(체력/8)이 필요하다.</summary>
        public float MaxHealth => Mathf.Max(1f, PerformanceTimer.TargetScore * (config != null ? config.HealthMultiplier : 1f));
        public float CurrentHealth => Mathf.Clamp(MaxHealth + _healed - _damage - CurrentScore, 0f, MaxHealth + _healed);
        public float HealthNormalized => Mathf.Clamp01(CurrentHealth / MaxHealth);
        public int PatternsResolved => patternsResolved;
        public int PatternsSucceeded => patternsSucceeded;
        public int FansRecruited => fansRecruited;
        public int FansLost => fansLost;

        static int CurrentScore => GameManager.HasInstance ? GameManager.Instance.Score : 0;

        // ---------------- 룰 수명 ----------------

        protected override void OnActivate(StageRuleContext context)
        {
            if (config == null)
            {
                Debug.LogError("[Boss] BossBattleConfig 가 비어 있어 보스 룰을 켤 수 없습니다.", this);
                return;
            }

            _random = new System.Random(unchecked(context.RunSeed ^ 0x0B055));
            _damage = 0f;
            _healed = 0f;
            _lastHealth = -1f;
            _rivalFans = config.RivalFanPool;
            _peakTime = false;
            _defeated = false;
            _forceDropNext = false;
            _active = null;
            _pending = null;
            _lastPattern = null;
            _nextPatternAt = config.FirstPatternDelay;
            patternsResolved = 0;
            patternsSucceeded = 0;
            fansRecruited = 0;
            fansLost = 0;

            BuildPools();
            if (StageRuntimeDirector.Active != null) StageRuntimeDirector.Active.ClearVerdictOverride = false;

            if (presentation == null) presentation = GetComponent<BossStagePresentation>();
            if (presentation != null) presentation.Begin(config);

            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
        }

        protected override void OnDeactivate()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);

            _pending = null;
            if (_active != null)
            {
                _active.End(false);
                _active = null;
            }
            if (presentation != null) presentation.End();
        }

        void BuildPools()
        {
            _basePool.Clear();
            _peakPool.Clear();
            _basePool.Add(new B2BPattern());
            _basePool.Add(new GuestListPattern());
            _basePool.Add(new BeatmatchPattern());
            _basePool.Add(new KillSwitchPattern());
            _peakPool.Add(new B2BPattern());
            _peakPool.Add(new GuestListPattern());
            _peakPool.Add(new BeatmatchPattern());
            _peakPool.Add(new KillSwitchPattern());
            _drop = new DropPattern();
            _peakPool.Add(_drop);
        }

        // ---------------- 매 프레임 ----------------

        void Update()
        {
            if (!IsActive || config == null || _defeated) return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;

            PublishHealth();
            if (CurrentHealth <= 0f)
            {
                Defeat(byStreak: false);
                return;
            }

            if (!_peakTime && HealthNormalized <= config.PeakTimeRatio) EnterPeakTime();

            // 예고 연출 중: 창도 스케줄도 멈춘다 (연출이 끝나면 BeginPendingPattern)
            if (_pending != null) return;

            if (_active != null)
            {
                _active.Tick(Time.deltaTime);
                _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);
                EventBus.Raise(new BossPatternProgress(
                    _active.Id, _remaining, _active.Window, _active.ProgressText, _active.Achieved));

                if (_active.Achieved) Resolve(true);
                else if (_active.Broken || _remaining <= 0f) Resolve(false);
                return;
            }

            if (PerformanceTimer.Elapsed >= _nextPatternAt) StartNextPattern();
        }

        void PublishHealth()
        {
            float health = CurrentHealth;
            if (Mathf.Approximately(health, _lastHealth)) return;
            float delta = _lastHealth < 0f ? 0f : health - _lastHealth;
            _lastHealth = health;
            EventBus.Raise(new BossHealthChanged(health, MaxHealth, delta, _peakTime));
        }

        void EnterPeakTime()
        {
            _peakTime = true;
            _forceDropNext = true;
            EventBus.Raise(new BossPeakTimeEntered());
            EventBus.Raise(new BossHealthChanged(CurrentHealth, MaxHealth, 0f, true));
        }

        // ---------------- 패턴 ----------------

        void StartNextPattern()
        {
            BossPattern pattern = PickPattern();
            if (pattern == null)
            {
                _nextPatternAt = PerformanceTimer.Elapsed + config.RetryDelay;
                return;
            }

            // 창이 공연 끝을 넘기면 시작하지 않는다
            float window = pattern.Window;
            if (PerformanceTimer.Duration > 0f && PerformanceTimer.Elapsed + window > PerformanceTimer.Duration)
            {
                _nextPatternAt = float.PositiveInfinity;
                return;
            }

            _remaining = window;

            // 연출이 있으면 라이벌 무대를 갔다 온 뒤 창을 연다. 그동안 카드 입력은 잠기고 타이머는 멈춘다
            if (presentation != null && presentation.IsReady)
            {
                _pending = pattern;
                Debug.Log($"[Boss] 패턴 예고: {pattern.Title}{(pattern.Enhanced ? " (강화)" : "")}", this);
                EventBus.Raise(new BossPatternAnnounced(
                    pattern.Id, pattern.Title, pattern.Instruction, pattern.Enhanced, config.AnnounceDisplaySeconds));
                presentation.PlayPatternAnnounce(BeginPendingPattern);
                return;
            }

            BeginPattern(pattern);
        }

        /// <summary>예고 연출이 끝난 뒤 호출. 그 사이 룰이 꺼졌거나 격파됐으면 버린다.</summary>
        void BeginPendingPattern()
        {
            BossPattern pattern = _pending;
            _pending = null;
            if (pattern == null || !IsActive || _defeated) return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;
            BeginPattern(pattern);
        }

        void BeginPattern(BossPattern pattern)
        {
            _active = pattern;
            _active.Begin();
            float window = _remaining;
            Debug.Log($"[Boss] 패턴 시작: {pattern.Title}{(pattern.Enhanced ? " (강화)" : "")} · {window:0.0}s · {pattern.Instruction}", this);
            EventBus.Raise(new BossPatternStarted(
                pattern.Id, pattern.Title, pattern.Instruction, window, pattern.Enhanced));
        }

        BossPattern PickPattern()
        {
            List<BossPattern> pool = _peakTime ? _peakPool : _basePool;

            if (_peakTime && _forceDropNext)
            {
                _forceDropNext = false;
                _drop.Setup(config, Context, _random, true);
                if (_drop.CanStart()) return _drop;
            }

            var candidates = new List<BossPattern>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                BossPattern candidate = pool[i];
                if (_lastPattern != null && candidate.Id == _lastPattern.Id) continue;
                candidate.Setup(config, Context, _random, _peakTime);
                if (candidate.CanStart()) candidates.Add(candidate);
            }
            if (candidates.Count == 0) return null;
            return candidates[_random.Next(candidates.Count)];
        }

        void Resolve(bool success)
        {
            BossPattern pattern = _active;
            _active = null;
            _lastPattern = pattern;
            pattern.End(success);
            patternsResolved++;

            float healthDelta;
            int moved;
            if (success)
            {
                patternsSucceeded++;
                // 피해 = 점수 가산. 체력은 점수로만 깎이므로 카드 점수와 같은 경로를 탄다
                int gain = Mathf.RoundToInt(MaxHealth * config.SuccessDamageRatio);
                if (gain > 0 && GameManager.HasInstance) GameManager.Instance.AddScore(gain);
                healthDelta = -gain;
                moved = RecruitRivalFans(pattern);
            }
            else
            {
                float heal = MaxHealth * config.FailHealRatio;
                _healed += heal;
                healthDelta = heal;
                moved = LoseOurFans();
            }

            Debug.Log($"[Boss] 패턴 {(success ? "성공" : "실패")}: {pattern.Title} · 체력 {HealthNormalized:P0} · 성공 {patternsSucceeded}/{config.PatternsToClear} · 팬 이동 {moved}", this);
            EventBus.Raise(new BossPatternResolved(pattern.Id, pattern.Title, success, patternsSucceeded, healthDelta, moved));
            PublishHealth();

            if (CurrentHealth <= 0f)
            {
                Defeat(byStreak: false);
                return;
            }

            _nextPatternAt = PerformanceTimer.Elapsed + (_peakTime ? config.PeakPatternInterval : config.PatternInterval);
        }

        // ---------------- 관객 이동 ----------------

        int RecruitRivalFans(BossPattern pattern)
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null) return 0;

            int want = Mathf.Min(pattern.Enhanced ? config.RecruitOnSuccessEnhanced : config.RecruitOnSuccess, _rivalFans);
            int joined = 0;
            for (int i = 0; i < want; i++)
            {
                if (roster.Count >= roster.Capacity) break; // 만석이면 점수만
                CrowdPreference preference = pattern.RecruitPreference ?? LeastRepresentedPreference(roster);
                if (!roster.TryAdd(preference, config.RecruitEngagement, AudienceJoinReason.RuntimeCommand, out _)) break;
                joined++;
            }

            _rivalFans -= joined;
            fansRecruited += joined;
            return joined;
        }

        int LoseOurFans()
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null) return 0;

            int removable = Mathf.Max(0, roster.Count - config.MinimumSurvivors);
            int count = Mathf.Min(config.LoseFanOnFail, removable);
            int lost = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryFindLowestEngagement(roster, out AudienceId id)) break;
                if (roster.TryRemove(id, AudienceDepartureReason.NearbyConcert, out _)) lost++;
            }

            _rivalFans += lost;
            fansLost += lost;
            return lost;
        }

        static bool TryFindLowestEngagement(AudienceRosterSystem roster, out AudienceId id)
        {
            id = default;
            float lowest = float.MaxValue;
            IReadOnlyList<AudienceSnapshot> members = roster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Engagement >= lowest) continue;
                lowest = members[i].Engagement;
                id = members[i].Id;
            }
            return lowest < float.MaxValue;
        }

        static CrowdPreference LeastRepresentedPreference(AudienceRosterSystem roster)
        {
            int chill = 0, singalong = 0, mosh = 0;
            IReadOnlyList<AudienceSnapshot> members = roster.Members;
            for (int i = 0; i < members.Count; i++)
            {
                switch (members[i].Preference)
                {
                    case CrowdPreference.Chill: chill++; break;
                    case CrowdPreference.Singalong: singalong++; break;
                    case CrowdPreference.Mosh: mosh++; break;
                }
            }
            if (chill <= singalong && chill <= mosh) return CrowdPreference.Chill;
            return singalong <= mosh ? CrowdPreference.Singalong : CrowdPreference.Mosh;
        }

        // ---------------- 격파 ----------------

        void Defeat(bool byStreak)
        {
            if (_defeated) return;
            _defeated = true;

            _pending = null;
            if (_active != null)
            {
                _active.End(true);
                _active = null;
            }

            float remaining = Mathf.Max(0f, PerformanceTimer.Duration - PerformanceTimer.Elapsed);
            int bonus = Mathf.RoundToInt(remaining * config.EarlyClearBonusPerSecond);
            if (bonus > 0 && GameManager.HasInstance) GameManager.Instance.AddScore(bonus);

            if (StageRuntimeDirector.Active != null) StageRuntimeDirector.Active.ClearVerdictOverride = true;
            Debug.Log($"[Boss] 격파! 체력 0 (패턴 성공 {patternsSucceeded}) · 남은 {remaining:0.0}s · 보너스 {bonus:N0}", this);
            EventBus.Raise(new BossHealthChanged(0f, MaxHealth, -_lastHealth, _peakTime));
            EventBus.Raise(new BossDefeated(byStreak, remaining, bonus));

            // 격파 연출(라이벌 무대 소등)을 보여준 뒤 공연을 끝낸다
            if (presentation != null && presentation.IsReady) presentation.PlayDefeat(EndPerformance);
            else EndPerformance();
        }

        static void EndPerformance()
        {
            if (GameManager.HasInstance && GameManager.Instance.IsPlaying) GameManager.Instance.GameOver();
        }

        // ---------------- 이벤트 전달 ----------------

        void OnCardResolved(CardResolved e) => _active?.OnCardResolved(e);
        void OnComboChanged(ComboChanged e) => _active?.OnComboChanged(e);
        void OnFeverChanged(FeverStateChanged e) => _active?.OnFeverChanged(e);
        void OnSpecialHit(SpecialHitLanded e) => _active?.OnSpecialHit(e);

        // ---------------- 디버그 ----------------

        /// <summary>[디버그] 최대 체력 대비 비율만큼 피해.</summary>
        public void DebugDamage(float ratio)
        {
            if (!IsActive) return;
            _damage += MaxHealth * Mathf.Max(0f, ratio);
        }

        /// <summary>[디버그] 다음 패턴을 지금 시작.</summary>
        public void DebugStartPatternNow()
        {
            if (!IsActive || _active != null || _pending != null) return;
            _nextPatternAt = 0f;
        }

        /// <summary>[디버그] 라이벌 무대 왕복 연출만 미리보기.</summary>
        public void DebugPreviewCinematic()
        {
            if (!IsActive || presentation == null) return;
            presentation.PlayPreview();
        }

        /// <summary>[디버그] 현재 패턴을 성공 처리.</summary>
        public void DebugSucceedPattern()
        {
            if (!IsActive || _active == null) return;
            Resolve(true);
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(BossBattleConfig battleConfig) => config = battleConfig;
#endif
    }
}
