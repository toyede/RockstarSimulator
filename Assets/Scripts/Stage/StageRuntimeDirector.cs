using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 현재 StageDefinition 을 Main 씬에 적용한다. (기획서 §10 StageRuntimeDirector)
    ///
    /// 책임은 다음으로 제한한다:
    ///   1. 현재 스테이지 조회 (투어 진행 중이면 TourRunManager, 아니면 fallbackStage)
    ///   2. 관객 프리셋 적용 → AudienceRosterSystem.ConfigureForStage
    ///   3. 기본 상태(특별 관객 OFF · 위기 OFF)로 되돌린 뒤 venueRuleIds 의 룰만 활성화
    ///   4. 공연 종료(GameOver) 시 활성 룰 전부 해제, 재시작(Ready) 시 다시 적용
    ///   5. StageRuntimeApplied 이벤트 발행 (배경·조명 등 표현 담당이 구독)
    ///
    /// 점수·카드 판정·증강 효과는 건드리지 않는다.
    /// 실행 순서 100: 관객 시스템의 Awake(모델 생성) 뒤, CardSystem.Start(공연 시작) 앞에 적용된다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class StageRuntimeDirector : MonoBehaviour
    {
        [Header("스테이지")]
        [SerializeField, Tooltip("투어 없이 Main 을 직접 실행할 때 적용할 스테이지. 비우면 씬 기본값(게임잼 버전)으로 동작")]
        StageDefinition fallbackStage;

        [Header("연결 (비워두면 씬에서 찾는다)")]
        [SerializeField] AudienceRosterSystem audienceRoster;
        [SerializeField] SpecialAudienceManager specialAudience;
        [SerializeField] NearbyConcertCrisisDirector crisisDirector;

        [SerializeField, Tooltip("이 디렉터가 켜고 끌 룰. 비워두면 자식에서 전부 모은다")]
        List<StageRuleBehaviour> rules = new List<StageRuleBehaviour>();

        [Header("디버그")]
        [SerializeField] bool verboseLogs = true;

        readonly List<StageRuleBehaviour> _activeRules = new List<StageRuleBehaviour>();

        /// <summary>씬에서 동작 중인 디렉터. 배경 뷰 등이 현재 스테이지를 읽을 때 쓴다.</summary>
        public static StageRuntimeDirector Active { get; private set; }

        /// <summary>현재 적용된 스테이지. 없으면 null (씬 기본값으로 진행 중).</summary>
        public static StageDefinition CurrentStage => Active != null ? Active.Stage : null;

        public StageDefinition Stage { get; private set; }
        public AudienceStagePreset Preset { get; private set; }
        public IReadOnlyList<StageRuleBehaviour> ActiveRules => _activeRules;
        public bool HasStage => Stage != null;

        /// <summary>
        /// 룰이 클리어 판정을 대신 정할 때 쓴다 (보스전: 체력 0 = 클리어, 시간 초과 = 실패).
        /// null 이면 TourPerformanceBridge 가 기존 타이머 판정(목표 점수)을 쓴다. Ready·재적용 때 null 로 돌아간다.
        /// </summary>
        public bool? ClearVerdictOverride { get; set; }

        void Awake()
        {
            Active = this;
            ResolveReferences();

            StageDefinition stage = ResolveStage();
            if (stage == null)
            {
                Log("적용할 스테이지가 없습니다. 씬 기본 설정으로 진행합니다.");
                return;
            }

            ApplyStage(stage);
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            DeactivateAllRules();
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        // ---------------- 적용 ----------------

        /// <summary>스테이지를 명시적으로 적용한다. (디버그·재적용용. 평소에는 Awake 가 한 번 호출)</summary>
        public void ApplyStage(StageDefinition stage)
        {
            if (stage == null) return;

            DeactivateAllRules();
            Stage = stage;
            Preset = null;
            ClearVerdictOverride = null;

            ApplyAudiencePreset(stage);
            ApplyBaseline();
            ActivateRules(stage);

            EventBus.Raise(new StageRuntimeApplied(stage, Preset));
            Log($"'{stage.DisplayName}' 적용 완료. 관객 프리셋={(Preset != null ? Preset.PresetId : "없음")}, 룰={_activeRules.Count}개");
        }

        void ApplyAudiencePreset(StageDefinition stage)
        {
            string presetId = stage.AudiencePresetId;
            if (string.IsNullOrWhiteSpace(presetId)) return;

            StageContentCatalog catalog = StageContentCatalog.LoadDefault();
            if (catalog == null)
            {
                Debug.LogWarning(
                    $"[StageRuntime] Resources/{StageContentCatalog.ResourcesPath} 가 없어 관객 프리셋 '{presetId}' 을 적용하지 못했습니다. " +
                    "Tools/Tour/Setup Stage Runtime 을 실행하세요.",
                    this);
                return;
            }

            if (!catalog.TryGetAudiencePreset(presetId, out AudienceStagePreset preset))
            {
                Debug.LogWarning($"[StageRuntime] 관객 프리셋 '{presetId}' 을 카탈로그에서 찾지 못했습니다.", catalog);
                return;
            }

            if (audienceRoster == null)
            {
                Debug.LogWarning("[StageRuntime] AudienceRosterSystem 이 없어 관객 프리셋을 적용하지 못했습니다.", this);
                return;
            }

            if (!audienceRoster.ConfigureForStage(preset)) return;
            Preset = preset;
        }

        /// <summary>룰이 켜기 전의 기본 상태. Stage 1 처럼 룰이 없는 스테이지는 이 상태로 진행된다.</summary>
        void ApplyBaseline()
        {
            if (specialAudience != null)
            {
                specialAudience.SetAutoStart(false);
                specialAudience.SetRuntimeConfig(null);
                specialAudience.StopSystem();
            }

            if (crisisDirector != null)
                crisisDirector.ConfigureForStage(null, false);
        }

        void ActivateRules(StageDefinition stage)
        {
            IReadOnlyList<string> ids = stage.VenueRuleIds;
            if (ids == null || ids.Count == 0) return;

            StageRuleContext context = BuildContext(stage);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (string.IsNullOrWhiteSpace(id)) continue;

                StageRuleBehaviour rule = FindRule(id);
                if (rule == null)
                {
                    Debug.LogWarning($"[StageRuntime] 룰 '{id}' 컴포넌트를 찾지 못했습니다. (StageDefinition: {stage.name})", this);
                    continue;
                }

                if (_activeRules.Contains(rule)) continue;
                rule.Activate(context);
                _activeRules.Add(rule);
                Log($"룰 활성화: {id}");
            }
        }

        void DeactivateAllRules()
        {
            for (int i = _activeRules.Count - 1; i >= 0; i--)
            {
                StageRuleBehaviour rule = _activeRules[i];
                if (rule != null) rule.Deactivate();
            }
            _activeRules.Clear();
        }

        StageRuleContext BuildContext(StageDefinition stage)
        {
            int seed = TourRunManager.HasInstance && TourRunManager.Instance.CurrentRun != null
                ? TourRunManager.Instance.CurrentRun.seed
                : Environment.TickCount & int.MaxValue;

            return new StageRuleContext(
                stage,
                Preset,
                audienceRoster,
                PerformanceTimerSystem.HasInstance ? PerformanceTimerSystem.Instance : null,
                specialAudience,
                crisisDirector,
                seed);
        }

        // ---------------- 상태 전이 ----------------

        void OnGameStateChanged(GameStateChanged e)
        {
            if (Stage == null) return;

            if (e.Current == GameState.GameOver)
            {
                DeactivateAllRules();
            }
            else if (e.Current == GameState.Ready)
            {
                // 씬 재로드 없이 다시 시작하는 경우(튜토리얼 종료 등): 룰 내부 상태를 새로 잡는다.
                // 관객 로스터는 스스로 Ready 에서 스테이지 모델로 초기화한다.
                DeactivateAllRules();
                ClearVerdictOverride = null;
                ApplyBaseline();
                ActivateRules(Stage);
            }
        }

        // ---------------- 조회 ----------------

        StageDefinition ResolveStage()
        {
            if (TourRunManager.HasInstance)
            {
                TourRunManager manager = TourRunManager.Instance;
                if (manager.CurrentRun != null && manager.CurrentRun.phase == RunPhase.Performance)
                {
                    StageDefinition stage = manager.CurrentStageDefinition;
                    if (stage != null) return stage;
                }
            }

            return fallbackStage;
        }

        StageRuleBehaviour FindRule(string id)
        {
            EnsureRuleList();
            for (int i = 0; i < rules.Count; i++)
            {
                StageRuleBehaviour rule = rules[i];
                if (rule != null && string.Equals(rule.RuleId, id, StringComparison.Ordinal))
                    return rule;
            }
            return null;
        }

        void EnsureRuleList()
        {
            if (rules != null && rules.Count > 0) return;
            rules = new List<StageRuleBehaviour>(GetComponentsInChildren<StageRuleBehaviour>(true));
        }

        void ResolveReferences()
        {
            if (audienceRoster == null && AudienceRosterSystem.HasInstance)
                audienceRoster = AudienceRosterSystem.Instance;
            if (audienceRoster == null)
                audienceRoster = FindFirstObjectByType<AudienceRosterSystem>(FindObjectsInactive.Include);

            if (specialAudience == null && SpecialAudienceManager.HasInstance)
                specialAudience = SpecialAudienceManager.Instance;
            if (specialAudience == null)
                specialAudience = FindFirstObjectByType<SpecialAudienceManager>(FindObjectsInactive.Include);

            if (crisisDirector == null)
                crisisDirector = FindFirstObjectByType<NearbyConcertCrisisDirector>(FindObjectsInactive.Include);

            EnsureRuleList();
        }

        void Log(string message)
        {
            if (verboseLogs) Debug.Log($"[StageRuntime] {message}", this);
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(
            AudienceRosterSystem roster,
            SpecialAudienceManager special,
            NearbyConcertCrisisDirector crisis,
            List<StageRuleBehaviour> ruleList)
        {
            audienceRoster = roster;
            specialAudience = special;
            crisisDirector = crisis;
            rules = ruleList ?? new List<StageRuleBehaviour>();
        }

        /// <summary>[에디터 셋업 전용] 룰 하나를 목록에 추가한다 (이미 있으면 무시).</summary>
        public void EditorAddRule(StageRuleBehaviour rule)
        {
            if (rule == null) return;
            if (rules == null) rules = new List<StageRuleBehaviour>();
            if (!rules.Contains(rule)) rules.Add(rule);
        }
#endif
    }
}
