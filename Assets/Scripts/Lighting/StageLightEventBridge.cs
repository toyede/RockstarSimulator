using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>무대 조명 프리셋을 무엇으로 고를지.</summary>
    public enum StageLightPresetSource
    {
        [InspectorName("관객 최다 성향")] CrowdMajority,
        [InspectorName("열기 단계")] Hype,
    }

    /// <summary>
    /// 관객 명단·열기·특별 관객 시스템과 무대 조명을 잇는 어댑터.
    ///
    /// 어느 시스템도 수정하지 않는다. 이미 발행 중인 EventBus 이벤트만 구독한다:
    ///   AudienceSummaryChanged → 객석 최다 성향으로 조명 프리셋 결정 (기본 동작)
    ///   HypeChanged            → 열기 단계로 결정 (presetSource = Hype 일 때, 또는 관객이 없을 때 폴백)
    ///   SpecialHitLanded       → 요청 타입 색으로 플래시
    ///   GameStateChanged       → 공연 시작·초기화 시 현재 값으로 즉시 맞춤
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

        [Header("프리셋 결정 방식")]
        [SerializeField, Tooltip(
            "관객 최다 성향: 객석에서 가장 많은 성향의 색으로 무대를 비춘다. " +
            "열기 단계: 예전처럼 열기 수치로 결정한다.")]
        StageLightPresetSource presetSource = StageLightPresetSource.CrowdMajority;

        [SerializeField, Tooltip(
            "동점일 때 고르는 우선순위. 단, 현재 적용 중인 성향이 최다에 포함되면 " +
            "그대로 유지해서 조명이 깜빡이지 않게 한다.")]
        List<CrowdPreference> tieBreakPriority = new List<CrowdPreference>
        {
            CrowdPreference.Mosh,
            CrowdPreference.Singalong,
            CrowdPreference.Chill,
        };

        [Header("특별 관객")]
        [SerializeField, Tooltip("Special Hit 플래시에 요청 타입 색을 쓸지. 끄면 흰색으로 번쩍인다")]
        bool useRequestColorForFlash = true;

        /// <summary>마지막으로 조명에 반영한 단계. 매 프레임 같은 값을 다시 넣지 않기 위한 캐시.</summary>
        HeatStage _lastStage = HeatStage.Chill;
        bool _hasStage;

        /// <summary>마지막으로 채택한 최다 성향. 동점일 때 이 값을 유지해 조명 진동을 막는다.</summary>
        CrowdPreference _lastMajority = CrowdPreference.Chill;

        bool _warnedMissingConfig;
        bool _warnedMissingRoster;

        void Awake()
        {
            if (controller == null) controller = GetComponent<StageLightController>();
        }

        void OnEnable()
        {
            EventBus.Subscribe<AudienceSummaryChanged>(OnAudienceSummaryChanged);
            EventBus.Subscribe<HypeChanged>(OnHypeChanged);
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

            SyncStage(instant: true); // 늦게 켜져도 현재 상태에 맞춰 시작한다
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AudienceSummaryChanged>(OnAudienceSummaryChanged);
            EventBus.Unsubscribe<HypeChanged>(OnHypeChanged);
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        // ---------------- 관객 명단 ----------------

        /// <summary>
        /// 관객이 들어오거나 나가거나 상태가 바뀔 때마다 로스터가 발행한다.
        /// 요약에는 성향별 인원이 없으므로 트리거로만 쓰고, 인원은 명단에서 직접 센다.
        /// (관객 수가 10명 안팎이라 매번 세도 부담이 없다)
        /// </summary>
        void OnAudienceSummaryChanged(AudienceSummaryChanged e)
        {
            if (presetSource != StageLightPresetSource.CrowdMajority) return;
            if (TryResolveFromRoster(out HeatStage stage))
                ApplyStage(stage, instant: false);
        }

        /// <summary>
        /// 관객 명단 → 조명 단계. 최다 성향 판정은 CrowdMajorityResolver 하나만 쓰므로
        /// 다른 연출이 같은 규칙을 필요로 할 때 그대로 재사용할 수 있다.
        /// </summary>
        bool TryResolveFromRoster(out HeatStage stage)
        {
            if (!CrowdMajorityResolver.TryResolveMajority(
                    AudienceRoster.Members, tieBreakPriority, _lastMajority,
                    out CrowdPreference majority))
            {
                stage = default;
                return false;
            }

            _lastMajority = majority;
            stage = majority.ToHeatStage();
            return true;
        }

        // ---------------- 열기 ----------------

        void OnHypeChanged(HypeChanged e)
        {
            if (UsesCrowdMajority()) return; // 관객 구성이 주도할 때는 열기로 색을 바꾸지 않는다
            if (TryResolveStage(e.Value, out HeatStage stage))
                ApplyStage(stage, instant: false);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연 준비/시작에는 보간 없이 바로 맞춘다 (이전 판의 색이 남지 않게)
            if (e.Current == GameState.Ready || e.Current == GameState.Playing)
                SyncStage(instant: e.Current == GameState.Ready);
        }

        void SyncStage(bool instant)
        {
            if (UsesCrowdMajority() && TryResolveFromRoster(out HeatStage crowdStage))
            {
                ApplyStage(crowdStage, instant);
                return;
            }
            // 관객 수가 0이면(아직 초기화 전) 열기로 대신 맞춘다.
            // 초기화가 끝나면 AudienceSummaryChanged 가 곧바로 들어온다.

            if (!HypeSystem.HasInstance || HypeSystem.Instance.Config == null)
            {
                WarnMissingConfig();
                return;
            }

            HypeSystem hype = HypeSystem.Instance;
            ApplyStage(hype.Config.ResolveStage(hype.Current), instant);
        }

        /// <summary>
        /// 관객 최다 성향 방식이 실제로 동작 가능한지. 로스터가 없으면 열기 방식으로 폴백한다.
        /// (조명이 아예 멈추는 것보다 예전 동작으로 돌아가는 편이 낫다)
        /// </summary>
        bool UsesCrowdMajority()
        {
            if (presetSource != StageLightPresetSource.CrowdMajority) return false;
            if (AudienceRoster.Exists) return true;

            WarnMissingRoster();
            return false;
        }

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
        bool TryResolveStage(float rawHype, out HeatStage stage)
        {
            var config = HypeSystem.HasInstance ? HypeSystem.Instance.Config : null;
            if (config != null)
            {
                stage = config.ResolveStage(rawHype);
                return true;
            }

            WarnMissingConfig();
            stage = default;
            return false;
        }

        void WarnMissingConfig()
        {
            if (_warnedMissingConfig) return;

            _warnedMissingConfig = true;
            Debug.LogError(
                "[StageLightEventBridge] A configured HypeSystem is required.",
                this);
        }

        void WarnMissingRoster()
        {
            if (_warnedMissingRoster) return;

            _warnedMissingRoster = true;
            Debug.LogWarning(
                "[StageLightEventBridge] 씬에 AudienceRosterSystem 이 없어 열기 단계로 폴백합니다. " +
                "AudienceRuntime 프리팹이 배치돼 있는지 확인하세요.",
                this);
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
