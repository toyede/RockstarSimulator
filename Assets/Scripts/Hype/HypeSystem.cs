using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 열기(구 호응도) 시스템 핵심 매니저. (기획: .myDox/열기(점수 배율) 시스템 작업계획.md)
    ///
    /// 열기는 승패 조건이 아니라 "점수 배율"을 유지하는 긴장 요소다.
    /// - 공연 시작 시 30 에서 출발, 카드 선택 중 초당 1 감소
    /// - 0 도달  → 게임오버 아님. 배율이 최저(×1)로 유지될 뿐
    /// - 100 도달 → 앙코르 아님. 100에 머물며 최고 배율(×5)을 지속
    /// - 현재 열기에 따른 점수 배율은 CurrentMultiplier / Hype.Multiplier 로 읽는다
    /// - 모든 수치는 HypeConfig 에셋에서 튜닝 (배율 테이블 포함)
    ///
    /// [다른 담당자용 API]
    ///   HypeSystem.Instance.ApplyJudgement(HypeJudgement.Perfect); // 카드 담당: 판정 결과 전달
    ///   HypeSystem.Instance.SetDecayPaused(true);                  // 연출 담당: 연출 시작/끝에 정지·재개
    ///   HypeSystem.Instance.Current;                               // 현재 호응도 (0~100)
    ///
    /// 직접 참조 없이 반응만 붙이려면 HypeEvents.cs 의 이벤트를 EventBus 로 구독하면 된다.
    /// </summary>
    public class HypeSystem : MonoSingleton<HypeSystem>
    {
        [SerializeField, Tooltip("밸런스 수치 에셋. 비워두면 기본값으로 임시 생성됨 (경고 출력)")]
        HypeConfig config;

        float _current;              // 현재 열기
        bool _decayPausedManually;   // 연출 담당이 SetDecayPaused 로 제어하는 정지 플래그

        /// <summary>씬 재시작 시 새로 초기화되도록 씬에 종속시킨다. (GameManager 처럼 DontDestroyOnLoad 하지 않음)</summary>
        protected override bool Persistent => false;

        public float Current => _current;
        public float Normalized => config == null ? 0f : Mathf.Clamp01(_current / config.maxHype);
        public HypeConfig Config => config;

        /// <summary>현재 열기에 해당하는 점수 배율 (×1~×5). 콘픽이 없으면 1.</summary>
        public float CurrentMultiplier => config == null ? 1f : config.GetMultiplier(Normalized);

        /// <summary>임의의 열기 비율(0~1)에 해당하는 점수 배율.</summary>
        public float MultiplierAt(float hype01) => config == null ? 1f : config.GetMultiplier(hype01);

        /// <summary>임의의 열기 원값(0~100)에 해당하는 점수 배율. (카드 담당이 사용)</summary>
        public float MultiplierForRaw(float rawHype)
            => config == null || config.maxHype <= 0f ? 1f : config.GetMultiplier(rawHype / config.maxHype);

        /// <summary>지금 감소가 멈춰 있는가 (연출 정지). 디버그 표시용.</summary>
        public bool IsDecayPaused => _decayPausedManually;

        protected override void OnAwake()
        {
            // 콘픽 미지정 안전장치: 에디터 메뉴(Tools/Hype)로 셋업하면 자동 지정된다
            if (config == null)
            {
                Debug.LogWarning("[HypeSystem] HypeConfig 가 지정되지 않아 기본값으로 임시 생성합니다. " +
                                 "Tools/Hype/Setup Hype Scene 을 실행하세요.");
                config = ScriptableObject.CreateInstance<HypeConfig>();
            }
            ResetHype();
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        void OnDisable() => EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연 시작(Ready → Playing)에만 초기화. Paused → Playing 재개 때는 유지된다.
            if (e.Previous == GameState.Ready && e.Current == GameState.Playing)
                ResetHype();
        }

        /// <summary>호응도를 시작값으로 되돌린다. (공연 시작 시 자동 호출)</summary>
        public void ResetHype()
        {
            _decayPausedManually = false;
            float prev = _current;
            _current = Mathf.Clamp(config.startHype, 0f, config.maxHype);
            RaiseChanged(_current - prev);
        }

        void Update()
        {
            // 플레이 중이 아니면(대기·일시정지·게임오버) 감소하지 않는다
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            // 연출 중에는 감소 정지 (판단 구간에서만 떨어져야 공정함)
            if (_decayPausedManually) return;

            Change(-config.decayPerSecond * Time.deltaTime);
        }

        // ---------------- 다른 담당자용 공개 API ----------------

        /// <summary>
        /// [카드 담당] 카드 사용 결과 판정을 넘기면 호응도가 증감된다.
        /// 증감량은 HypeConfig 에서 가져오므로 호출부에서 수치를 넣지 않는다.
        /// </summary>
        public void ApplyJudgement(HypeJudgement judgement)
        {
            ApplyDelta(config.GetDelta(judgement), judgement);
        }

        /// <summary>
        /// 카드 프리팹에 저장된 정확한 열기 변화량을 적용한다.
        /// 단계별 카드 수치는 HypeConfig의 기존 공통 판정값과 다르므로 이 경로를 사용한다.
        /// </summary>
        public void ApplyDelta(float delta, HypeJudgement judgement)
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;
            if (Mathf.Approximately(delta, 0f)) return;

            EventBus.Raise(new HypeJudgementApplied { Judgement = judgement, Delta = delta });
            Change(delta);
        }

        /// <summary>
        /// [연출 담당] 카드 연출·라운드 전환 시작 시 true, 끝나면 false 를 호출해
        /// 연출 중 호응도가 억울하게 깎이지 않게 한다.
        /// </summary>
        public void SetDecayPaused(bool paused) => _decayPausedManually = paused;

        // ---------------- 내부 로직 ----------------

        /// <summary>
        /// 열기 증감 + 경계(0~100 클램프) 처리의 단일 통로. 값 변경은 반드시 여기로 모은다.
        /// 열기는 승패 조건이 아니므로 0/100 에서 게임오버·앙코르 같은 특수 처리를 하지 않는다.
        /// (0 이면 배율이 ×1 바닥, 100 이면 ×5 로 유지될 뿐)
        /// </summary>
        void Change(float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;

            float prev = _current;
            _current = Mathf.Clamp(_current + delta, 0f, config.maxHype);
            if (!Mathf.Approximately(prev, _current)) RaiseChanged(_current - prev);
        }

        void RaiseChanged(float delta)
        {
            EventBus.Raise(new HypeChanged
            {
                Value = _current,
                Delta = delta,
                Normalized = Normalized
            });
        }
    }

    /// <summary>
    /// 어디서든 한 줄로 호응도를 읽고 쓰는 전역 접근자. (킷의 Sound.Play 와 같은 정적 파사드 패턴)
    /// HypeSystem 을 직접 참조하지 않아도 되므로 팀원 코드가 짧아진다.
    ///
    ///   float hype  = Hype.Current;             // 현재 호응도 (0~100)
    ///   float ratio = Hype.Normalized;          // 0~1 비율 (UI/연출용)
    ///   Hype.Apply(HypeJudgement.Perfect);      // 카드 판정 적용 (카드 담당)
    ///   Hype.SetDecayPaused(true);              // 연출 중 감소 정지 (연출 담당)
    /// </summary>
    public static class Hype
    {
        /// <summary>현재 호응도 (0~100). 씬에 HypeSystem 이 없으면 0. (억지로 생성하지 않는다)</summary>
        public static float Current => HypeSystem.HasInstance ? HypeSystem.Instance.Current : 0f;

        /// <summary>현재 호응도의 0~1 비율.</summary>
        public static float Normalized => HypeSystem.HasInstance ? HypeSystem.Instance.Normalized : 0f;

        /// <summary>현재 열기에 해당하는 점수 배율 (×1~×5). 시스템이 없으면 1.</summary>
        public static float Multiplier => HypeSystem.HasInstance ? HypeSystem.Instance.CurrentMultiplier : 1f;

        /// <summary>[카드 담당] 카드 낼 때의 열기 원값(0~100)에 해당하는 점수 배율.</summary>
        public static float MultiplierFor(float rawHype) => HypeSystem.HasInstance ? HypeSystem.Instance.MultiplierForRaw(rawHype) : 1f;

        /// <summary>지금 감소가 멈춰 있는가 (연출 정지 또는 앙코르 직후).</summary>
        public static bool IsDecayPaused => HypeSystem.HasInstance && HypeSystem.Instance.IsDecayPaused;

        /// <summary>[카드 담당] 판정 결과를 호응도에 적용.</summary>
        public static void Apply(HypeJudgement judgement)
        {
            if (!CheckInstance()) return;
            HypeSystem.Instance.ApplyJudgement(judgement);
        }

        /// <summary>[카드 담당] 프리팹에 저장된 정확한 열기 변화량을 적용.</summary>
        public static void ApplyDelta(float delta, HypeJudgement judgement)
        {
            if (!CheckInstance()) return;
            HypeSystem.Instance.ApplyDelta(delta, judgement);
        }

        /// <summary>[연출 담당] 연출 시작(true)/종료(false) 시 감소 정지·재개.</summary>
        public static void SetDecayPaused(bool paused)
        {
            if (!CheckInstance()) return;
            HypeSystem.Instance.SetDecayPaused(paused);
        }

        static bool CheckInstance()
        {
            if (HypeSystem.HasInstance) return true;
            Debug.LogWarning("[Hype] 씬에 HypeSystem 이 없습니다. Tools/Hype/Setup Hype Scene 을 실행했는지 확인하세요.");
            return false;
        }
    }
}
