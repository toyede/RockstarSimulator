using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 덱, 손패와 숫자키 선택 흐름을 관리한다.
    ///
    /// - 새 공연을 시작할 때만 CardDeckConfig.BaseHandSize만큼 뽑는다.
    /// - 사용한 카드는 손패에서 사라진 뒤 드로우 더미에 다시 섞인다.
    /// - EncoreTriggered를 받을 때만 카드 한 장을 추가로 뽑는다.
    /// </summary>
    public sealed class CardSystem : MonoSingleton<CardSystem>
    {
        const int MaxSelectableCards = 9;

        [SerializeField] CardDeckConfig config;

        readonly List<CardData> _drawPile = new List<CardData>();
        readonly List<CardData> _hand = new List<CardData>();

        int _bonusCardCount;
        bool _selecting;
        bool _warnedEmptyDeck;

        protected override bool Persistent => false;

        public CardDeckConfig Config => config;
        public IReadOnlyList<CardData> Hand => _hand;
        public int HandCount => _hand.Count;
        public int BaseHandSize => config != null ? config.BaseHandSize : 0;
        public int BonusCardCount => _bonusCardCount;

        protected override void OnAwake()
        {
            StartRun();
        }

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<EncoreTriggered>(OnEncoreTriggered);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<EncoreTriggered>(OnEncoreTriggered);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready)
                StartRun();
            else if (e.Previous == GameState.Ready && e.Current == GameState.Playing && _hand.Count == 0)
                StartRun();
        }

        /// <summary>새 공연용 덱과 손패를 준비한다. Ready 상태에서도 카드는 화면에 보인다.</summary>
        public void StartRun()
        {
            _drawPile.Clear();
            _hand.Clear();
            _bonusCardCount = 0;
            _selecting = false;
            _warnedEmptyDeck = false;

            if (config == null || config.Cards == null || config.Cards.Count == 0)
            {
                Debug.LogWarning("[CardSystem] CardDeckConfig가 비어 있습니다. Tools/Cards/Setup Card Prototype을 실행하세요.");
                RaiseHandChanged();
                return;
            }

            for (int i = 0; i < config.Cards.Count; i++)
            {
                var card = config.Cards[i];
                if (card != null) _drawPile.Add(card);
            }

            if (config.ShuffleOnStart) Shuffle(_drawPile);
            DrawInitialHand();
            RaiseHandChanged();
        }

        /// <summary>현재 손패의 index 카드를 사용한다. 성공했을 때만 true.</summary>
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
                // 앙코르로 추가된 선택지는 다음 카드 한 번을 고를 때 소비된 것으로 센다.
                if (_bonusCardCount > 0) _bonusCardCount--;

                var card = _hand[index];
                _hand.RemoveAt(index);
                ReturnToDrawPile(card);

                float currentHype = Hype.Current;
                HypeJudgement judgement = card.ResolveJudgement(currentHype);
                float delta = card.GetPreviewDelta(currentHype, HypeSystem.Instance.Config);
                Hype.Apply(judgement);

                EventBus.Raise(new CardSelected
                {
                    CardId = card.Id,
                    DisplayName = card.DisplayName,
                    HandIndex = index,
                    Judgement = judgement,
                    Delta = delta
                });

                RaiseHandChanged();
                return true;
            }
            finally
            {
                _selecting = false;
            }
        }

        public CardData GetCard(int index)
            => index >= 0 && index < _hand.Count ? _hand[index] : null;

        void OnEncoreTriggered(EncoreTriggered _)
        {
            if (_hand.Count >= MaxSelectableCards) return;
            if (!DrawOne(true)) return;

            _bonusCardCount++;
            RaiseHandChanged();
        }

        void DrawInitialHand()
        {
            int target = Mathf.Min(BaseHandSize, MaxSelectableCards);
            while (_hand.Count < target && DrawOne(false)) { }
        }

        bool DrawOne(bool isEncoreBonus)
        {
            if (_drawPile.Count == 0)
            {
                if (!_warnedEmptyDeck)
                {
                    Debug.LogWarning("[CardSystem] 드로우할 카드가 부족합니다. 덱의 카드 장수를 늘려주세요.");
                    _warnedEmptyDeck = true;
                }
                return false;
            }

            int last = _drawPile.Count - 1;
            var card = _drawPile[last];
            _drawPile.RemoveAt(last);
            _hand.Add(card);

            EventBus.Raise(new CardDrawn
            {
                CardId = card.Id,
                DisplayName = card.DisplayName,
                HandIndex = _hand.Count - 1,
                IsEncoreBonus = isEncoreBonus
            });
            return true;
        }

        void ReturnToDrawPile(CardData card)
        {
            if (card == null) return;
            _drawPile.Add(card);
            Shuffle(_drawPile);
        }

        void RaiseHandChanged()
        {
            EventBus.Raise(new HandChanged
            {
                Count = _hand.Count,
                BaseHandSize = BaseHandSize,
                BonusCardCount = _bonusCardCount
            });
        }

        static void Shuffle(List<CardData> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }
    }
}
