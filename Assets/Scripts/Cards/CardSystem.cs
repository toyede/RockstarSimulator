using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 프리팹 카드 손패와 10장 단위 현재/대기 덱을 관리한다.
    /// 사용한 카드는 사라지고, 덱 묶음이 소진되면 미리 만든 다음 묶음으로 이어서 뽑는다.
    /// </summary>
    public sealed class CardSystem : MonoSingleton<CardSystem>
    {
        [SerializeField] CardDeckConfig config;

        readonly List<CardDefinition> _hand = new List<CardDefinition>();
        PreparedDeck<CardDefinition> _deck;

        bool _selecting;
        bool _warnedInvalidPool;

        protected override bool Persistent => false;

        public CardDeckConfig Config => config;
        public IReadOnlyList<CardDefinition> Hand => _hand;
        public int HandCount => _hand.Count;
        public int MinimumHandSize => config != null ? config.MinimumHandSize : 0;
        public int MaximumHandSize => config != null ? config.MaximumHandSize : 0;
        public int BonusCardCount => Mathf.Max(0, HandCount - MinimumHandSize);
        public int PreparedDeckCount => _deck != null ? _deck.PreparedBatchCount : 0;
        public int CurrentDeckRemaining => _deck != null ? _deck.CurrentRemaining : 0;

        protected override void OnAwake() => StartRun();

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
                config.GeneratedDeckSize,
                config.PreparedDeckCount,
                PickWeightedCard);
            _deck.Reset();
            RefillToMinimumHand();
            RaiseHandChanged();
        }

        /// <summary>현재 손패의 index 카드를 사용한다. 성공했을 때만 true를 반환한다.</summary>
        public bool SelectCard(int index)
        {
            if (_selecting) return false;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return false;
            if (!HypeSystem.HasInstance)
            {
                Debug.LogWarning("[CardSystem] HypeSystem이 없어 카드를 사용할 수 없습니다.");
                return false;
            }
            if (index < 0 || index >= _hand.Count) return false;

            _selecting = true;
            try
            {
                var card = _hand[index];
                _hand.RemoveAt(index);

                float currentHype = Hype.Current;
                var result = CardEffectResolver.Resolve(
                    card,
                    currentHype,
                    HypeSystem.Instance.Config,
                    SpecialCardRequest.None);
                float multiplier = Hype.MultiplierFor(currentHype);

                // 동기 EventBus 구독자가 현재 열기 배율로 점수를 먼저 반영한다.
                EventBus.Raise(new CardSelected
                {
                    CardId = card.Id,
                    DisplayName = card.DisplayName,
                    HandIndex = index,
                    Judgement = result.Judgement,
                    Delta = result.HeatDelta,
                    BaseScore = result.BaseScore,
                    Multiplier = multiplier
                });

                // 이 카드의 점수 계산이 끝난 뒤 열기를 변경한다.
                if (!Mathf.Approximately(result.HeatDelta, 0f))
                    Hype.ApplyDelta(result.HeatDelta, result.Judgement);

                ResolveHandEffect(card);
                RaiseHandChanged();
                return true;
            }
            finally
            {
                _selecting = false;
            }
        }

        public CardDefinition GetCard(int index)
            => index >= 0 && index < _hand.Count ? _hand[index] : null;

        /// <summary>외부 카드 효과가 손패 최대치 안에서 카드를 추가할 때 사용하는 API.</summary>
        public int AddCards(int count)
        {
            int drawn = DrawCards(count);
            if (drawn > 0 && !_selecting) RaiseHandChanged();
            return drawn;
        }

        void ResolveHandEffect(CardDefinition card)
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
                    DrawCards(card.RerollDrawCount);
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
            while (drawn < count && _hand.Count < MaximumHandSize && DrawOne())
                drawn++;

            return drawn;
        }

        bool DrawOne()
        {
            if (_hand.Count >= MaximumHandSize) return false;
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

        CardDefinition PickWeightedCard()
        {
            if (config == null || !config.HasUsableCards) return null;

            float totalWeight = 0f;
            var pool = config.CardPool;
            for (int i = 0; i < pool.Count; i++)
            {
                var entry = pool[i];
                if (entry != null && entry.IsUsable) totalWeight += entry.Weight;
            }
            if (totalWeight <= 0f) return null;

            float roll = Random.value * totalWeight;
            for (int i = 0; i < pool.Count; i++)
            {
                var entry = pool[i];
                if (entry == null || !entry.IsUsable) continue;

                roll -= entry.Weight;
                if (roll <= 0f) return entry.Prefab;
            }

            for (int i = pool.Count - 1; i >= 0; i--)
            {
                if (pool[i] != null && pool[i].IsUsable) return pool[i].Prefab;
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
