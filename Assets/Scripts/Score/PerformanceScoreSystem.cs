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
        /// <summary>씬 재시작 시 새로 초기화되도록 씬에 종속시킨다. (HypeSystem 과 동일)</summary>
        protected override bool Persistent => false;

        void OnEnable() => EventBus.Subscribe<CardResolved>(OnCardResolved);
        void OnDisable() => EventBus.Unsubscribe<CardResolved>(OnCardResolved);

        void OnCardResolved(CardResolved e)
        {
            if (!GameManager.HasInstance) return;

            // 획득 점수 = 현재 관객별 최종 반응값의 합
            if (e.GainedScore != 0)
                GameManager.Instance.AddScore(e.GainedScore);
        }
    }
}
