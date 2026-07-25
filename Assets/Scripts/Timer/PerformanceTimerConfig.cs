using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 공연 시간(제한시간) + 목표 점수 수치 모음. (기획: .myDox/열기(점수 배율) 시스템 작업계획.md 의 후속 작업)
    ///
    /// 목표 점수는 여기 한 곳에서만 관리한다 — ScoreUI 는 표시만 하고,
    /// PerformanceTimerSystem 이 이 값을 읽어 시간 초과 시 성공/실패를 판정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PerformanceTimerConfig", menuName = "ContextStage/Performance Timer Config")]
    public class PerformanceTimerConfig : ScriptableObject
    {
        [Tooltip("공연 제한시간(초). 다 차면 공연이 끝난다")]
        public float duration = 120f;

        [Tooltip("제한시간 안에 달성해야 하는 목표 점수")]
        public int targetScore = 5000;
    }
}
