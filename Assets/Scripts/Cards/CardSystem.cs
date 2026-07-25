using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 프리팹 카드 손패와 10장 단위 현재/대기 덱을 관리한다.
    /// 공연 카드는 현재 관객을 개별 계산해 몰입도를 갱신하고 반응값 합계를 점수로 발행한다.
    /// 사용한 카드는 사라지고, 덱 묶음이 소진되면 미리 만든 다음 묶음으로 이어서 뽑는다.
    /// </summary>
    public sealed class CardSystem : MonoSingleton<CardSystem>
    {
        [SerializeField] CardDeckConfig config;
        [SerializeField] AudienceRosterSystem audienceRoster;

        readonly List<CardDefinition> _hand = new List<CardDefinition>();
        readonly List<AudienceReactionResult> _audienceReactions =
            new List<AudienceReactionResult>(10);
        readonly List<AudienceId> _audienceIds = new List<AudienceId>(10);
        PreparedDeck<CardDefinition> _deck;

        bool _selecting;
        bool _warnedInvalidPool;

        protected override bool Persistent => false;

        public CardDeckConfig Config => config;
        public IReadOnlyList<CardDefinition> Hand => _hand;
        public int HandCount => _hand.Count;
        public int MinimumHandSize => config != null ? config.MinimumHandSize : 0;
        public int BonusCardCount => Mathf.Max(0, HandCount - MinimumHandSize);
        public int PreparedDeckCount => _deck != null ? _deck.PreparedBatchCount : 0;
        public int CurrentDeckRemaining => _deck != null ? _deck.CurrentRemaining : 0;
        public AudienceRosterSystem AudienceRoster => audienceRoster;

        protected override void OnAwake()
        {
            if (audienceRoster == null)
            {
                Debug.LogError(
                    "[CardSystem] An AudienceRosterSystem reference is required.",
                    this);
            }
            StartRun();
        }

        void Start()
        {
            if (GameManager.HasInstance &&
                GameManager.Instance.State == GameState.Ready)
            {
                GameManager.Instance.StartGame();
            }
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
                StartRun();
            else if (e.Previous == GameState.Ready && e.Current == GameState.Playing && _hand.Count == 0)
                StartRun();
        }

        /// <summary>새 공연용 현재 덱과 대기 덱을 만들고 기본 손패를 준비한다.</summary>
        public void StartRun()
        {
            _deck = null;
            _hand.Clear();
            _selecting = false;
            _warnedInvalidPool = false;

            if (config == null || !config.HasUsableCards)
            {
                Debug.LogWarning("[CardSystem] 사용할 카드 프리팹이 없습니다. Tools/Cards/Setup Prefab Card System을 실행하세요.");
                RaiseHandChanged();
                return;
            }

            _deck = new PreparedDeck<CardDefinition>(
                config.PreparedDeckCount,
                BuildDeckBatch);
            _deck.Reset();
            RefillToMinimumHand();
            RaiseHandChanged();
        }

        /// <summary>현재 손패의 index 카드를 사용한다. 성공했을 때만 true를 반환한다.</summary>
        public bool SelectCard(int index)
            => SelectCard(index, SpecialCardRequest.None);

        /// <summary>
        /// 드롭 순간 확정한 특별 관객 요청과 함께 카드를 사용한다.
        /// 위치 정보가 없는 호출은 None을 사용하므로 특수 카드가 임의의 위치에서 성공하지 않는다.
        /// </summary>
        public bool SelectCard(int index, SpecialCardRequest specialRequest)
        {
            if (_selecting) return false;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return false;
            if (audienceRoster == null ||
                !audienceRoster.isActiveAndEnabled ||
                !audienceRoster.IsConfigured)
            {
                Debug.LogError(
                    "[CardSystem] Active configured AudienceRosterSystem is required.",
                    this);
                return false;
            }
            if (index < 0 || index >= _hand.Count) return false;

            _selecting = true;
            try
            {
                var card = _hand[index];
                int handCountBeforeUse = _hand.Count;
                AudienceReactionProfile profile = card.AudienceReaction;
                if (profile == null)
                {
                    Debug.LogError(
                        $"[CardSystem] Card '{card.Id}' has no audience reaction data.",
                        card);
                    return false;
                }
                if (!profile.TryValidate(out string profileError))
                {
                    Debug.LogError(
                        $"[CardSystem] Card '{card.Id}' has invalid audience reaction data: " +
                        $"{profileError}",
                        this);
                    return false;
                }
                if (card.Role != CardRole.Utility && !profile.AppliesToAudience)
                {
                    Debug.LogError(
                        $"[CardSystem] Performance card '{card.Id}' must apply an audience reaction.",
                        card);
                    return false;
                }

                int gainedScore = 0;
                int positiveReactionCount = 0;
                int reactionAudienceCount = 0;
                bool isSpecialHit = false;
                int specialBonusScore = 0;

                bool matchesTargetedRequest =
                    card.Role == CardRole.Special &&
                    specialRequest.IsActive &&
                    card.TargetStage == specialRequest.RequestedStage;
                if (matchesTargetedRequest)
                {
                    SpecialCardTargetEffect targetEffect =
                        card.SpecialTargetEffect;
                    string targetError = targetEffect == null
                        ? "Target effect data is missing."
                        : string.Empty;
                    if (targetEffect == null ||
                        !targetEffect.TryValidate(out targetError))
                    {
                        Debug.LogError(
                            $"[CardSystem] Special card '{card.Id}' has invalid " +
                            $"target effect data: {targetError}",
                            card);
                        return false;
                    }

                    // 드롭과 카드 사용은 같은 프레임에 동기적으로 처리된다.
                    // 요청 소비에 성공한 경우에만 저격 효과를 적용하고, 만료 등으로
                    // 소비하지 못했다면 일반 사용으로 자연스럽게 처리한다.
                    if (SpecialAudience.ConsumeRequest(
                            card.TargetStage,
                            targetEffect.TargetScoreBonus,
                            0f))
                    {
                        if (!SpecialCardTargetEffectExecutor.TryExecute(
                                targetEffect,
                                audienceRoster,
                                _audienceIds,
                                out _,
                                out string executionError))
                        {
                            Debug.LogError(
                                $"[CardSystem] Failed to execute targeted effect " +
                                $"for '{card.Id}': {executionError}",
                                card);
                            return false;
                        }

                        isSpecialHit = true;
                        specialBonusScore = targetEffect.TargetScoreBonus;
                    }
                }

                if (!isSpecialHit &&
                    !TryApplyGeneralAudienceReaction(
                        card,
                        profile,
                        out gainedScore,
                        out positiveReactionCount,
                        out reactionAudienceCount))
                {
                    return false;
                }

                _hand.RemoveAt(index);
                int rawScore = gainedScore + specialBonusScore;
                ComboResolution combo = ComboSystem.HasInstance
                    ? ComboSystem.Instance.ResolveCard(
                        card.Role,
                        rawScore,
                        isSpecialHit)
                    : new ComboResolution(0, 1f, isSpecialHit || rawScore > 0);
                int comboScore = card.Role == CardRole.Utility
                    ? 0
                    : Mathf.RoundToInt(rawScore * combo.Multiplier);
                int feverAudienceCount = audienceRoster.Members.Count;
                bool isFeverActive =
                    FeverSystem.HasInstance &&
                    FeverSystem.Instance.IsActive;
                int feverBonusScore = isFeverActive
                    ? FeverSystem.Instance.CalculateCardBonus(feverAudienceCount)
                    : 0;
                // Fever replaces the card's normal score. Reactions and targeted
                // effects still change the audience, but only the per-audience
                // Fever reward is added to the run score.
                int finalScore = isFeverActive
                    ? feverBonusScore
                    : comboScore;
                HypeJudgement feedback = isSpecialHit
                    ? HypeJudgement.Perfect
                    : ResolveFeedback(
                        gainedScore,
                        positiveReactionCount,
                        reactionAudienceCount);

                // CardSelected is retained as the input/audio/visual notification contract.
                // Gameplay score and engagement use the per-audience result above.
                EventBus.Raise(new CardSelected
                {
                    CardId = card.Id,
                    DisplayName = card.DisplayName,
                    HandIndex = index,
                    Judgement = feedback,
                    Delta = 0f,
                    BaseScore = rawScore,
                    Multiplier = combo.Multiplier
                });

                EventBus.Raise(new CardResolved
                {
                    CardId = card.Id,
                    DisplayName = card.DisplayName,
                    HandIndex = index,
                    Role = card.Role,
                    TargetPreference = card.TargetPreference,
                    CrowdReaction = isSpecialHit || gainedScore > 0
                        ? CrowdReactionGrade.Good
                        : CrowdReactionGrade.Weak,
                    Judgement = feedback,
                    BaseScore = rawScore,
                    HypeMultiplier = combo.Multiplier,
                    CrowdMultiplier = 1f,
                    GainedScore = finalScore,
                    HypeDelta = 0f,
                    IsSpecialHit = isSpecialHit,
                    RawAudienceScore = gainedScore,
                    SpecialBonusScore = specialBonusScore,
                    RawScore = rawScore,
                    ComboCount = combo.Combo,
                    ComboMultiplier = combo.Multiplier,
                    FeverAudienceCount = feverAudienceCount,
                    FeverBonusScore = feverBonusScore
                });

                ResolveHandEffect(card, handCountBeforeUse);
                RaiseHandChanged();
                return true;
            }
            finally
            {
                _selecting = false;
            }
        }

        bool TryApplyGeneralAudienceReaction(
            CardDefinition card,
            AudienceReactionProfile profile,
            out int gainedScore,
            out int positiveReactionCount,
            out int audienceCount)
        {
            _audienceReactions.Clear();
            IReadOnlyList<AudienceSnapshot> members = audienceRoster.Members;
            gainedScore = 0;
            positiveReactionCount = 0;
            audienceCount = members.Count;

            for (int i = 0; i < members.Count; i++)
            {
                AudienceReactionResult reaction =
                    AudienceReactionResolver.Resolve(profile, members[i]);
                _audienceReactions.Add(reaction);
                gainedScore += reaction.Value;
                if (reaction.Value > 0) positiveReactionCount++;
            }

            for (int i = 0; i < _audienceReactions.Count; i++)
            {
                AudienceReactionResult reaction = _audienceReactions[i];
                if (audienceRoster.TryApplyCardReaction(
                        reaction.AudienceId,
                        card.Id,
                        reaction.Value,
                        profile.EngagementMultiplier,
                        out _))
                    continue;

                Debug.LogError(
                    $"[CardSystem] Failed to apply '{card.Id}' to audience " +
                    $"{reaction.AudienceId}.",
                    this);
                return false;
            }

            return true;
        }

        static HypeJudgement ResolveFeedback(
            int totalReaction,
            int positiveReactionCount,
            int audienceCount)
        {
            if (totalReaction <= 0 || positiveReactionCount <= 0)
                return HypeJudgement.Miss;
            if (audienceCount > 0 &&
                positiveReactionCount == audienceCount &&
                totalReaction >= audienceCount * 5)
                return HypeJudgement.Perfect;
            return HypeJudgement.Good;
        }

        public CardDefinition GetCard(int index)
            => index >= 0 && index < _hand.Count ? _hand[index] : null;

        /// <summary>외부 카드 효과로 손패에 카드를 추가할 때 사용하는 API.</summary>
        public int AddCards(int count)
        {
            int drawn = DrawCards(count);
            if (drawn > 0 && !_selecting) RaiseHandChanged();
            return drawn;
        }

        void ResolveHandEffect(CardDefinition card, int handCountBeforeUse)
        {
            if (card.Role != CardRole.Utility)
            {
                RefillToMinimumHand();
                return;
            }

            switch (card.UtilityEffect)
            {
                case UtilityCardEffect.Draw:
                    DrawCards(card.DrawCount);
                    break;
                case UtilityCardEffect.Reroll:
                    _hand.Clear();
                    DrawCards(handCountBeforeUse);
                    break;
                default:
                    RefillToMinimumHand();
                    break;
            }
        }

        int RefillToMinimumHand()
        {
            int missing = Mathf.Max(0, MinimumHandSize - _hand.Count);
            return DrawCards(missing);
        }

        int DrawCards(int count)
        {
            if (count <= 0) return 0;

            int drawn = 0;
            while (drawn < count && DrawOne())
                drawn++;

            return drawn;
        }

        bool DrawOne()
        {
            if (_deck == null)
            {
                if (!_warnedInvalidPool)
                {
                    Debug.LogWarning("[CardSystem] 카드 풀에서 새 덱을 만들 수 없습니다.");
                    _warnedInvalidPool = true;
                }
                return false;
            }

            var card = _deck.Draw();
            if (card == null)
            {
                if (!_warnedInvalidPool)
                {
                    Debug.LogWarning("[CardSystem] 카드 풀에서 새 덱을 만들 수 없습니다.");
                    _warnedInvalidPool = true;
                }
                return false;
            }

            _hand.Add(card);
            EventBus.Raise(new CardDrawn
            {
                CardId = card.Id,
                DisplayName = card.DisplayName,
                HandIndex = _hand.Count - 1,
                IsEncoreBonus = false
            });

            return true;
        }

        List<CardDefinition> BuildDeckBatch()
        {
            if (CardDeckBatchBuilder.TryBuild(config, out var cards, out string error))
                return cards;

            if (!_warnedInvalidPool)
            {
                Debug.LogError($"[CardSystem] Invalid generated deck: {error}", this);
                _warnedInvalidPool = true;
            }

            return null;
        }

        void RaiseHandChanged()
        {
            EventBus.Raise(new HandChanged
            {
                Count = _hand.Count,
                BaseHandSize = MinimumHandSize,
                BonusCardCount = BonusCardCount
            });
        }
    }
}
