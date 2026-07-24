using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 열기·특별 관객 시스템과 무대 조명을 잇는 어댑터.
    ///
    /// 두 시스템 어느 쪽도 수정하지 않는다. 이미 발행 중인 EventBus 이벤트만 구독한다:
    ///   HypeChanged     → HypeConfig.ResolveStage() 로 단계를 계산해 바뀌었을 때만 조명 갱신
    ///   SpecialHitLanded → 요청 타입 색으로 플래시
    ///   GameStateChanged → 공연 시작·초기화 시 현재 단계로 즉시 맞춤
    ///
    /// 조명 로직을 전혀 모르기 때문에, 연출 방식이 바뀌어도 이 파일은 그대로다.
    /// 반대로 조명만 단독으로 테스트하고 싶으면 이 컴포넌트를 꺼두면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StageLightController))]
    public sealed class StageLightEventBridge : MonoBehaviour
    {
        [SerializeField, Tooltip("비워두면 같은 오브젝트에서 찾는다")]
        StageLightController controller;

        [SerializeField, Tooltip("Special Hit 플래시에 요청 타입 색을 쓸지. 끄면 흰색으로 번쩍인다")]
        bool useRequestColorForFlash = true;

        /// <summary>마지막으로 조명에 반영한 단계. 매 프레임 같은 값을 다시 넣지 않기 위한 캐시.</summary>
        HeatStage _lastStage = HeatStage.Chill;
        bool _hasStage;

        void Awake()
        {
            if (controller == null) controller = GetComponent<StageLightController>();
        }

        void OnEnable()
        {
            EventBus.Subscribe<HypeChanged>(OnHypeChanged);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

            SyncStage(instant: true); // 늦게 켜져도 현재 열기에 맞춰 시작한다
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<HypeChanged>(OnHypeChanged);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        // ---------------- 열기 ----------------

        void OnHypeChanged(HypeChanged e) => ApplyStage(ResolveStage(e.Value), instant: false);

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연 준비/시작에는 보간 없이 바로 맞춘다 (이전 판의 색이 남지 않게)
            if (e.Current == GameState.Ready || e.Current == GameState.Playing)
                SyncStage(instant: e.Current == GameState.Ready);
        }

        void SyncStage(bool instant) => ApplyStage(ResolveStage(Hype.Current), instant);

        void ApplyStage(HeatStage stage, bool instant)
        {
            if (controller == null) return;
            if (_hasStage && stage == _lastStage && !instant) return; // 단계가 그대로면 건드리지 않는다

            _lastStage = stage;
            _hasStage = true;
            controller.SetHeatStage(stage, instant);
        }

        /// <summary>
        /// 열기 수치 → 단계. 카드 판정과 <b>같은 함수</b>(HypeConfig.ResolveStage)를 쓰므로
        /// 조명이 보여주는 단계와 카드가 판정하는 단계가 어긋날 수 없다.
        /// </summary>
        static HeatStage ResolveStage(float rawHype)
        {
            var config = HypeSystem.HasInstance ? HypeSystem.Instance.Config : null;
            if (config != null) return config.ResolveStage(rawHype);

            // 콘픽이 없을 때의 안전한 기본값 (CardEffectResolver 의 폴백과 동일한 경계)
            if (rawHype >= 80f) return HeatStage.Mosh;
            if (rawHype >= 50f) return HeatStage.Singalong;
            return HeatStage.Chill;
        }

        // ---------------- 특별 관객 ----------------

        void OnSpecialHit(SpecialHitLanded e)
        {
            if (controller == null) return;

            // 조명만 건드린다. 점수·열기는 각 담당이 이미 처리했다.
            if (useRequestColorForFlash) controller.PlaySpecialHitFlash(e.RequestType);
            else controller.PlaySpecialHitFlash();
        }
    }
}
