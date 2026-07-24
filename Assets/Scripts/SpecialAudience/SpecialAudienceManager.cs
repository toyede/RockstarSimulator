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
    /// - 공연이 시작되면 firstSpawnDelay 뒤 특별 관객이 등장하고, 이후 spawnInterval 마다 반복
    /// - 요구 타입은 셔플 백(Chill/Singalong/Mosh)에서 하나씩 꺼내 뽑는다
    /// - requestDuration 안에 대응하지 못하면 <b>패널티 없이</b> 사라진다
    /// - P 키(디버그)로 즉시 등장·교체할 수 있다
    ///
    /// [카드 담당이 나중에 연결할 지점]
    ///   if (SpecialAudience.TryHit(cardType, out var reward)) { /* reward 를 점수·열기에 적용 */ }
    ///   또는 직접 참조로: specialAudienceManager.TrySpecialHit(cardType, out var reward)
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

        [Header("등장 규칙")]
        [SerializeField, Tooltip("공연 시작 후 첫 특별 관객이 등장하기까지의 시간(초)")]
        float firstSpawnDelay = 10f;

        [SerializeField, Tooltip("특별 관객이 사라진 뒤 다음 등장까지의 간격(초)")]
        float spawnInterval = 15f;

        [SerializeField, Tooltip("요구가 유지되는 제한시간(초). 이 안에 대응하지 못하면 패널티 없이 사라진다")]
        float requestDuration = 7f;

        [SerializeField, Tooltip("공연 시작(Ready → Playing)에 시스템을 자동으로 켤지")]
        bool autoStart = true;

        [Header("Special Hit 보상 (직접 적용하지 않고 이벤트로만 전달)")]
        [SerializeField, Tooltip("Special Hit 기본 점수")]
        int specialHitBaseScore = 400;

        [SerializeField, Tooltip("Special Hit 열기(호응도) 보너스")]
        float specialHitBonusHeat = 25f;

        [SerializeField, Tooltip("Special Hit 연출을 유지하는 시간(초). 이후 특별 관객이 숨겨진다")]
        float specialHitHoldDuration = 0.7f;

        [Header("View")]
        [SerializeField, Tooltip("표시 담당. 비워두면 로직만 동작한다 (null-safe)")]
        SpecialAudienceView view;

        [Header("디버그")]
        [SerializeField, Tooltip("이 키를 누르면 특별 관객이 즉시 등장한다 (에디터·개발 빌드 전용)")]
        KeyCode debugSpawnKey = KeyCode.P;

        [SerializeField, Tooltip("디버그 키 입력을 받을지")]
        bool enableDebugKey = true;

        [SerializeField, Tooltip("콘솔 로그 출력 여부")]
        bool verboseLogs = true;

        // ---------------- 상태 ----------------

        Phase _phase = Phase.Stopped;
        float _phaseDeadline;                 // 현재 페이즈가 끝나는 시각 (Time.time 기준)
        SpecialAudienceRequestType _currentRequest;
        bool _hitRaisedForCurrentRequest;     // 한 요청당 Special Hit 이벤트 1회 보장
        float _lastReportedRemaining = -1f;   // 같은 값 중복 통지 방지

        /// <summary>셔플 백. 매 등장마다 새 리스트를 만들지 않고 재사용한다.</summary>
        readonly List<SpecialAudienceRequestType> _bag = new List<SpecialAudienceRequestType>(3);

        /// <summary>씬 재시작 시 새로 초기화되도록 씬에 종속시킨다. (HypeSystem·CardSystem 과 동일)</summary>
        protected override bool Persistent => false;

        // ---------------- 공개 상태 ----------------

        public bool HasActiveRequest => _phase == Phase.Active;
        public SpecialAudienceRequestType CurrentRequestType => _currentRequest;
        public float RequestDuration => requestDuration;

        public float RemainingTime =>
            _phase == Phase.Active ? Mathf.Max(0f, _phaseDeadline - Time.time) : 0f;

        /// <summary>현재 남은 시간의 0~1 비율. 게이지용.</summary>
        public float RemainingNormalized =>
            requestDuration > 0f ? Mathf.Clamp01(RemainingTime / requestDuration) : 0f;

        public bool IsRunning => _phase != Phase.Stopped;

        // ---------------- 이벤트 ----------------

        /// <summary>(요구 타입, 전체 제한시간)</summary>
        public event Action<SpecialAudienceRequestType, float> OnSpecialAudienceSpawned;

        /// <summary>(남은 시간, 전체 제한시간) — 요구 중 매 프레임</summary>
        public event Action<float, float> OnRequestTimeChanged;

        /// <summary>(요구 타입) — 제한시간 초과로 사라짐</summary>
        public event Action<SpecialAudienceRequestType> OnSpecialAudienceExpired;

        /// <summary>(요구 타입, 종료 사유) — 모든 종료를 하나로 받고 싶을 때</summary>
        public event Action<SpecialAudienceRequestType, SpecialAudienceEndReason> OnSpecialAudienceEnded;

        /// <summary>(요구 타입, 보상) — 한 요청당 정확히 1회</summary>
        public event Action<SpecialAudienceRequestType, SpecialHitReward> OnSpecialHit;

        // ---------------- 수명 주기 ----------------

        protected override void OnAwake()
        {
            if (view == null) view = GetComponentInChildren<SpecialAudienceView>(true);
            RefillBag();
        }

        void Start()
        {
            // 이미 공연이 진행 중인 상태에서 늦게 활성화돼도 자연스럽게 합류한다
            if (autoStart && GameManager.HasInstance && GameManager.Instance.IsPlaying) StartSystem();
            else view?.Hide(instant: true);
        }

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

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
            ResetSystem();
            _phase = Phase.Waiting;
            _phaseDeadline = Time.time + Mathf.Max(0f, firstSpawnDelay);
            Log($"Start (first spawn in {firstSpawnDelay:0.#}s)");
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
        public void ForceSpawn(SpecialAudienceRequestType requestType)
        {
            if (_phase == Phase.Active || _phase == Phase.HitHold)
                EndRequest(SpecialAudienceEndReason.ReplacedByDebug, hideInstant: true);

            Spawn(requestType);
        }

        /// <summary>
        /// [카드 담당용 공개 API] 낸 카드의 타입이 현재 요구와 맞는지 판정한다.
        ///
        /// 성공하면 true 와 보상 데이터를 돌려주고, 제한시간을 멈춘 뒤 연출에 들어간다.
        /// 실패하면 false 를 돌려주고 <b>현재 요청은 그대로 유지된다</b> (남은 시간도 계속 감소).
        /// 어느 쪽이든 이 함수는 점수·열기를 직접 건드리지 않는다.
        /// </summary>
        public bool TrySpecialHit(SpecialAudienceRequestType playedType, out SpecialHitReward reward)
        {
            reward = default;

            // 만료된 프레임과 호출이 겹쳐도 여기서 걸린다 (Update 가 먼저 Phase 를 바꿨으면 Active 가 아니다)
            if (_phase != Phase.Active) return false;
            if (playedType != _currentRequest) return false;
            if (_hitRaisedForCurrentRequest) return false; // 같은 프레임 중복 호출 방지

            reward = new SpecialHitReward(specialHitBaseScore, specialHitBonusHeat);
            _hitRaisedForCurrentRequest = true;

            // 1. 제한시간 중지 → 2. 이벤트 → 3. 연출 → (4. 유지 후 숨김·다음 간격 시작은 Update 에서)
            _phase = Phase.HitHold;
            _phaseDeadline = Time.time + Mathf.Max(0f, specialHitHoldDuration);

            var type = _currentRequest;
            var payload = reward;

            OnSpecialHit?.Invoke(type, payload);
            OnSpecialAudienceEnded?.Invoke(type, SpecialAudienceEndReason.SpecialHit);
            EventBus.Raise(new SpecialHitLanded { RequestType = type, Reward = payload });
            EventBus.Raise(new SpecialAudienceEnded { RequestType = type, Reason = SpecialAudienceEndReason.SpecialHit });

            view?.PlaySpecialHit();
            Log($"Special Hit: {type} ({payload})");
            return true;
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
            OnRequestTimeChanged?.Invoke(remaining, requestDuration);
            view?.SetRemaining(remaining, requestDuration);
        }

        void Spawn(SpecialAudienceRequestType requestType)
        {
            _currentRequest = requestType;
            _hitRaisedForCurrentRequest = false;
            _lastReportedRemaining = -1f;
            _phase = Phase.Active;
            _phaseDeadline = Time.time + Mathf.Max(0.01f, requestDuration);

            OnSpecialAudienceSpawned?.Invoke(requestType, requestDuration);
            EventBus.Raise(new SpecialAudienceSpawned { RequestType = requestType, Duration = requestDuration });

            view?.Show(requestType, requestDuration);
            ReportRemaining();
        }

        /// <summary>요청을 끝낸다. (Special Hit 는 TrySpecialHit 에서 따로 처리하므로 여기로 오지 않는다)</summary>
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
            _phaseDeadline = Time.time + Mathf.Max(0f, spawnInterval);
        }

        // ---------------- 셔플 백 ----------------

        /// <summary>세 타입을 채우고 섞는다. 같은 타입이 연달아 나오는 편중을 줄인다.</summary>
        void RefillBag()
        {
            _bag.Clear();
            _bag.Add(SpecialAudienceRequestType.Chill);
            _bag.Add(SpecialAudienceRequestType.Singalong);
            _bag.Add(SpecialAudienceRequestType.Mosh);

            for (int i = _bag.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
        }

        SpecialAudienceRequestType TakeFromBag()
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

        [ContextMenu("Debug/Resolve Current Request As Special Hit")]
        void DebugResolveCurrentAsSpecialHit()
        {
            if (!HasActiveRequest)
            {
                Debug.LogWarning("[SpecialAudience] 활성화된 특별 관객이 없습니다.");
                return;
            }

            // 현재 요구 타입을 그대로 넘겨 정상 경로(TrySpecialHit)로 성공시킨다
            if (TrySpecialHit(_currentRequest, out var reward))
                Debug.Log($"[SpecialAudience] Debug Special Hit 성공 → {reward}");
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

        /// <summary>씬에 시스템이 없으면 아무 일도 없이 false. (억지로 생성하지 않는다)</summary>
        public static bool TryHit(SpecialAudienceRequestType playedType, out SpecialHitReward reward)
        {
            if (!SpecialAudienceManager.HasInstance)
            {
                reward = default;
                return false;
            }
            return SpecialAudienceManager.Instance.TrySpecialHit(playedType, out reward);
        }

        public static void ForceSpawnRandom()
        {
            if (SpecialAudienceManager.HasInstance) SpecialAudienceManager.Instance.ForceSpawnRandom();
        }
    }
}
