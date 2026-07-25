using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 공연 시간(제한시간) 시스템. HypeSystem 과 같은 구조를 따른다.
    ///
    /// - 공연 시작(Ready → Playing) 시 0부터 시작해 매 프레임 누적
    /// - Playing 상태일 때만 흐른다 (대기·일시정지·게임오버 중엔 정지)
    /// - 제한시간에 도달하면 딱 한 번 EvaluateStageEnd() 를 호출해
    ///   목표 점수(PerformanceTimerConfig.targetScore) 미달성 시 GameManager.GameOver() 를 호출한다.
    ///   목표를 이미 달성한 경우는 이번 범위에서 아무 것도 하지 않는다 (클리어 연출은 후속 작업).
    ///
    /// [다른 담당자용 API]
    ///   PerformanceTimer.Elapsed / Duration / Normalized / TargetScore
    /// </summary>
    public class PerformanceTimerSystem : MonoSingleton<PerformanceTimerSystem>
    {
        [SerializeField, Tooltip("필수 밸런스 수치 에셋")]
        PerformanceTimerConfig config;

        float _elapsed;
        bool _ended; // 시간 초과 판정을 한 번만 실행하기 위한 가드

        protected override bool Persistent => false;

        public float Elapsed => _elapsed;
        public float Duration => config == null ? 0f : config.duration;
        public float Normalized => Duration <= 0f ? 0f : Mathf.Clamp01(_elapsed / Duration);
        public int TargetScore => config == null ? 0 : config.targetScore;

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연 시작(Ready → Playing)에만 초기화. Paused → Playing 재개 때는 유지된다.
            if (e.Previous == GameState.Ready && e.Current == GameState.Playing)
                ResetTimer();
        }

        /// <summary>경과 시간을 0으로 되돌린다. (공연 시작 시 자동 호출)</summary>
        public void ResetTimer()
        {
            _elapsed = 0f;
            _ended = false;
            RaiseChanged();
        }

        void Update()
        {
            if (config == null || _ended) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= Duration)
            {
                _elapsed = Duration;
                _ended = true;
                RaiseChanged();
                EvaluateStageEnd();
                return;
            }

            RaiseChanged();
        }

        /// <summary>제한시간이 다 찼을 때 성공/실패를 판정한다. 실패(목표 미달성)면 게임오버.</summary>
        void EvaluateStageEnd()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.Score < TargetScore) gm.GameOver();
            // 목표 달성(성공) 시 처리는 후속 작업 — 이번 범위에서는 아무 것도 하지 않는다.
        }

        void RaiseChanged()
        {
            EventBus.Raise(new PerformanceTimeChanged
            {
                Elapsed = _elapsed,
                Duration = Duration,
                Normalized = Normalized
            });
        }
    }

    /// <summary>
    /// 어디서든 한 줄로 공연 시간을 읽는 전역 접근자. (Hype 와 같은 정적 파사드 패턴)
    ///
    ///   float t = PerformanceTimer.Elapsed;
    ///   float ratio = PerformanceTimer.Normalized;
    ///   int target = PerformanceTimer.TargetScore;
    /// </summary>
    public static class PerformanceTimer
    {
        /// <summary>경과 시간(초). 씬에 시스템이 없으면 0.</summary>
        public static float Elapsed => PerformanceTimerSystem.HasInstance ? PerformanceTimerSystem.Instance.Elapsed : 0f;

        /// <summary>공연 제한시간(초). 씬에 시스템이 없으면 0.</summary>
        public static float Duration => PerformanceTimerSystem.HasInstance ? PerformanceTimerSystem.Instance.Duration : 0f;

        /// <summary>경과 시간의 0~1 비율.</summary>
        public static float Normalized => PerformanceTimerSystem.HasInstance ? PerformanceTimerSystem.Instance.Normalized : 0f;

        /// <summary>목표 점수. 씬에 시스템이 없으면 0(목표 표시 안 함으로 취급됨).</summary>
        public static int TargetScore => PerformanceTimerSystem.HasInstance ? PerformanceTimerSystem.Instance.TargetScore : 0;
    }
}
