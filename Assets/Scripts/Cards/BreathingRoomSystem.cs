using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 숨 고르기 증강의 공연 단위 타이머다.
    /// 카드를 사용하지 않은 동안 티어 주기마다 손패에 한 장을 계속 추가한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BreathingRoomSystem : MonoBehaviour
    {
        float _intervalSeconds;
        float _elapsedSeconds;

        public float IntervalSeconds => _intervalSeconds;
        public float ElapsedSeconds => _elapsedSeconds;

        void OnEnable()
        {
            EventBus.Subscribe<CardSelected>(OnCardSelected);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            RefreshModifier(resetTimer: true);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<CardSelected>(OnCardSelected);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        void Update()
        {
            if (_intervalSeconds <= 0f ||
                !GameManager.HasInstance ||
                !GameManager.Instance.IsPlaying)
                return;

            if (TutorialFlow.IsRunning)
            {
                _elapsedSeconds = 0f;
                return;
            }

            _elapsedSeconds += Time.deltaTime;
            while (_elapsedSeconds >= _intervalSeconds)
            {
                _elapsedSeconds -= _intervalSeconds;
                if (CardSystem.HasInstance)
                    CardSystem.Instance.AddCards(1);
            }
        }

        void OnCardSelected(CardSelected _)
        {
            // 성공한 카드 사용만 CardSelected를 발행하므로 모든 카드 역할을 함께 처리한다.
            _elapsedSeconds = 0f;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Playing)
            {
                RefreshModifier(resetTimer: true);
                return;
            }

            if (e.Current == GameState.Ready || e.Current == GameState.GameOver)
                _elapsedSeconds = 0f;
        }

        void RefreshModifier(bool resetTimer)
        {
            _intervalSeconds =
                AugmentRuntime.Current.PeriodicIdleDrawIntervalSeconds;
            if (resetTimer) _elapsedSeconds = 0f;
        }
    }
}
