using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 공연 점수 누적 담당. CardSystem이 계산한 관객별 반응값 합계를 점수에 더한다.
    ///
    /// - 점수 저장·이벤트는 킷 GameManager(Score/AddScore/ScoreChanged)를 재사용한다.
    /// - CardResolved 이벤트만 구독하므로 CardSystem 을 직접 참조하지 않는다.
    ///   (카드 담당의 손패 로직과 점수 계산을 분리해 서로 충돌하지 않게 한다)
    /// - 점수는 스테이지 동안 무한 누적. 클리어/등급 판정은 후속 작업.
    /// </summary>
    public sealed class PerformanceScoreSystem : MonoSingleton<PerformanceScoreSystem>
    {
        bool _comboWasBroken;
        bool _finalRewardApplied;

        /// <summary>씬 재시작 시 새로 초기화되도록 씬에 종속시킨다. (HypeSystem 과 동일)</summary>
        protected override bool Persistent => false;

        public bool WasPerfectClear => !_comboWasBroken;
        public bool FinalRewardApplied => _finalRewardApplied;

        void OnEnable()
        {
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnCardResolved(CardResolved e)
        {
            if (!GameManager.HasInstance) return;

            // 고위험 증강의 점수 배율은 음수 점수도 같이 키운다.
            if (e.GainedScore != 0)
            {
                int adjustedScore = Mathf.RoundToInt(
                    e.GainedScore *
                    AugmentRuntime.Current.PerformanceScoreMultiplier);
                GameManager.Instance.AddScore(adjustedScore);
            }
        }

        /// <summary>
        /// 제한시간을 정상 소진한 순간에만 호출된다.
        /// GameOver 이전에 점수를 반영해 하이스코어와 스테이지 판정도 같은 값을 사용한다.
        /// </summary>
        public bool ApplyTimedPerformanceFinalRewards()
        {
            if (_finalRewardApplied ||
                _comboWasBroken ||
                !GameManager.HasInstance)
                return false;

            float multiplier = AugmentRuntime.Current.PerfectClearScoreMultiplier;
            if (multiplier <= 1f) return false;

            int currentScore = GameManager.Instance.Score;
            int finalScore = Mathf.RoundToInt(currentScore * multiplier);
            GameManager.Instance.AddScore(finalScore - currentScore);
            _finalRewardApplied = true;
            return true;
        }

        void OnComboChanged(ComboChanged e)
        {
            if (e.WasLost) _comboWasBroken = true;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current != GameState.Ready) return;

            _comboWasBroken = false;
            _finalRewardApplied = false;
        }
    }
}
