using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 공연 시간(제한시간) 시스템. HypeSystem 과 같은 구조를 따른다.
    ///
    /// - 공연 시작(Ready → Playing) 시 0부터 시작해 매 프레임 누적
    /// - Playing 상태일 때만 흐른다 (대기·일시정지·게임오버 중엔 정지)
    /// - 제한시간에 도달하면 딱 한 번 EvaluateStageEnd() 를 호출해 GameManager.GameOver() 로 공연을 종료시킨다.
    ///   (관객 이탈 등 다른 원인으로 GameOver 가 될 수도 있다 — 그래서 Failed 는 특정 트리거에서만
    ///    세팅되는 플래그가 아니라, 매번 "현재 점수 &lt; 목표 점수" 로 계산되는 파생값이다.
    ///    어떤 경로로 GameOver 가 됐든 항상 올바른 성공/실패 판정을 준다.)
    ///
    /// [다른 담당자용 API]
    ///   PerformanceTimer.Elapsed / Duration / Normalized / TargetScore / Failed
    /// </summary>
    public class PerformanceTimerSystem : MonoSingleton<PerformanceTimerSystem>
    {
        [SerializeField, Tooltip("필수 밸런스 수치 에셋")]
        PerformanceTimerConfig config;

        float _elapsed;
        bool _ended; // 시간 초과 판정을 한 번만 실행하기 위한 가드
        bool _pausedManually; // 연출 담당(튜토리얼 등)이 SetPaused 로 제어하는 정지 플래그

        protected override bool Persistent => false;

        public float Elapsed => _elapsed;
        public float Duration => config == null ? 0f : config.duration;
        public float Normalized => Duration <= 0f ? 0f : Mathf.Clamp01(_elapsed / Duration);
        public int TargetScore => config == null ? 0 : config.targetScore;
        public bool IsPaused => _pausedManually;

        /// <summary>현재 점수가 목표 점수 미달인지. 특정 트리거가 아니라 항상 실시간으로 계산된다.</summary>
        public bool Failed => GameManager.HasInstance && GameManager.Instance.Score < TargetScore;

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

        /// <summary>[연출/튜토리얼 전용] true 인 동안 제한시간이 흐르지 않는다.</summary>
        public void SetPaused(bool paused) => _pausedManually = paused;

        void Update()
        {
            if (config == null || _ended || _pausedManually) return;

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

        /// <summary>제한시간이 다 찼을 때 공연을 종료시킨다. 성공/실패 판정은 Failed 가 알아서 계산한다.</summary>
        void EvaluateStageEnd()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.GameOver(); // 시간 초과는 성공/실패 모두 공연 종료
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

        /// <summary>현재 점수가 목표 점수 미달인지(실시간 계산). 씬에 시스템이 없으면 false.</summary>
        public static bool Failed => PerformanceTimerSystem.HasInstance && PerformanceTimerSystem.Instance.Failed;

        /// <summary>제한시간이 수동으로 정지된 상태인지. 씬에 시스템이 없으면 false.</summary>
        public static bool IsPaused => PerformanceTimerSystem.HasInstance && PerformanceTimerSystem.Instance.IsPaused;

        /// <summary>[연출/튜토리얼 전용] true 인 동안 제한시간이 흐르지 않는다.</summary>
        public static void SetPaused(bool paused)
        {
            if (PerformanceTimerSystem.HasInstance)
                PerformanceTimerSystem.Instance.SetPaused(paused);
        }
    }
}
