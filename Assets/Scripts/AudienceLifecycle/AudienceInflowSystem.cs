using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 관객 "일반 신규 유입"과 "이탈 처리"를 담당한다.
    ///
    /// 개인별 몰입도(0~100) 계산과 카드 반응값은 이 시스템의 책임이 아니다 (별도 담당자가
    /// CrowdComposition 쪽에서 개편 중). 이 시스템은 그 작업과 무관하게:
    ///   1) 공연 시작 시 초기 관객을 유입시키고, 이후 주기적으로 확률 판정해 신규 관객을 유입시키며
    ///   2) 몰입도 담당 코드가 "이 관객 몰입도 0" 이라고 알려오면(RequestExit) 실제 퇴장 처리를 한다.
    ///
    /// 시각 스폰은 새 프리팹/좌표 시스템을 만들지 않고 기존 <see cref="CrowdSpawner"/> 에 추가한
    /// SpawnAdditional/DespawnAdditional API 를 그대로 재사용한다 (CrowdCompositionManager 기반
    /// 프리셋 그리드와는 별개 목록으로 동작하므로 서로 간섭하지 않는다).
    ///
    /// 개인 몰입도 시스템이 아직 없으므로, 인원수는 이 시스템이 자체적으로 임시 로스터
    /// (_activeMembers)로 들고 있는다. 몰입도 시스템이 완성되면 그쪽이 진짜 데이터 소스가 되고
    /// 이 임시 로스터는 걷어낼 예정 — 그때까지는 AudienceMemberSpawned/Exited 이벤트의 MemberId 를
    /// 몰입도 담당 코드가 키로 그대로 재사용하면 된다.
    /// </summary>
    public class AudienceInflowSystem : MonoSingleton<AudienceInflowSystem>
    {
        [Header("설정")]
        [SerializeField] AudienceLifecycleConfig config;

        [Header("스폰 연동")]
        [Tooltip("씬에 이미 배치된 CrowdSpawner. 프리팹/좌표 배치를 새로 만들지 않고 " +
                 "SpawnAdditional/DespawnAdditional API로 그대로 재사용한다")]
        [SerializeField] CrowdSpawner crowdSpawner;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Prototype Debug")]
        [SerializeField] bool enableDebugInput = true;
#endif

        readonly Dictionary<int, CrowdMemberView> _activeMembers = new Dictionary<int, CrowdMemberView>();
        int _nextMemberId = 1;
        Coroutine _inflowRoutine;

        /// <summary>임시 로스터 기준 현재 관객 수. (팀원 몰입도 시스템 통합 전까지의 잠정 소스)</summary>
        public int Count => _activeMembers.Count;
        public int MaxCount => config != null ? config.maxCount : 0;

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            this.Cancel(ref _inflowRoutine);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Playing && e.Previous == GameState.Ready) StartRun();
            else if (e.Current == GameState.Ready) StopRun();
        }

        void StartRun()
        {
            if (config == null)
            {
                Debug.LogError("[AudienceInflowSystem] AudienceLifecycleConfig 가 필요합니다.", this);
                return;
            }
            if (crowdSpawner == null)
            {
                Debug.LogError("[AudienceInflowSystem] CrowdSpawner 참조가 필요합니다.", this);
                return;
            }

            ClearAll();
            _nextMemberId = 1;

            for (int i = 0; i < config.initialCount; i++) SpawnMember();

            this.Cancel(ref _inflowRoutine);
            _inflowRoutine = this.Repeat(config.inflowCheckInterval, TryInflow);
        }

        void StopRun()
        {
            this.Cancel(ref _inflowRoutine);
            ClearAll();
        }

        void ClearAll()
        {
            if (crowdSpawner != null)
            {
                foreach (var pair in _activeMembers)
                    crowdSpawner.DespawnAdditional(pair.Value);
            }
            _activeMembers.Clear();
        }

        // ---------------- 신규 유입 ----------------

        /// <summary>기획 4.2: 판정 주기마다 확률로 유입 여부를 정한다. 최대 인원이면 발생하지 않는다.</summary>
        void TryInflow()
        {
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;
            if (_activeMembers.Count >= config.maxCount) return;
            if (Random.value > config.inflowProbability) return;

            SpawnMember();
        }

        bool SpawnMember()
        {
            if (config == null || crowdSpawner == null || _activeMembers.Count >= config.maxCount) return false;

            int memberId = _nextMemberId++;
            var preference = (CrowdPreference)Random.Range(0, 3);
            float immersion = config.RollSpawnImmersion();

            // 개인 몰입도 시스템은 이 이벤트를 구독해 memberId 를 키로 자기 데이터를 만들면 된다.
            EventBus.Raise(new AudienceMemberSpawned
            {
                MemberId = memberId,
                Preference = preference,
                InitialImmersion = immersion
            });

            // 시각 스폰은 새로 만들지 않고 기존 CrowdSpawner 의 추가 전용 API 를 재사용한다.
            CrowdMemberView view = crowdSpawner.SpawnAdditional(preference);
            if (view != null) _activeMembers[memberId] = view;

            EventBus.Raise(new AudienceCountChanged { Count = _activeMembers.Count, MaxCount = config.maxCount });
            return true;
        }

        // ---------------- 이탈 ----------------

        /// <summary>
        /// 몰입도가 0이 되었을 때 호출하는 이탈 처리 진입점.
        /// 개인 몰입도 시스템 완성 후에는: <c>AudienceLifecycle.RequestExit(memberId, AudienceExitReason.NaturalDecay)</c> 한 줄만 호출하면 된다.
        /// </summary>
        public void Exit(int memberId, AudienceExitReason reason)
        {
            if (!_activeMembers.TryGetValue(memberId, out CrowdMemberView view)) return;

            _activeMembers.Remove(memberId);
            if (crowdSpawner != null) crowdSpawner.DespawnAdditional(view);

            EventBus.Raise(new AudienceMemberExited { MemberId = memberId, Reason = reason });
            EventBus.Raise(new AudienceCountChanged { Count = _activeMembers.Count, MaxCount = config != null ? config.maxCount : 0 });

            // 기획 4.3: 관객이 한 명도 남지 않으면 곡이 끝나기 전이라도 즉시 게임 오버.
            if (_activeMembers.Count == 0 && GameManager.HasInstance && GameManager.Instance.IsPlaying)
                GameManager.Instance.GameOver();
        }

        // ---------------- 디버그 ----------------

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        void Update()
        {
            if (!enableDebugInput) return;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f5Key.wasPressedThisFrame) DebugForceInflow();
            else if (keyboard.f6Key.wasPressedThisFrame) DebugForceExit();
#else
            if (Input.GetKeyDown(KeyCode.F5)) DebugForceInflow();
            else if (Input.GetKeyDown(KeyCode.F6)) DebugForceExit();
#endif
        }

        void DebugForceInflow()
        {
            bool spawned = SpawnMember();
            Debug.Log($"[AudienceInflowSystem] F5 강제 유입 {(spawned ? "성공" : "실패(최대 인원)")}. 현재 {Count}/{MaxCount}", this);
        }

        void DebugForceExit()
        {
            foreach (var pair in _activeMembers)
            {
                Exit(pair.Key, AudienceExitReason.NaturalDecay);
                Debug.Log($"[AudienceInflowSystem] F6 강제 이탈: memberId={pair.Key}. 현재 {Count}/{MaxCount}", this);
                return;
            }
            Debug.Log("[AudienceInflowSystem] F6 강제 이탈: 남은 관객 없음.", this);
        }
#endif
    }

    /// <summary>
    /// 전역 정적 파사드. 개인 몰입도 시스템은 이 클래스만 참조하면 되고,
    /// AudienceInflowSystem 의 내부 구현(임시 로스터 등)은 몰라도 된다.
    /// </summary>
    public static class AudienceLifecycle
    {
        public static int CurrentCount => AudienceInflowSystem.HasInstance ? AudienceInflowSystem.Instance.Count : 0;
        public static int MaxCount => AudienceInflowSystem.HasInstance ? AudienceInflowSystem.Instance.MaxCount : 0;

        /// <summary>몰입도가 0이 된 관객을 퇴장시킨다. 개인 몰입도 시스템의 유일한 연동 지점.</summary>
        public static void RequestExit(int memberId, AudienceExitReason reason) =>
            AudienceInflowSystem.Instance.Exit(memberId, reason);
    }
}
