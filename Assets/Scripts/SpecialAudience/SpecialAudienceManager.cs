using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 특별 관객 시스템. 카드·점수·열기 시스템과 완전히 독립적으로 동작한다.
    ///
    /// - 공연이 시작되면 설정된 대기시간 뒤 특별 관객이 등장하고, 설정 간격마다 반복
    /// - 요구 타입은 셔플 백(Chill/Singalong/Mosh)에서 하나씩 꺼내 뽑는다
    /// - 설정된 요구시간 안에 대응하지 못하면 <b>패널티 없이</b> 사라진다
    /// - P 키(디버그)로 즉시 등장·교체할 수 있다
    ///
    /// [카드 담당 연결 지점]
    ///   카드가 CardDefinition의 보상을 적용한 뒤 ConsumeRequest로 요청을 소비한다.
    ///
    /// [점수·열기 담당]
    ///   EventBus.Subscribe&lt;SpecialHitLanded&gt;(e => ...); // 보상만 받아서 적용
    ///
    /// 이 클래스는 점수·열기에 <b>직접 손대지 않는다.</b> 보상은 데이터로만 전달한다.
    /// 타이머는 코루틴 대신 Update + Time.time 마감시각 비교로 처리한다 (HypeSystem 과 동일).
    /// Disable·씬 재시작 시 코루틴이 남아 중복 실행되는 문제를 원천적으로 없애기 위함.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialAudienceManager : MonoSingleton<SpecialAudienceManager>
    {
        enum Phase
        {
            Stopped,   // 시스템 정지 (공연 전·종료 후)
            Waiting,   // 다음 등장 대기 중
            Active,    // 특별 관객이 요구 중
            HitHold    // Special Hit 연출 유지 중
        }

        [Header("설정")]
        [SerializeField, Tooltip("필수 등장 주기·요구 시간·연출 시간 설정")]
        SpecialAudienceConfig config;

        [SerializeField, Tooltip("공연 시작(Ready → Playing)에 시스템을 자동으로 켤지")]
        bool autoStart = true;

        [Header("View")]
        [SerializeField, Tooltip("표시 담당. 비워두면 로직만 동작한다 (null-safe)")]
        SpecialAudienceView view;

        [Header("디버그")]
        [SerializeField, Tooltip("이 키를 누르면 특별 관객이 즉시 등장한다 (에디터·개발 빌드 전용)")]
        KeyCode debugSpawnKey = KeyCode.P;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField, Tooltip("디버그 키 입력을 받을지")]
        bool enableDebugKey = true;

        [SerializeField, Tooltip("콘솔 로그 출력 여부")]
        bool verboseLogs = true;
#endif

        // ---------------- 상태 ----------------

        Phase _phase = Phase.Stopped;
        float _phaseDeadline;                 // 현재 페이즈가 끝나는 시각 (Time.time 기준)
        HeatStage _currentRequest;
        bool _hitRaisedForCurrentRequest;     // 한 요청당 Special Hit 이벤트 1회 보장
        float _lastReportedRemaining = -1f;   // 같은 값 중복 통지 방지

        /// <summary>셔플 백. 매 등장마다 새 리스트를 만들지 않고 재사용한다.</summary>
        readonly List<HeatStage> _bag = new List<HeatStage>(3);

        /// <summary>씬 재시작 시 새로 초기화되도록 씬에 종속시킨다. (HypeSystem·CardSystem 과 동일)</summary>
        protected override bool Persistent => false;

        // ---------------- 공개 상태 ----------------

        public bool HasActiveRequest => _phase == Phase.Active;
        public HeatStage CurrentRequestType => _currentRequest;
        public SpecialAudienceConfig Config => config;
        public float RequestDuration => config != null ? config.RequestDuration : 0f;

        public float RemainingTime =>
            _phase == Phase.Active ? Mathf.Max(0f, _phaseDeadline - Time.time) : 0f;

        /// <summary>현재 남은 시간의 0~1 비율. 게이지용.</summary>
        public float RemainingNormalized =>
            RequestDuration > 0f ? Mathf.Clamp01(RemainingTime / RequestDuration) : 0f;

        public bool IsRunning => _phase != Phase.Stopped;

        /// <summary>
        /// 지금 카드를 받을 수 있는 드롭 영역. 없거나 요구가 없으면 null.
        /// 특별 관객은 동시에 한 명이라 싱글턴을 새로 만들지 않고 이 프로퍼티로 노출한다.
        /// </summary>
        public SpecialAudienceDropTarget CurrentDropTarget { get; private set; }

        /// <summary>
        /// true 면 <b>드롭 영역 위에 놓았을 때만</b> Special Hit 가 성립한다.
        /// false 면 예전처럼 요구가 살아 있는 동안 아무 곳에 내도 성립한다(키보드 사용 등).
        /// </summary>
        [Header("드롭 판정")]
        [SerializeField, Tooltip("특별 관객 위에 카드를 놓아야만 Special Hit 로 인정")]
        bool requireDropOnTarget = true;

        public bool RequireDropOnTarget => requireDropOnTarget;

        /// <summary>드롭 영역이 지금 포인터를 물고 있는가. (없으면 false)</summary>
        public bool IsPointerOverDropTarget =>
            CurrentDropTarget != null && CurrentDropTarget.IsPointerOver;

        /// <summary>
        /// 드롭 위치 게이트를 적용해야 하는가.
        ///
        /// requireDropOnTarget이 켜져 있으면 타깃 누락도 실패로 처리한다.
        /// 셋업 오류를 이유로 어디서나 Special Hit가 되는 fail-open 동작을 허용하지 않는다.
        /// </summary>
        public bool ShouldGateByDropTarget
        {
            get
            {
                if (!requireDropOnTarget) return false;
                if (CurrentDropTarget == null) WarnNoDropTargetOnce();
                return true;
            }
        }

        bool _warnedNoDropTarget;
        bool _warnedMissingConfig;

        bool EnsureConfigured()
        {
            if (config != null) return true;
            if (!_warnedMissingConfig)
            {
                _warnedMissingConfig = true;
                Debug.LogError(
                    "[SpecialAudience] SpecialAudienceConfig 참조가 없습니다. " +
                    "Tools/Special Audience/Setup Special Audience를 실행하세요.",
                    this);
            }

            enabled = false;
            view?.Hide(instant: true);
            return false;
        }

        void WarnNoDropTargetOnce()
        {
            if (_warnedNoDropTarget) return;
            _warnedNoDropTarget = true;
            Debug.LogWarning(
                "[SpecialAudience] 등록된 DropTarget이 없어 Special Hit를 차단합니다. " +
                "씬 또는 프리팹에 SpecialAudienceDropTarget을 명시적으로 배치하세요.",
                this);
        }

        /// <summary>DropTarget 이 스스로 등록한다. (씬에 한 개만 있다고 가정)</summary>
        public void RegisterDropTarget(SpecialAudienceDropTarget target)
        {
            if (target == null) return;
            CurrentDropTarget = target;
            _warnedNoDropTarget = false;
        }

        public void UnregisterDropTarget(SpecialAudienceDropTarget target)
        {
            if (CurrentDropTarget == target) CurrentDropTarget = null;
        }

        /// <summary>드롭 순간의 확정 좌표로 현재 특별 관객 요청을 판정한다.</summary>
        public SpecialCardRequest ResolveDropRequest(Vector2 screenPosition, float radiusPixels)
        {
            if (!HasActiveRequest) return SpecialCardRequest.None;
            if (!requireDropOnTarget)
                return new SpecialCardRequest(CurrentRequestType);

            if (CurrentDropTarget == null)
            {
                WarnNoDropTargetOnce();
                return SpecialCardRequest.None;
            }

            return CurrentDropTarget.ContainsScreenCircle(
                screenPosition,
                Mathf.Max(0f, radiusPixels))
                ? new SpecialCardRequest(CurrentRequestType)
                : SpecialCardRequest.None;
        }

        // ---------------- 이벤트 ----------------

        /// <summary>(요구 타입, 전체 제한시간)</summary>
        public event Action<HeatStage, float> OnSpecialAudienceSpawned;

        /// <summary>(남은 시간, 전체 제한시간) — 요구 중 매 프레임</summary>
        public event Action<float, float> OnRequestTimeChanged;

        /// <summary>(요구 타입) — 제한시간 초과로 사라짐</summary>
        public event Action<HeatStage> OnSpecialAudienceExpired;

        /// <summary>(요구 타입, 종료 사유) — 모든 종료를 하나로 받고 싶을 때</summary>
        public event Action<HeatStage, SpecialAudienceEndReason> OnSpecialAudienceEnded;

        /// <summary>(요구 타입, 보상) — 한 요청당 정확히 1회</summary>
        public event Action<HeatStage, SpecialHitReward> OnSpecialHit;

        // ---------------- 수명 주기 ----------------

        protected override void OnAwake()
        {
            if (view == null) view = GetComponentInChildren<SpecialAudienceView>(true);
            if (!EnsureConfigured()) return;
            RefillBag();
        }

        void Start()
        {
            if (!EnsureConfigured()) return;
            // 이미 공연이 진행 중인 상태에서 늦게 활성화돼도 자연스럽게 합류한다
            if (autoStart && GameManager.HasInstance && GameManager.Instance.IsPlaying) StartSystem();
            else view?.Hide(instant: true);
        }

        void OnEnable()
        {
            if (EnsureConfigured())
                EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            // Disable 되면 모든 타이머가 멈춘다. (Update 가 돌지 않으므로 자동이지만 상태도 정리해 둔다)
            StopSystem();
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            switch (e.Current)
            {
                case GameState.Playing:
                    if (autoStart && !IsRunning) StartSystem();
                    break;

                case GameState.Ready:
                case GameState.GameOver:
                    StopSystem();
                    break;
            }
        }

        // ---------------- 공개 메서드 ----------------

        /// <summary>시스템을 켠다. 첫 등장까지 firstSpawnDelay 만큼 기다린다.</summary>
        public void StartSystem()
        {
            if (!EnsureConfigured()) return;
            ResetSystem();
            _phase = Phase.Waiting;
            _phaseDeadline = Time.time + config.FirstSpawnDelay;
            Log($"Start (first spawn in {config.FirstSpawnDelay:0.#}s)");
        }

        /// <summary>시스템을 멈춘다. 활성 요청이 있으면 StageEnded 로 종료 처리한다.</summary>
        public void StopSystem()
        {
            if (_phase == Phase.Active || _phase == Phase.HitHold)
                EndRequest(SpecialAudienceEndReason.StageEnded, hideInstant: true);

            _phase = Phase.Stopped;
            _phaseDeadline = 0f;
            view?.Hide(instant: true);
        }

        /// <summary>내부 상태를 초기값으로 되돌린다. (타이머·셔플 백·연출)</summary>
        public void ResetSystem()
        {
            _phase = Phase.Stopped;
            _phaseDeadline = 0f;
            _hitRaisedForCurrentRequest = false;
            _lastReportedRemaining = -1f;
            RefillBag();
            view?.Hide(instant: true);
        }

        /// <summary>랜덤 타입으로 즉시 등장시킨다. 이미 활성 요청이 있으면 교체한다.</summary>
        public void ForceSpawnRandom() => ForceSpawn(TakeFromBag());

        /// <summary>지정한 타입으로 즉시 등장시킨다. 이미 활성 요청이 있으면 교체한다.</summary>
        public void ForceSpawn(HeatStage requestType)
        {
            if (_phase == Phase.Active || _phase == Phase.HitHold)
                EndRequest(SpecialAudienceEndReason.ReplacedByDebug, hideInstant: true);

            Spawn(requestType);
        }

        /// <summary>
        /// 구형 호출부 호환용. 요청은 소비하지만 보상 데이터는 반환하지 않는다.
        /// </summary>
        [Obsolete("Use ConsumeRequest so CardDefinition remains the single reward source.")]
        public bool TrySpecialHit(HeatStage playedType, out SpecialHitReward reward)
        {
            reward = default;
            return ConsumeRequest(playedType, 0, 0f);
        }

        /// <summary>
        /// [카드 판정 경로용] 카드가 자기 수치(CardDefinition 의 SpecialHitBaseScore/HeatDelta)로
        /// 이미 보상을 적용한 뒤, 요청을 소비만 시킬 때 쓴다.
        ///
        /// 보상 수치의 단일 원천은 CardDefinition이다.
        /// 이벤트에는 <b>실제로 적용된</b> 값이 실려 나간다.
        /// </summary>
        public bool ConsumeRequest(HeatStage playedType, int appliedBaseScore, float appliedHeatDelta)
        {
            if (!CanAcceptHit(playedType)) return false;

            CompleteHit(new SpecialHitReward(appliedBaseScore, appliedHeatDelta), alreadyApplied: true);
            return true;
        }

        /// <summary>지금 이 타입의 히트를 받아줄 수 있는가.</summary>
        bool CanAcceptHit(HeatStage playedType)
        {
            // 만료된 프레임과 호출이 겹쳐도 여기서 걸린다 (Update 가 먼저 Phase 를 바꿨으면 Active 가 아니다)
            if (_phase != Phase.Active) return false;
            if (playedType != _currentRequest) return false;
            if (_hitRaisedForCurrentRequest) return false; // 같은 프레임 중복 호출 방지
            return true;
        }

        /// <summary>히트 성공 처리의 단일 통로. 이벤트는 한 요청당 정확히 한 번만 나간다.</summary>
        void CompleteHit(SpecialHitReward reward, bool alreadyApplied = false)
        {
            _hitRaisedForCurrentRequest = true;

            // 1. 제한시간 중지 → 2. 이벤트 → 3. 연출 → (4. 유지 후 숨김·다음 간격 시작은 Update 에서)
            _phase = Phase.HitHold;
            _phaseDeadline = Time.time + config.SpecialHitHoldDuration;

            var type = _currentRequest;

            OnSpecialHit?.Invoke(type, reward);
            OnSpecialAudienceEnded?.Invoke(type, SpecialAudienceEndReason.SpecialHit);
            EventBus.Raise(new SpecialHitLanded
            {
                RequestType = type,
                Reward = reward,
                AlreadyApplied = alreadyApplied,
                HoldDuration = config.SpecialHitHoldDuration
            });
            EventBus.Raise(new SpecialAudienceEnded { RequestType = type, Reason = SpecialAudienceEndReason.SpecialHit });

            view?.PlaySpecialHit();
            Log($"Special Hit: {type} ({reward}){(alreadyApplied ? " [카드가 이미 적용함]" : "")}");
        }

        // ---------------- 타이머 ----------------

        void Update()
        {
            // 디버그 키는 시스템이 꺼져 있어도 받는다 (Play Mode 에서 바로 확인할 수 있도록)
            HandleDebugKey();

            if (_phase == Phase.Stopped) return;

            // 공연 중이 아니면(일시정지 등) 시간이 흐르지 않게 한다
            if (GameManager.HasInstance && !GameManager.Instance.IsPlaying) return;

            switch (_phase)
            {
                case Phase.Waiting:
                    if (Time.time >= _phaseDeadline) Spawn(TakeFromBag());
                    break;

                case Phase.Active:
                    ReportRemaining();
                    if (Time.time >= _phaseDeadline)
                    {
                        var type = _currentRequest;
                        EndRequest(SpecialAudienceEndReason.Expired, hideInstant: false);
                        OnSpecialAudienceExpired?.Invoke(type);
                        ScheduleNextSpawn();
                        Log($"Expired: {type}");
                    }
                    break;

                case Phase.HitHold:
                    if (Time.time >= _phaseDeadline)
                    {
                        view?.Hide(instant: false);
                        ScheduleNextSpawn();
                    }
                    break;
            }
        }

        void ReportRemaining()
        {
            float remaining = RemainingTime;
            if (Mathf.Approximately(remaining, _lastReportedRemaining)) return;

            _lastReportedRemaining = remaining;
            OnRequestTimeChanged?.Invoke(remaining, config.RequestDuration);
            view?.SetRemaining(remaining, config.RequestDuration);
        }

        void Spawn(HeatStage requestType)
        {
            _currentRequest = requestType;
            _hitRaisedForCurrentRequest = false;
            _lastReportedRemaining = -1f;
            _phase = Phase.Active;
            _phaseDeadline = Time.time + config.RequestDuration;

            OnSpecialAudienceSpawned?.Invoke(requestType, config.RequestDuration);
            EventBus.Raise(new SpecialAudienceSpawned { RequestType = requestType, Duration = config.RequestDuration });

            view?.Show(requestType, config.RequestDuration);
            ReportRemaining();
        }

        /// <summary>만료·교체·공연 종료로 요청을 끝낸다. 성공 처리는 CompleteHit이 담당한다.</summary>
        void EndRequest(SpecialAudienceEndReason reason, bool hideInstant)
        {
            var type = _currentRequest;

            if (reason != SpecialAudienceEndReason.SpecialHit)
            {
                OnSpecialAudienceEnded?.Invoke(type, reason);
                EventBus.Raise(new SpecialAudienceEnded { RequestType = type, Reason = reason });
            }

            _hitRaisedForCurrentRequest = false;
            _lastReportedRemaining = -1f;

            if (reason == SpecialAudienceEndReason.Expired) view?.PlayExpire();
            else view?.Hide(hideInstant);
        }

        void ScheduleNextSpawn()
        {
            _phase = Phase.Waiting;
            _phaseDeadline = Time.time + config.SpawnInterval;
        }

        // ---------------- 셔플 백 ----------------

        /// <summary>세 타입을 채우고 섞는다. 같은 타입이 연달아 나오는 편중을 줄인다.</summary>
        void RefillBag()
        {
            _bag.Clear();
            _bag.Add(HeatStage.Chill);
            _bag.Add(HeatStage.Singalong);
            _bag.Add(HeatStage.Mosh);

            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
        }

        HeatStage TakeFromBag()
        {
            if (_bag.Count == 0) RefillBag();

            int last = _bag.Count - 1;
            var picked = _bag[last];
            _bag.RemoveAt(last);
            return picked;
        }

        // ---------------- 디버그 ----------------

        void HandleDebugKey()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!enableDebugKey) return;
            if (!WasDebugKeyPressed()) return;

            // 시스템이 아직 안 켜진 상태(공연 시작 전)에서도 바로 확인할 수 있게 한다
            var type = TakeFromBag();
            ForceSpawn(type);
            Debug.Log($"[SpecialAudience] Debug Spawn: {type}");
#endif
        }

        bool WasDebugKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;

            // 알파벳 키만 매핑한다 (기본값 P). 다른 키가 필요하면 여기에 케이스를 추가한다.
            if (debugSpawnKey >= KeyCode.A && debugSpawnKey <= KeyCode.Z)
            {
                var key = Key.A + (debugSpawnKey - KeyCode.A);
                return kb[key].wasPressedThisFrame;
            }
            return false;
#else
            return Input.GetKeyDown(debugSpawnKey);
#endif
        }

        void Log(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verboseLogs) Debug.Log($"[SpecialAudience] {message}");
#endif
        }

        // ---------------- 카드 시스템 없이 테스트하기 ----------------

        [ContextMenu("Debug/Spawn Random Special Audience")]
        void DebugSpawnRandom()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SpecialAudience] Play Mode 에서만 동작합니다.");
                return;
            }
            if (!IsRunning) StartSystem();
            ForceSpawnRandom();
            Debug.Log($"[SpecialAudience] Debug Spawn: {_currentRequest}");
        }

        [ContextMenu("Debug/Spawn Chill")]
        void DebugSpawnChill() => DebugSpawnStage(HeatStage.Chill);

        [ContextMenu("Debug/Spawn Singalong")]
        void DebugSpawnSingalong() => DebugSpawnStage(HeatStage.Singalong);

        [ContextMenu("Debug/Spawn Mosh")]
        void DebugSpawnMosh() => DebugSpawnStage(HeatStage.Mosh);

        [ContextMenu("Debug/Hide Special Audience")]
        void DebugHide()
        {
            if (!Application.isPlaying) return;
            if (_phase == Phase.Active || _phase == Phase.HitHold)
                EndRequest(SpecialAudienceEndReason.StageEnded, hideInstant: true);
            ScheduleNextSpawn();
        }

        void DebugSpawnStage(HeatStage stage)
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SpecialAudience] Play Mode 에서만 동작합니다.");
                return;
            }
            if (!IsRunning) StartSystem();
            ForceSpawn(stage);
            Debug.Log($"[SpecialAudience] Debug Spawn: {stage}");
        }

        [ContextMenu("Debug/Resolve Current Request As Special Hit")]
        void DebugResolveCurrentAsSpecialHit()
        {
            if (!HasActiveRequest)
            {
                Debug.LogWarning("[SpecialAudience] 활성화된 특별 관객이 없습니다.");
                return;
            }

            // 보상은 CardDefinition만 소유하므로 디버그 경로는 요청 소비와 연출만 검증한다.
            if (ConsumeRequest(_currentRequest, 0, 0f))
                Debug.Log("[SpecialAudience] Debug Special Hit 성공");
        }
    }

    /// <summary>
    /// 어디서든 한 줄로 특별 관객을 다루는 전역 접근자. (Sound / Hype / CrowdMood 와 같은 패턴)
    ///
    /// 카드 담당은 최종적으로 이 한 줄이면 된다:
    ///   if (SpecialAudience.TryHit(cardType, out var reward)) { /* 점수·열기 담당이 reward 적용 */ }
    /// </summary>
    public static class SpecialAudience
    {
        public static bool Exists => SpecialAudienceManager.HasInstance;

        public static bool HasActiveRequest =>
            SpecialAudienceManager.HasInstance && SpecialAudienceManager.Instance.HasActiveRequest;

        public static float RemainingTime =>
            SpecialAudienceManager.HasInstance ? SpecialAudienceManager.Instance.RemainingTime : 0f;

        /// <summary>
        /// [카드 판정용] 지금 살아 있는 요청을 CardEffectResolver 가 쓰는 형태로 넘겨준다.
        ///
        /// <b>드롭 위치 게이트가 여기 들어 있다.</b> requireDropOnTarget 이 켜져 있으면
        /// 포인터가 특별 관객의 히트 영역 안일 때만 요청을 돌려준다.
        /// 덕분에 카드 코드는 그대로 두고도 "특별 관객 위에 놓아야 성공"이 성립한다.
        /// (영역 밖이면 None → 카드는 평소대로 일반 판정을 받는다)
        /// </summary>
        public static SpecialCardRequest CurrentRequest
        {
            get
            {
                if (!HasActiveRequest) return SpecialCardRequest.None;

                var manager = SpecialAudienceManager.Instance;
                if (manager.ShouldGateByDropTarget && !manager.IsPointerOverDropTarget)
                    return SpecialCardRequest.None;

                return new SpecialCardRequest(manager.CurrentRequestType);
            }
        }

        /// <summary>드롭 순간의 화면 좌표와 카드 반지름으로 요청을 확정한다.</summary>
        public static SpecialCardRequest ResolveDropRequest(
            Vector2 screenPosition,
            float radiusPixels)
        {
            if (!SpecialAudienceManager.HasInstance) return SpecialCardRequest.None;
            return SpecialAudienceManager.Instance.ResolveDropRequest(screenPosition, radiusPixels);
        }

        /// <summary>현재 드롭 영역. 없으면 null. (카드 담당이 Hover 표시를 직접 하고 싶을 때)</summary>
        public static SpecialAudienceDropTarget CurrentDropTarget =>
            SpecialAudienceManager.HasInstance ? SpecialAudienceManager.Instance.CurrentDropTarget : null;

        /// <summary>
        /// [카드 판정용] 카드가 자기 수치로 보상을 이미 적용한 뒤 요청을 소비시킨다.
        /// 매니저 보상은 쓰지 않으므로 이중 적용이 생기지 않는다.
        /// </summary>
        public static bool ConsumeRequest(HeatStage playedType, int appliedBaseScore, float appliedHeatDelta)
            => SpecialAudienceManager.HasInstance &&
               SpecialAudienceManager.Instance.ConsumeRequest(playedType, appliedBaseScore, appliedHeatDelta);

        /// <summary>씬에 시스템이 없으면 아무 일도 없이 false. (억지로 생성하지 않는다)</summary>
        public static bool TryHit(HeatStage playedType, out SpecialHitReward reward)
        {
            if (!SpecialAudienceManager.HasInstance)
            {
                reward = default;
                return false;
            }
            reward = default;
            return SpecialAudienceManager.Instance.ConsumeRequest(playedType, 0, 0f);
        }

        public static void ForceSpawnRandom()
        {
            if (SpecialAudienceManager.HasInstance) SpecialAudienceManager.Instance.ForceSpawnRandom();
        }
    }
}
