using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 보스 패턴 하나. 룰이 창(window) 동안 Tick 하고, Achieved 가 되면 즉시 성공, 창이 끝나면 실패로 처리한다.
    /// 점수·관객·UI 는 직접 건드리지 않는다 — 판정만 하고 결과는 룰이 적용한다.
    /// </summary>
    public abstract class BossPattern
    {
        protected BossBattleConfig Config { get; private set; }
        protected StageRuleContext Context { get; private set; }
        protected System.Random Random { get; private set; }

        public bool Enhanced { get; private set; }
        public bool Achieved { get; protected set; }

        /// <summary>창이 끝나기 전에 실패가 확정됐는가 (예: BEATMATCH 에서 콤보를 잃음).</summary>
        public bool Broken { get; protected set; }

        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Instruction { get; }
        public abstract string ProgressText { get; }
        public virtual float Window => Config.PatternWindow;

        /// <summary>성공 시 합류할 팬 성향. null 이면 룰이 가장 적은 성향을 고른다.</summary>
        public virtual CrowdPreference? RecruitPreference => null;

        public void Setup(BossBattleConfig config, StageRuleContext context, System.Random random, bool enhanced)
        {
            Config = config;
            Context = context;
            Random = random;
            Enhanced = enhanced;
            Achieved = false;
            Broken = false;
        }

        /// <summary>지금 시작할 수 있는가 (손패·시스템 상태). false 면 룰이 다른 패턴을 고른다.</summary>
        public virtual bool CanStart() => true;

        public virtual void Begin() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void End(bool success) { }

        public virtual void OnCardResolved(CardResolved e) { }
        public virtual void OnComboChanged(ComboChanged e) { }
        public virtual void OnFeverChanged(FeverStateChanged e) { }
        public virtual void OnSpecialHit(SpecialHitLanded e) { }

        protected static bool IsPerformanceCard(CardRole role) =>
            role == CardRole.Normal || role == CardRole.Special;

        protected static string PreferenceLabel(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return "CHILL";
                case CrowdPreference.Singalong: return "SINGALONG";
                case CrowdPreference.Mosh: return "MOSH";
                default: return preference.ToString().ToUpperInvariant();
            }
        }
    }

    /// <summary>B2B — 라이벌이 트랙을 걸면 지목한 카드로 받아친다. 덱에 있는 Normal·Special 카드만 지목한다.</summary>
    public sealed class B2BPattern : BossPattern
    {
        readonly List<CardDefinition> _targets = new List<CardDefinition>(2);
        readonly HashSet<string> _played = new HashSet<string>(StringComparer.Ordinal);

        public override string Id => "b2b";
        public override string Title => "B2B";
        public override string Instruction
        {
            get
            {
                if (_targets.Count == 0) return "라이벌의 트랙에 답하라!";
                var names = new List<string>(_targets.Count);
                for (int i = 0; i < _targets.Count; i++) names.Add(_targets[i].DisplayName);
                return $"라이벌의 트랙에 답하라 — [{string.Join("] · [", names)}] 사용!";
            }
        }
        public override string ProgressText => $"{_played.Count} / {_targets.Count}";

        public override bool CanStart()
        {
            return CardSystem.HasInstance && CollectCandidates().Count > 0;
        }

        public override void Begin()
        {
            _targets.Clear();
            _played.Clear();

            int count = Enhanced ? Config.B2BCardsEnhanced : Config.B2BCards;
            List<CardDefinition> candidates = CollectCandidates();

            // 손패에 있는 카드를 먼저 지목한다 (준비된 플레이어를 억지로 막지 않는다). 부족하면 덱에서 뽑는다.
            List<CardDefinition> hand = FilterPerformance(CardSystem.Instance.Hand);
            while (_targets.Count < count && hand.Count > 0)
            {
                CardDefinition pick = hand[Random.Next(hand.Count)];
                hand.RemoveAll(card => card.Id == pick.Id);
                candidates.RemoveAll(card => card.Id == pick.Id);
                _targets.Add(pick);
            }
            while (_targets.Count < count && candidates.Count > 0)
            {
                CardDefinition pick = candidates[Random.Next(candidates.Count)];
                candidates.RemoveAll(card => card.Id == pick.Id);
                _targets.Add(pick);
            }
        }

        public override void OnCardResolved(CardResolved e)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (!string.Equals(_targets[i].Id, e.CardId, StringComparison.Ordinal)) continue;
                _played.Add(e.CardId);
                break;
            }
            if (_targets.Count > 0 && _played.Count >= _targets.Count) Achieved = true;
        }

        /// <summary>덱(카드 풀)의 Normal·Special. 유틸리티(드로우·리롤)는 제외.</summary>
        static List<CardDefinition> CollectCandidates()
        {
            var result = new List<CardDefinition>();
            if (!CardSystem.HasInstance || CardSystem.Instance.Config == null) return result;

            IReadOnlyList<CardPoolEntry> pool = CardSystem.Instance.Config.CardPool;
            for (int i = 0; i < pool.Count; i++)
            {
                CardPoolEntry entry = pool[i];
                if (entry == null || !entry.IsUsable) continue;
                CardDefinition card = entry.Prefab;
                if (!IsPerformanceCard(card.Role)) continue;
                if (result.Exists(existing => existing.Id == card.Id)) continue;
                result.Add(card);
            }
            return result;
        }

        static List<CardDefinition> FilterPerformance(IReadOnlyList<CardDefinition> cards)
        {
            var result = new List<CardDefinition>();
            for (int i = 0; i < cards.Count; i++)
            {
                CardDefinition card = cards[i];
                if (card == null || !IsPerformanceCard(card.Role)) continue;
                if (result.Exists(existing => existing.Id == card.Id)) continue;
                result.Add(card);
            }
            return result;
        }
    }

    /// <summary>GUEST LIST — 상대 팬이 특별 관객으로 기웃거린다. 요청 Special 카드를 드롭하면 합류.</summary>
    public sealed class GuestListPattern : BossPattern
    {
        HeatStage _request;
        bool _spawned;

        public override string Id => "guest_list";
        public override string Title => "GUEST LIST";
        public override string Instruction =>
            $"상대 팬이 {RequestLabel(_request)} 을(를) 요청한다 — Special 카드를 직접 드롭!";
        public override string ProgressText => Achieved ? "합류!" : (_spawned ? "요청 대기 중" : "등장 중");
        public override float Window
        {
            get
            {
                float request = SpecialAudienceManager.HasInstance
                    ? SpecialAudienceManager.Instance.RequestDuration
                    : Config.PatternWindow;
                if (Enhanced) request = Mathf.Min(request, Config.GuestListEnhancedWindow);
                return Mathf.Max(1f, request) + 1f;
            }
        }
        public override CrowdPreference? RecruitPreference => ToPreference(_request);

        public override bool CanStart()
        {
            if (!SpecialAudienceManager.HasInstance || SpecialAudienceManager.Instance.HasActiveRequest) return false;
            return TryPickRequestFromHand(out _);
        }

        public override void Begin()
        {
            _spawned = false;
            if (!TryPickRequestFromHand(out _request)) return;
            SpecialAudienceManager.Instance.ForceSpawn(_request);
            _spawned = true;
        }

        public override void OnSpecialHit(SpecialHitLanded e)
        {
            if (_spawned && e.RequestType == _request) Achieved = true;
        }

        /// <summary>손패의 Special 카드 중 하나의 요구 타입을 고른다. 없으면 시작하지 않는다.</summary>
        bool TryPickRequestFromHand(out HeatStage request)
        {
            request = HeatStage.Chill;
            if (!CardSystem.HasInstance) return false;

            var options = new List<HeatStage>(3);
            IReadOnlyList<CardDefinition> hand = CardSystem.Instance.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                CardDefinition card = hand[i];
                if (card == null || card.Role != CardRole.Special) continue;
                if (!options.Contains(card.TargetStage)) options.Add(card.TargetStage);
            }

            if (options.Count == 0) return false;
            request = options[Random.Next(options.Count)];
            return true;
        }

        static CrowdPreference ToPreference(HeatStage stage)
        {
            switch (stage)
            {
                case HeatStage.Singalong: return CrowdPreference.Singalong;
                case HeatStage.Mosh: return CrowdPreference.Mosh;
                default: return CrowdPreference.Chill;
            }
        }

        static string RequestLabel(HeatStage stage) => stage.ToString().ToUpperInvariant();
    }

    /// <summary>BEATMATCH — 창 동안 콤보를 잃지 않고 공연 카드를 N장 이상 낸다.</summary>
    public sealed class BeatmatchPattern : BossPattern
    {
        int _played;
        int Required => Enhanced ? Config.BeatmatchCardsEnhanced : Config.BeatmatchCards;

        public override string Id => "beatmatch";
        public override string Title => "BEATMATCH";
        public override string Instruction => $"박자를 놓치지 마 — 콤보를 지키며 카드 {Required}장!";
        public override string ProgressText => Broken ? "콤보 끊김" : $"{_played} / {Required}";

        public override void Begin() => _played = 0;

        public override void OnCardResolved(CardResolved e)
        {
            if (Broken || !IsPerformanceCard(e.Role)) return;
            _played++;
            if (_played >= Required) Achieved = true;
        }

        public override void OnComboChanged(ComboChanged e)
        {
            if (e.WasLost && !Achieved) Broken = true;
        }
    }

    /// <summary>KILL SWITCH — 성향 하나(강화: 둘)의 카드를 봉인. 남은 카드로 양수 반응을 N번 만든다.</summary>
    public sealed class KillSwitchPattern : BossPattern
    {
        readonly List<CrowdPreference> _banned = new List<CrowdPreference>(2);
        int _positives;
        bool _ownsFilter;
        int Required => Enhanced ? Config.KillSwitchPositivesEnhanced : Config.KillSwitchPositives;

        public override string Id => "kill_switch";
        public override string Title => "KILL SWITCH";
        public override string Instruction
        {
            get
            {
                var labels = new List<string>(_banned.Count);
                for (int i = 0; i < _banned.Count; i++) labels.Add(PreferenceLabel(_banned[i]));
                return $"{string.Join(" · ", labels)} 카드 봉인 — 나머지 카드로 좋은 반응 {Required}번!";
            }
        }
        public override string ProgressText => $"{_positives} / {Required}";

        public override bool CanStart() => CardInput.UseFilter == null; // 튜토리얼 등 다른 주인이 있으면 건너뛴다

        public override void Begin()
        {
            _positives = 0;
            _banned.Clear();

            var pool = new List<CrowdPreference> { CrowdPreference.Chill, CrowdPreference.Singalong, CrowdPreference.Mosh };
            int count = Mathf.Min(Enhanced ? Config.KillSwitchBannedPreferencesEnhanced : Config.KillSwitchBannedPreferences, 2);
            for (int i = 0; i < count; i++)
            {
                CrowdPreference pick = pool[Random.Next(pool.Count)];
                pool.Remove(pick);
                _banned.Add(pick);
            }

            if (CardInput.UseFilter == null)
            {
                CardInput.UseFilter = FilterCard;
                _ownsFilter = true;
            }
        }

        public override void End(bool success)
        {
            if (_ownsFilter && CardInput.UseFilter == (Func<int, bool>)FilterCard) CardInput.UseFilter = null;
            _ownsFilter = false;
        }

        public override void OnCardResolved(CardResolved e)
        {
            if (!IsPerformanceCard(e.Role) || e.GainedScore <= 0) return;
            _positives++;
            if (_positives >= Required) Achieved = true;
        }

        public bool IsBanned(CrowdPreference preference) => _banned.Contains(preference);

        bool FilterCard(int handIndex)
        {
            if (!CardSystem.HasInstance) return true;
            CardDefinition card = CardSystem.Instance.GetCard(handIndex);
            if (card == null || card.Role != CardRole.Normal) return true; // Special·유틸리티는 봉인하지 않는다
            return !_banned.Contains(card.TargetPreference);
        }
    }

    /// <summary>DROP — 드롭 전에 피버에 진입한다. 이미 피버면 즉시 성공. 콤보는 건드리지 않는다.</summary>
    public sealed class DropPattern : BossPattern
    {
        public override string Id => "drop";
        public override string Title => "DROP";
        public override string Instruction => "빌드업이 끝나기 전에 FEVER 를 터뜨려라!";
        public override string ProgressText
        {
            get
            {
                if (Achieved) return "FEVER!";
                if (!ComboSystem.HasInstance || !FeverSystem.HasInstance || FeverSystem.Instance.Config == null) return "";
                int interval = FeverSystem.Instance.Config.FeverComboInterval;
                int combo = ComboSystem.Instance.CurrentCombo;
                int need = interval - (combo % interval);
                return $"피버까지 콤보 {need}";
            }
        }

        public override void Begin()
        {
            if (FeverSystem.HasInstance && FeverSystem.Instance.IsActive) Achieved = true;
        }

        public override void OnFeverChanged(FeverStateChanged e)
        {
            if (e.IsActive) Achieved = true;
        }
    }
}
