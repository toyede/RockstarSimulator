using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 보스전 「관객 쟁탈전」 (boss_battle). Docs/BOSS_STAGE_REDESIGN_KO.md v2.
    ///
    ///   팬     = 우리 관객(로스터) + 라이벌 팬(_rivalFans). 총원은 변하지 않고 양쪽을 오간다
    ///   드레인 = drainInterval 초마다 점수 −(라이벌 팬 × drainPerFan). 라이벌 팬 0명이면 멈춘다
    ///   패턴   = B2B · GUEST LIST · BEATMATCH · KILL SWITCH (+ REVENGE 에서 DROP)
    ///   성공   = 라이벌 팬 2명(강화 3, DROP 3) 합류 + 목표 × 6%(DROP 10%) 보너스 / 실패 = 우리 팬 1명이 라이벌로
    ///   REVENGE = 라이벌 팬 ≤ revengeThreshold: 강화 패턴, 간격 단축, 진입 즉시 DROP. ≥ releaseThreshold 면 해제
    ///   승패   = 시간 종료 시 점수 ≥ 목표 (PerformanceTimer 판정 그대로). 조기 종료 없음
    ///
    /// 점수는 GameManager, 관객은 AudienceRosterSystem 이 담당하고 이 룰은 판정·이동·드레인만 한다.
    /// 드레인과 패턴 스케줄은 PerformanceTimer.Elapsed 기준이라 예고 연출(타이머 정지) 중에는 멈추고, 엿보기 중에는 계속 흐른다.
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
        [SerializeField] int drainTotal;

        readonly List<BossPattern> _basePool = new List<BossPattern>();
        readonly List<BossPattern> _revengePool = new List<BossPattern>();

        BossPattern _active;
        BossPattern _pending;   // 예고 연출 중인 패턴 (LeadIn)
        BossPattern _lastPattern;
        DropPattern _drop;
        System.Random _random;

        float _remaining;
        float _nextPatternAt;
        float _nextDrainAt;
        int _rivalFans;
        bool _revenge;
        bool _forceDropNext;

        public BossBattleConfig Config => config;
        public bool IsPatternActive => _active != null;
        public bool IsLeadIn => _pending != null;
        public bool IsRevenge => _revenge;
        public int RivalFans => _rivalFans;
        public int OurFans => Context.AudienceRoster != null ? Context.AudienceRoster.Count : 0;
        public int TotalFans => OurFans + _rivalFans;
        public int PatternsResolved => patternsResolved;
        public int PatternsSucceeded => patternsSucceeded;
        public int FansRecruited => fansRecruited;
        public int FansLost => fansLost;
        public int DrainTotal => drainTotal;
        public float NextDrainIn => Mathf.Max(0f, _nextDrainAt - PerformanceTimer.Elapsed);
        public int DrainAmount => _rivalFans * (config != null ? config.DrainPerFan : 0);

        /// <summary>예전 이름 호환: 성공한 패턴 수.</summary>
        public int Streak => patternsSucceeded;

        // ---------------- 룰 수명 ----------------

        protected override void OnActivate(StageRuleContext context)
        {
            if (config == null)
            {
                Debug.LogError("[Boss] BossBattleConfig 가 비어 있어 보스 룰을 켤 수 없습니다.", this);
                return;
            }

            _random = new System.Random(unchecked(context.RunSeed ^ 0x0B055));
            _rivalFans = config.RivalFanPool;
            _revenge = false;
            _forceDropNext = false;
            _active = null;
            _pending = null;
            _lastPattern = null;
            _remaining = 0f;
            _nextPatternAt = config.FirstPatternDelay;
            _nextDrainAt = config.DrainStartDelay;
            patternsResolved = 0;
            patternsSucceeded = 0;
            fansRecruited = 0;
            fansLost = 0;
            drainTotal = 0;

            BuildPools();

            if (presentation == null) presentation = GetComponent<BossStagePresentation>();
            if (presentation != null) presentation.Begin(config, () => !IsPatternActive && !IsLeadIn);

            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Subscribe<AudienceDeparted>(OnAudienceDeparted);

            PublishBalance();
        }

        protected override void OnDeactivate()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Unsubscribe<AudienceDeparted>(OnAudienceDeparted);

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
            _revengePool.Clear();
            _basePool.Add(new B2BPattern());
            _basePool.Add(new GuestListPattern());
            _basePool.Add(new BeatmatchPattern());
            _basePool.Add(new KillSwitchPattern());
            _revengePool.Add(new B2BPattern());
            _revengePool.Add(new GuestListPattern());
            _revengePool.Add(new BeatmatchPattern());
            _revengePool.Add(new KillSwitchPattern());
            _drop = new DropPattern();
            _revengePool.Add(_drop);
        }

        // ---------------- 매 프레임 ----------------

        void Update()
        {
            if (!IsActive || config == null) return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;

            float elapsed = PerformanceTimer.Elapsed;

            // 드레인: 라이벌 팬이 있는 동안 주기적으로 점수 감소 (타이머 기준이라 예고 연출 중에는 멈춘다)
            if (_rivalFans > 0 && elapsed >= _nextDrainAt) ApplyDrain();
            EventBus.Raise(new BossDrainCountdown(NextDrainIn, DrainAmount, _rivalFans > 0));

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

            if (elapsed >= _nextPatternAt) StartNextPattern();
        }

        // ---------------- 드레인 ----------------

        void ApplyDrain()
        {
            int amount = DrainAmount;
            int score = GameManager.HasInstance ? GameManager.Instance.Score : 0;
            int applied = Mathf.Clamp(amount, 0, score);
            if (applied > 0) GameManager.Instance.SetScore(score - applied);
            drainTotal += applied;

            _nextDrainAt += config.DrainInterval;
            if (_nextDrainAt < PerformanceTimer.Elapsed) _nextDrainAt = PerformanceTimer.Elapsed + config.DrainInterval;

            EventBus.Raise(new BossDrainApplied(applied, _rivalFans, config.DrainInterval));
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

            if (presentation != null && presentation.IsReady)
            {
                _pending = pattern;
                Debug.Log($"[Boss] 패턴 예고: {pattern.Title}{(pattern.Enhanced ? " (REVENGE)" : "")}", this);
                EventBus.Raise(new BossPatternAnnounced(
                    pattern.Id, pattern.Title, pattern.Instruction, pattern.Enhanced, config.AnnounceDisplaySeconds));
                presentation.PlayPatternAnnounce(BeginPendingPattern);
                return;
            }

            BeginPattern(pattern);
        }

        void BeginPendingPattern()
        {
            BossPattern pattern = _pending;
            _pending = null;
            if (pattern == null || !IsActive) return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;
            BeginPattern(pattern);
        }

        void BeginPattern(BossPattern pattern)
        {
            _active = pattern;
            _active.Begin();
            float window = _remaining;
            Debug.Log($"[Boss] 패턴 시작: {pattern.Title}{(pattern.Enhanced ? " (REVENGE)" : "")} · {window:0.0}s · {pattern.Instruction}", this);
            EventBus.Raise(new BossPatternStarted(pattern.Id, pattern.Title, pattern.Instruction, window, pattern.Enhanced));
        }

        BossPattern PickPattern()
        {
            List<BossPattern> pool = _revenge ? _revengePool : _basePool;

            if (_revenge && _forceDropNext)
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
                candidate.Setup(config, Context, _random, _revenge);
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

            int moved;
            int bonus = 0;
            if (success)
            {
                patternsSucceeded++;
                bool drop = pattern is DropPattern;
                int want = drop ? config.StealOnDrop : (pattern.Enhanced ? config.StealOnSuccessEnhanced : config.StealOnSuccess);
                moved = StealFans(want, pattern.RecruitPreference);
                bonus = Mathf.RoundToInt(PerformanceTimer.TargetScore * (drop ? config.DropBonusRatio : config.PatternBonusRatio));
                if (bonus > 0 && GameManager.HasInstance) GameManager.Instance.AddScore(bonus);
            }
            else
            {
                moved = LoseOurFans(config.LoseFanOnFail);
            }

            Debug.Log($"[Boss] 패턴 {(success ? "성공" : "실패")}: {pattern.Title} · 라이벌 팬 {_rivalFans} · 우리 {OurFans} · 보너스 {bonus:N0} · 팬 이동 {moved}", this);
            EventBus.Raise(new BossPatternResolved(pattern.Id, pattern.Title, success, patternsSucceeded, bonus, moved));

            _nextPatternAt = PerformanceTimer.Elapsed + (_revenge ? config.RevengePatternInterval : config.PatternInterval);
        }

        // ---------------- 팬 이동 ----------------

        /// <summary>라이벌 팬을 우리 쪽으로. 라이벌 팬이 없거나 만석이면 그만큼 못 온다. 옮긴 수를 돌려준다.</summary>
        public int StealFans(int count, CrowdPreference? preference = null)
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null || !IsActive) return 0;

            int joined = 0;
            for (int i = 0; i < count; i++)
            {
                if (_rivalFans <= 0) break;
                if (roster.Count >= roster.Capacity) break;
                CrowdPreference pick = preference ?? LeastRepresentedPreference(roster);
                if (!roster.TryAdd(pick, config.RecruitEngagement, AudienceJoinReason.RuntimeCommand, out _)) break;
                joined++;
                _rivalFans--;
            }

            if (joined > 0)
            {
                fansRecruited += joined;
                EventBus.Raise(new BossFanMoved(false, joined, _rivalFans, OurFans));
                AfterFanChange();
            }
            return joined;
        }

        int LoseOurFans(int count)
        {
            AudienceRosterSystem roster = Context.AudienceRoster;
            if (roster == null) return 0;

            int removable = Mathf.Max(0, roster.Count - config.MinimumSurvivors);
            int take = Mathf.Min(count, removable);
            int lost = 0;
            for (int i = 0; i < take; i++)
            {
                if (!TryFindLowestEngagement(roster, out AudienceId id)) break;
                if (roster.TryRemove(id, AudienceDepartureReason.NearbyConcert, out _)) lost++;
            }

            if (lost > 0)
            {
                _rivalFans += lost;
                fansLost += lost;
                EventBus.Raise(new BossFanMoved(true, lost, _rivalFans, OurFans));
                AfterFanChange();
            }
            return lost;
        }

        /// <summary>몰입도가 바닥나 자연 이탈한 관객은 라이벌 무대로 건너간 것으로 친다 (총원 유지).</summary>
        void OnAudienceDeparted(AudienceDeparted e)
        {
            if (!IsActive || e.Reason != AudienceDepartureReason.EngagementDepleted) return;
            _rivalFans++;
            fansLost++;
            EventBus.Raise(new BossFanMoved(true, 1, _rivalFans, OurFans));
            AfterFanChange();
        }

        void AfterFanChange()
        {
            PublishBalance();
            if (!_revenge && _rivalFans <= config.RevengeThreshold)
            {
                _revenge = true;
                _forceDropNext = true;
                Debug.Log($"[Boss] REVENGE TIME! 라이벌 팬 {_rivalFans}", this);
                EventBus.Raise(new BossRevengeChanged(true));
                PublishBalance();
            }
            else if (_revenge && _rivalFans >= config.RevengeReleaseThreshold)
            {
                _revenge = false;
                Debug.Log($"[Boss] REVENGE 해제. 라이벌 팬 {_rivalFans}", this);
                EventBus.Raise(new BossRevengeChanged(false));
                PublishBalance();
            }
        }

        void PublishBalance() => EventBus.Raise(new BossFanBalanceChanged(OurFans, _rivalFans, TotalFans, _revenge));

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

        // ---------------- 이벤트 전달 ----------------

        void OnCardResolved(CardResolved e) => _active?.OnCardResolved(e);
        void OnComboChanged(ComboChanged e) => _active?.OnComboChanged(e);
        void OnFeverChanged(FeverStateChanged e) => _active?.OnFeverChanged(e);
        void OnSpecialHit(SpecialHitLanded e) => _active?.OnSpecialHit(e);

        // ---------------- 디버그 ----------------

        /// <summary>[디버그] 라이벌 팬을 지금 뺏어온다.</summary>
        public void DebugStealFans(int count) => StealFans(count);

        /// <summary>[디버그] 드레인을 지금 적용.</summary>
        public void DebugDrainNow()
        {
            if (!IsActive || _rivalFans <= 0) return;
            ApplyDrain();
        }

        /// <summary>[디버그] 다음 패턴을 지금 시작.</summary>
        public void DebugStartPatternNow()
        {
            if (!IsActive || _active != null || _pending != null) return;
            _nextPatternAt = 0f;
        }

        /// <summary>[디버그] 현재 패턴을 성공 처리.</summary>
        public void DebugSucceedPattern()
        {
            if (!IsActive || _active == null) return;
            Resolve(true);
        }

        /// <summary>[디버그] 라이벌 무대 왕복 연출만 미리보기.</summary>
        public void DebugPreviewCinematic()
        {
            if (!IsActive || presentation == null) return;
            presentation.PlayPreview();
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(BossBattleConfig battleConfig) => config = battleConfig;
#endif
    }
}
