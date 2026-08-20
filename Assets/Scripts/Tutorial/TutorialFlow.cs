using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 스킵 가능한 인터랙티브 튜토리얼 디렉터. 별도 씬 없이 실제 게임 씬 위에서
    /// 관객 구성·손패 사용을 고정한 70~90초짜리 미니 공연으로 진행한다.
    ///
    /// 핵심 문장: "옷은 무엇을 좋아하는지, 움직임은 지금 얼마나 신났는지 알려줍니다."
    ///
    /// 진행: Intro → 성향 3연습(Mosh 정답 공개 → Singalong 절반 힌트 → Chill 자율) → 호버 안내
    ///       → 교차 반응 → 상태 읽기(지루한 관객 구하기) → 관객 교체 → 피버타임 체험
    ///       → 10초 최종 미니 공연 → 완료
    ///
    /// 다른 시스템을 제어하는 방법 (전부 기존 공개 API·한 줄 훅):
    ///   관객 고정      : AudienceRosterSystem.TryAdd/TryRemove/TrySetEngagement
    ///                    + SuppressNaturalArrivals(유입 정지) + SuppressEngagementDecay(개별 몰입도 감소 정지)
    ///   손패 고정      : CardSystem.SetHand (성향별 카드 1장씩, 총 3장)
    ///   카드 제한      : CardInput.UseFilter (거부된 카드는 소모 없이 손패로 복귀)
    ///   호응도 정지    : Hype.SetDecayPaused(true)
    ///   특별 관객 정지 : SpecialAudienceManager.StopSystem()
    ///   위기 정지      : NearbyConcertCrisisDirector.enabled = false
    ///   호버 안내      : AudiencePreferenceHoverController.RevealedActor (읽기 전용 폴링)
    ///   피버 체험      : FeverSystem.ForceStart() (강제 발동, IsActive 폴링으로 유지)
    ///   공연 시간 정지  : PerformanceTimer.SetPaused(true)
    /// 끝나면(스킵 포함) 전부 원래대로 복구하고 새 공연을 시작한다.
    ///
    /// 완료 여부는 Save("tutorial_done") 에 저장되어 다음 실행부터는 자동으로 뜨지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialFlow : MonoBehaviour
    {
        public const string TutorialDoneKey = "tutorial_done";

        enum Phase
        {
            Idle,
            Intro,
            PrefMosh, PrefSingalong, PrefChill,
            HoverHint,
            CrossUse, CrossExplain,
            Excitement, GameOverExplain,
            CrowdChange, LightingHint,
            FeverIntro, FeverExplain,
            SpecialIntro, SpecialUse, SpecialExplain,
            FinalIntro, FinalRun, FinalFail,
            Complete
        }

        [Header("연결")]
        [SerializeField, Tooltip("표시 담당. 비워두면 씬에서 찾는다")]
        TutorialOverlayUI overlay;

        [Header("실행")]
        [SerializeField, Tooltip("첫 공연 시작 시 자동 실행 (완료 기록이 있으면 안 뜬다)")]
        bool autoRunOnFirstPlay = true;

        [SerializeField, Tooltip("공연 시작 후 튜토리얼이 뜨기까지의 여유(초). 시스템 초기화 대기")]
        float startDelay = 0.6f;

        [Header("관객 몰입도 프리셋 (0~100)")]
        [SerializeField, Tooltip("지루(Calm) 상태로 만들 값")] float calmEngagement = 16f;
        [SerializeField, Tooltip("관심(Middle) 상태로 만들 값")] float middleEngagement = 52f;
        [SerializeField, Tooltip("흥분(Excited) 상태로 만들 값")] float excitedEngagement = 88f;

        [Header("최종 미니 공연")]
        [SerializeField, Tooltip("제한 시간(초)")] float finalDuration = 10f;
        [SerializeField, Tooltip("목표 총 반응 점수")] int finalScoreGoal = 1000;

        [Header("관객 Hover 학습")]
        [SerializeField, Min(0.5f), Tooltip("테두리와 말풍선을 읽도록 Hover를 유지해야 하는 시간(초)")]
        float hoverInspectionDuration = 2f;

        /// <summary>튜토리얼 진행 중인가. (TutorialTips 가 이때는 조용히 있는다)</summary>
        public static bool IsRunning { get; private set; }

        Phase _phase = Phase.Idle;
        float _phaseTimer;
        float _pendingStartAt = -1f;
        bool _hasStartedThisScene;

        CrowdPreference _expectedPref;
        string _blockedHint = "";
        bool _allowAllCards;
        bool _hoverTaught;
        float _hoverInspectionTimer;
        AudienceId _crowdChangeHoverTargetId;

        // 최종 미니 공연 집계
        int _finalScore;

        // 복구용
        NearbyConcertCrisisDirector _crisis;
        AudienceRosterPresenter _presenter;
        AudiencePreferenceHoverController _hoverController;
        bool _crisisWasEnabled;

        void Awake()
        {
            if (overlay == null) overlay = FindFirstObjectByType<TutorialOverlayUI>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            CardInput.UseBlocked += OnCardBlocked;

            if (overlay != null)
            {
                overlay.ContinueClicked += OnContinueClicked;
                overlay.SkipClicked += SkipTutorial;
            }
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            CardInput.UseBlocked -= OnCardBlocked;

            if (overlay != null)
            {
                overlay.ContinueClicked -= OnContinueClicked;
                overlay.SkipClicked -= SkipTutorial;
            }

            if (IsRunning) ReleaseControl(); // Disable 로도 훅이 남지 않게
        }

        void Update()
        {
            // 자동 시작 대기
            if (_pendingStartAt > 0f && Time.unscaledTime >= _pendingStartAt)
            {
                _pendingStartAt = -1f;
                StartTutorial();
            }

            if (!IsRunning) return;
            _phaseTimer += Time.deltaTime;

            switch (_phase)
            {
                case Phase.CrowdChange:
                    bool isInspectingNewAudience =
                        !_hoverTaught &&
                        _crowdChangeHoverTargetId.IsValid &&
                        _hoverController != null &&
                        _hoverController.RevealedActor != null &&
                        _hoverController.RevealedActor.BoundId == _crowdChangeHoverTargetId;

                    if (isInspectingNewAudience)
                    {
                        _hoverInspectionTimer += Time.unscaledDeltaTime;
                        if (_hoverInspectionTimer >= hoverInspectionDuration)
                        {
                            _hoverTaught = true;
                            overlay?.ShowMessage(
                                "새 관객의 정보를 확인했습니다.",
                                "관객 구성이 바뀌면 호응을 크게 이끌어낼 카드도 달라집니다. " +
                                "새 관객의 취향을 기억하세요. (클릭해서 계속)",
                                true);
                        }
                    }
                    else if (!_hoverTaught)
                    {
                        _hoverInspectionTimer = 0f;
                    }
                    break;

                case Phase.HoverHint:
                    bool isInspecting = _hoverController != null && _hoverController.RevealedActor != null;
                    if (!_hoverTaught && isInspecting)
                    {
                        _hoverInspectionTimer += Time.unscaledDeltaTime;
                        if (_hoverInspectionTimer >= hoverInspectionDuration)
                        {
                            _hoverTaught = true;
                            overlay?.ShowMessage(
                                "관객의 힌트를 확인했습니다.",
                                "테두리 색은 선호하는 카드 계열을, 말풍선은 원하는 행동과 현재 기분을 알려줍니다. " +
                                "(클릭해서 계속)",
                                true);
                        }
                    }
                    else if (!_hoverTaught)
                        _hoverInspectionTimer = 0f;
                    break;

                case Phase.FeverIntro:
                    // 플레이어가 반응하기 전에 FeverDuration 이 지나 꺼지면 즉시 다시 켠다
                    if (FeverSystem.HasInstance && !FeverSystem.Instance.IsActive)
                        FeverSystem.Instance.ForceStart();
                    break;

                case Phase.SpecialIntro:
                case Phase.SpecialUse:
                    // 설명을 읽는 동안 요청 시간이 끝나도 바로 같은 요청을 다시 보여준다.
                    if (SpecialAudienceManager.HasInstance &&
                        !SpecialAudienceManager.Instance.HasActiveRequest)
                    {
                        SpecialAudienceManager.Instance.ForceSpawn(HeatStage.Singalong);
                    }
                    break;

                case Phase.FinalRun:
                    float remain = Mathf.Max(0f, finalDuration - _phaseTimer);
                    overlay?.FlashSub(
                        $"남은 시간 {remain:0}초 · 총 반응 {_finalScore:+0;-0;0} / {finalScoreGoal}");
                    if (remain <= 0f) FinishFinalRun();
                    break;
            }
        }

        // ---------------- 시작 / 종료 ----------------

        void OnGameStateChanged(GameStateChanged e)
        {
            // 첫 공연 자동 실행
            if (autoRunOnFirstPlay &&
                !_hasStartedThisScene &&
                !IsRunning &&
                e.Previous == GameState.Ready && e.Current == GameState.Playing)
            {
                _pendingStartAt = Time.unscaledTime + startDelay;
            }

            // 튜토리얼 도중 외부에서 게임오버·리셋되면 조용히 정리
            if (IsRunning && (e.Current == GameState.GameOver || e.Current == GameState.Ready))
                EndTutorial(markDone: false, restartRun: false);
        }

        /// <summary>수동 실행 진입점. (타이틀 '튜토리얼' 버튼이나 ContextMenu)</summary>
        public void StartTutorial()
        {
            if (IsRunning) return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying)
            {
                Debug.LogWarning("[Tutorial] 공연 중(Playing)에만 시작할 수 있습니다.");
                return;
            }
            if (!AudienceRosterSystem.HasInstance || !AudienceRosterSystem.Instance.IsConfigured)
            {
                Debug.LogWarning("[Tutorial] AudienceRosterSystem 이 준비되지 않았습니다.");
                return;
            }

            IsRunning = true;
            _hasStartedThisScene = true;
            TakeControl();
            EnterIntro();
        }

        /// <summary>스킵. 완료로 기록해 다음부터 뜨지 않는다.</summary>
        public void SkipTutorial() => EndTutorial(markDone: true, restartRun: true);

        void TakeControl()
        {
            var roster = AudienceRosterSystem.Instance;
            roster.SuppressNaturalArrivals = true;
            Hype.SetDecayPaused(true);
            PerformanceTimer.SetPaused(true);
            if (SpecialAudienceManager.HasInstance) SpecialAudienceManager.Instance.StopSystem();
            if (FeverSystem.HasInstance)
            {
                FeverSystem.Instance.SetAutomaticTriggerEnabled(false);
                FeverSystem.Instance.CancelFever();
            }

            _crisis = FindFirstObjectByType<NearbyConcertCrisisDirector>();
            _crisisWasEnabled = _crisis != null && _crisis.enabled;
            if (_crisis != null) _crisis.enabled = false;

            _presenter = FindFirstObjectByType<AudienceRosterPresenter>();
            _hoverController = FindFirstObjectByType<AudiencePreferenceHoverController>();

            roster.SuppressEngagementDecay = true;

            CardInput.UseFilter = FilterCard;
            _allowAllCards = false;
            SetupFixedHand();

            overlay?.SetSkipVisible(true);
        }

        void ReleaseControl()
        {
            IsRunning = false;

            if (AudienceRosterSystem.HasInstance)
            {
                AudienceRosterSystem.Instance.SuppressNaturalArrivals = false;
                AudienceRosterSystem.Instance.SuppressEngagementDecay = false;
            }
            Hype.SetDecayPaused(false);
            PerformanceTimer.SetPaused(false);
            if (_crisis != null) _crisis.enabled = _crisisWasEnabled;
            if (SpecialAudienceManager.HasInstance)
                SpecialAudienceManager.Instance.StopSystem();
            if (FeverSystem.HasInstance)
            {
                FeverSystem.Instance.CancelFever();
                FeverSystem.Instance.SetAutomaticTriggerEnabled(true);
            }
            CardInput.UseFilter = null;
            overlay?.HideAll();
            overlay?.SetSkipVisible(false);
        }

        void EndTutorial(bool markDone, bool restartRun)
        {
            _phase = Phase.Idle;
            ReleaseControl();

            // 완료 여부를 저장하지 않아 다음 실행에서도 튜토리얼이 다시 진행된다

            if (restartRun && GameManager.HasInstance && GameManager.Instance.IsPlaying)
            {
                // Ready 이벤트에서 덱·관객·콤보 자원·공연 결과 집계를 초기화하고,
                // Playing 이벤트에서 타이머·호응도·공연 시스템을 새로 시작한다.
                GameManager.Instance.ResetGame();
                GameManager.Instance.StartGame();
            }
        }

        // ---------------- 관객 구성 헬퍼 ----------------

        readonly List<AudienceId> _spawned = new List<AudienceId>();

        static readonly CrowdPreference[] FixedHandOrder =
            { CrowdPreference.Mosh, CrowdPreference.Singalong, CrowdPreference.Chill };

        /// <summary>튜토리얼 시작 손패를 성향별 카드 1장씩, 총 3장 고정으로 세팅한다.</summary>
        void SetupFixedHand()
        {
            if (!CardSystem.HasInstance) return;
            var system = CardSystem.Instance;
            var config = system.Config;
            if (config == null) return;

            var cards = new List<CardDefinition>(3);
            foreach (var pref in FixedHandOrder)
            {
                foreach (var entry in config.CardPool)
                {
                    if (entry == null || !entry.IsUsable) continue;
                    if (entry.Prefab.Role == CardRole.Normal && entry.Prefab.TargetPreference == pref)
                    {
                        cards.Add(entry.Prefab);
                        break;
                    }
                }
            }

            if (cards.Count == 3) system.SetHand(cards);
            else Debug.LogWarning("[Tutorial] 카드 풀에 성향별 Normal 카드가 3종 모두 있어야 합니다.");
        }

        void SetupSpecialHand()
        {
            if (!CardSystem.HasInstance) return;
            var system = CardSystem.Instance;
            var config = system.Config;
            if (config == null) return;

            foreach (var entry in config.CardPool)
            {
                if (entry == null || !entry.IsUsable) continue;
                var card = entry.Prefab;
                if (card.Role != CardRole.Special ||
                    card.TargetPreference != CrowdPreference.Singalong)
                    continue;

                system.SetHand(new[] { card });
                return;
            }

            Debug.LogWarning("[Tutorial] SINGALONG Special 카드가 카드 풀에 없습니다.");
        }

        /// <summary>기존 관객을 정리하고 지정 구성으로 채운다. (추가 먼저 → 제거 — 로스터가 비면 게임오버가 뜨므로)</summary>
        void SetRoster(params (CrowdPreference pref, float engagement)[] members)
        {
            var roster = AudienceRosterSystem.Instance;

            var previous = new List<AudienceId>();
            foreach (var m in roster.Members) previous.Add(m.Id);

            _spawned.Clear();
            foreach (var (pref, engagement) in members)
            {
                if (roster.TryAdd(pref, engagement, AudienceJoinReason.RuntimeCommand, out var snapshot))
                    _spawned.Add(snapshot.Id);
            }

            foreach (var id in previous)
                roster.TryRemove(id, AudienceDepartureReason.RuntimeRemoval, out _);
        }

        AudienceMemberActor FindActor(CrowdPreference pref, AudienceEngagementStage? stage = null)
        {
            if (_presenter == null || !AudienceRosterSystem.HasInstance) return null;

            foreach (var m in AudienceRosterSystem.Instance.Members)
            {
                if (m.Preference != pref) continue;
                if (stage.HasValue && m.Stage != stage.Value) continue;
                if (_presenter.TryGetActor(m.Id, out var actor)) return actor;
            }
            return null;
        }

        void SpotlightPref(CrowdPreference pref, AudienceEngagementStage? stage = null)
        {
            overlay?.SetDim(true);
            overlay?.Spotlight(FindActor(pref, stage));
        }

        // ---------------- 카드 게이트 ----------------

        bool FilterCard(int handIndex)
        {
            if (_allowAllCards) return true;
            if (!CardSystem.HasInstance) return true;

            var card = CardSystem.Instance.GetCard(handIndex);
            if (card == null) return false;

            if (_phase == Phase.SpecialUse)
            {
                return card.Role == CardRole.Special &&
                       card.TargetPreference == CrowdPreference.Singalong;
            }

            // 손패에 정답 카드가 없으면 소프트락 방지를 위해 전부 허용한다
            if (!HandHasMatch()) return true;

            return card.Role == CardRole.Normal && card.TargetPreference == _expectedPref;
        }

        bool HandHasMatch()
        {
            var system = CardSystem.Instance;
            for (int i = 0; i < system.HandCount; i++)
            {
                var card = system.GetCard(i);
                if (card != null && card.Role == CardRole.Normal && card.TargetPreference == _expectedPref)
                    return true;
            }
            return false;
        }

        void OnCardBlocked(int handIndex)
        {
            if (!IsRunning || string.IsNullOrEmpty(_blockedHint)) return;
            overlay?.FlashSub(_blockedHint);
        }

        // ---------------- 진행 ----------------

        void OnContinueClicked()
        {
            if (!IsRunning) return;
            switch (_phase)
            {
                case Phase.Intro:        EnterPrefMosh(); break;
                case Phase.HoverHint:    EnterCrossUse(); break;
                case Phase.CrossExplain: EnterExcitement(); break;
                case Phase.GameOverExplain: EnterCrowdChange(); break;
                case Phase.CrowdChange:  EnterLightingHint(); break;
                case Phase.LightingHint: EnterFeverIntro(); break;
                case Phase.FeverExplain: EnterSpecialIntro(); break;
                case Phase.SpecialIntro: EnterSpecialUse(); break;
                case Phase.SpecialExplain: EnterFinalIntro(); break;
                case Phase.FinalIntro:   EnterFinalRun(); break;
                case Phase.FinalFail:    EnterFinalIntro(); break;
                case Phase.Complete:     EndTutorial(markDone: true, restartRun: true); break;
            }
        }

        void OnCardResolved(CardResolved e)
        {
            if (!IsRunning || e.Role == CardRole.Utility) return;

            switch (_phase)
            {
                case Phase.PrefMosh:      EnterPrefSingalong(); break;
                case Phase.PrefSingalong: EnterPrefChill(); break;
                case Phase.PrefChill:     EnterHoverHint(); break;
                case Phase.CrossUse:      EnterCrossExplain(e.GainedScore); break;
                case Phase.Excitement:    EnterGameOverExplain(); break;
                case Phase.FeverIntro:    EnterFeverExplain(e.GainedScore); break;
                case Phase.SpecialUse:
                    if (e.IsSpecialHit) EnterSpecialExplain();
                    else
                    {
                        SetupSpecialHand();
                        overlay?.FlashSub(
                            "요청 아이콘과 같은 SPECIAL 카드를 골라 특별 관객 위에 직접 놓으세요.");
                    }
                    break;
                case Phase.FinalRun:      _finalScore += e.GainedScore; break;
            }
        }

        void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTimer = 0f;
        }

        // ---------------- 각 단계 ----------------

        void EnterIntro()
        {
            SetPhase(Phase.Intro);
            SetRoster((CrowdPreference.Mosh, middleEngagement)); // 첫 관객 한 명만 등장

            overlay?.SetDim(true);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                "관객을 읽고, 알맞은 행동 카드를 사용하세요.",
                "이 게임은 관객의 복장과 상태를 살펴 호응을 이끌어내는 공연 게임입니다. " +
                "옷차림은 취향을, 움직임은 현재 신난 정도를 보여줍니다. (클릭해서 계속)",
                true);
        }

        void EnterPrefMosh()
        {
            SetPhase(Phase.PrefMosh);
            _expectedPref = CrowdPreference.Mosh;
            _blockedHint = "MOSH 관객은 격렬한 연주를 좋아합니다. 기타 솔로 카드를 골라보세요.";
            SpotlightPref(CrowdPreference.Mosh);
            overlay?.ShowMessage(
                "검은 옷과 밴드 패치 = MOSH 관객",
                "MOSH 관객은 거칠고 강한 연주에 크게 호응합니다. " +
                "기타 솔로 카드를 위로 드래그해 사용하세요.",
                false);
        }

        void EnterPrefSingalong()
        {
            SetPhase(Phase.PrefSingalong);
            _expectedPref = CrowdPreference.Singalong;
            _blockedHint = "SINGALONG 관객은 함께 노래하고 싶어 합니다. 손 머리 위로! 카드를 골라보세요.";
            SetRoster((CrowdPreference.Singalong, middleEngagement)); // Mosh 퇴장, Singalong 등장
            SpotlightPref(CrowdPreference.Singalong);
            overlay?.ShowMessage(
                "트레이닝복을 입은 관객 = SINGALONG 관객",
                "SINGALONG 관객은 함께 노래하고 공연에 참여하는 행동을 좋아합니다. " +
                "손 머리 위로! 카드를 사용하세요.",
                false);
        }

        void EnterPrefChill()
        {
            SetPhase(Phase.PrefChill);
            _expectedPref = CrowdPreference.Chill;
            _blockedHint = "CHILL 관객은 여유로운 공연을 좋아합니다. 청록색 CHILL 계열 카드를 골라보세요.";
            SetRoster((CrowdPreference.Chill, middleEngagement)); // Singalong 퇴장, Chill 등장
            SpotlightPref(CrowdPreference.Chill);
            overlay?.ShowMessage(
                "차분한 복장 = CHILL 관객",
                "CHILL 관객은 편안하고 여유로운 공연에 크게 호응합니다. " +
                "이번에는 옷차림을 단서로 알맞은 카드를 골라보세요.",
                false);
        }

        void EnterHoverHint()
        {
            SetPhase(Phase.HoverHint);
            _hoverTaught = false;
            _hoverInspectionTimer = 0f;
            overlay?.ShowMessage(
                "관객 위에 마우스를 올리거나 길게 누르면 관객의 마음을 알 수 있습니다.",
                $"말풍선과 테두리색을 통해 좋아하는 행동과 현재 기분을 확인할 수 있습니다. " +
                $"{hoverInspectionDuration:0.#}초 동안 유지해 힌트를 확인하세요.",
                false);
        }

        void EnterCrossUse()
        {
            SetPhase(Phase.CrossUse);
            _allowAllCards = true; // 어떤 공연 카드든 좋다 — 교차 반응을 보는 게 목적
            SetRoster(
                (CrowdPreference.Mosh, middleEngagement),
                (CrowdPreference.Singalong, middleEngagement),
                (CrowdPreference.Chill, middleEngagement)); // 세 관객 재소집 — 이제 전체 반응 차이를 보여준다
            overlay?.SetDim(false);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                "카드 한 장은 모든 관객에게 서로 다르게 작용합니다.",
                "한 관객은 열광해도 다른 관객은 싫어할 수 있습니다. " +
                "아무 공연 카드나 사용해 세 관객의 반응을 비교하세요.",
                false);
        }

        void EnterCrossExplain(int gainedScore)
        {
            SetPhase(Phase.CrossExplain);
            _allowAllCards = false;
            overlay?.ShowMessage(
                "총 반응은 모든 관객의 반응을 합친 점수입니다.",
                $"방금 카드의 총 반응은 {gainedScore:+0;-0;0}입니다. " +
                "누구의 호응을 얻고 누구의 실망을 감수할지 판단하세요. (클릭해서 계속)",
                true);
        }

        void EnterExcitement()
        {
            SetPhase(Phase.Excitement);
            _expectedPref = CrowdPreference.Chill;
            _blockedHint = "지루해진 CHILL 관객이 떠나기 전에 CHILL 계열 카드로 관심을 되찾으세요.";

            // 앞 단계의 드로우 결과와 무관하게 CHILL 카드를 반드시 보장한다.
            SetupFixedHand();

            // 지루해진 CHILL 관객을 만든다
            var roster = AudienceRosterSystem.Instance;
            foreach (var m in roster.Members)
            {
                if (m.Preference != CrowdPreference.Chill) continue;
                roster.TrySetEngagement(m.Id, calmEngagement, AudienceChangeReason.RuntimeCommand, out _);
                break;
            }

            SpotlightPref(CrowdPreference.Chill, AudienceEngagementStage.Calm);
            overlay?.ShowMessage(
                "옷차림은 취향, 움직임은 현재 호응 상태를 뜻합니다.",
                "CHILL 관객의 움직임이 줄어 지루해하고 있습니다. " +
                "떠나기 전에 CHILL 계열 카드로 관심을 되찾으세요.",
                false);
        }

        void EnterCrowdChange()
        {
            SetPhase(Phase.CrowdChange);
            _allowAllCards = true;
            _hoverTaught = false;
            _hoverInspectionTimer = 0f;
            _crowdChangeHoverTargetId = default;

            bool added = AudienceRosterSystem.Instance.TryAdd(
                CrowdPreference.Singalong, excitedEngagement,
                AudienceJoinReason.RuntimeCommand, out var snapshot);

            if (added) _crowdChangeHoverTargetId = snapshot.Id;

            overlay?.SetDim(true);
            if (added && _presenter != null && _presenter.TryGetActor(snapshot.Id, out var actor))
                overlay?.Spotlight(actor);

            overlay?.ShowMessage(
                "새로운 SINGALONG 관객이 들어왔습니다.",
                "관객 구성이 바뀌면 높은 총 반응을 얻기 좋은 카드도 달라집니다. " +
                $"새 관객 위에 마우스를 올리거나 길게 누르고 {hoverInspectionDuration:0.#}초 동안 " +
                "옷차림·움직임·말풍선을 확인하세요.",
                false);
        }

        void EnterGameOverExplain()
        {
            SetPhase(Phase.GameOverExplain);
            _allowAllCards = false;
            overlay?.SetDim(false);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                "관심을 잃은 관객은 공연장을 떠납니다.",
                "모든 관객이 떠나면 즉시 GAME OVER입니다. 제한 시간이 끝났을 때 목표 점수에 " +
                "도달하지 못해도 실패합니다. " +
                "(클릭해서 계속)",
                true);
        }

        void EnterLightingHint()
        {
            SetPhase(Phase.LightingHint);
            overlay?.SetDim(false);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                "조명 색은 현재 가장 많은 관객 성향을 알려줍니다.",
                "지금 보라색 조명은 SINGALONG 관객이 가장 많다는 힌트입니다. " +
                "옷차림·움직임·말풍선과 함께 확인하세요. (클릭해서 계속)",
                true);
        }

        void EnterFeverIntro()
        {
            SetPhase(Phase.FeverIntro);
            _allowAllCards = true;
            overlay?.SetDim(false);
            overlay?.Spotlight(null);

            int interval = FeverSystem.HasInstance && FeverSystem.Instance.Config != null
                ? FeverSystem.Instance.Config.FeverComboInterval
                : 5;
            overlay?.ShowMessage(
                "카드의 총 반응이 1점 이상이면 COMBO가 이어집니다.",
                $"긍정적인 총 반응을 {interval}번 연속으로 만들면 FEVER TIME이 시작됩니다. " +
                "지금 공연 카드를 한 장 사용해 피버 보너스를 확인하세요.",
                false);

            if (FeverSystem.HasInstance) FeverSystem.Instance.ForceStart();
        }

        void EnterFeverExplain(int gainedScore)
        {
            SetPhase(Phase.FeverExplain);
            // The tutorial gate only blocks an early automatic activation.
            // Once the forced demonstration is complete, normal combo Fever is safe.
            if (FeverSystem.HasInstance)
                FeverSystem.Instance.SetAutomaticTriggerEnabled(true);
            overlay?.ShowMessage(
                "FEVER TIME에는 모든 카드가 금색으로 빛납니다.",
                $"피버 중에는 현재 관객 수에 따른 보너스 점수를 얻습니다. " +
                $"방금 획득 점수: {gainedScore:+0;-0;0}. (클릭해서 계속)",
                true);
        }

        void EnterSpecialIntro()
        {
            SetPhase(Phase.SpecialIntro);
            _allowAllCards = false;
            if (FeverSystem.HasInstance) FeverSystem.Instance.CancelFever();
            SetupSpecialHand();

            if (SpecialAudienceManager.HasInstance)
            {
                SpecialAudienceManager.Instance.StopSystem();
                SpecialAudienceManager.Instance.ForceSpawn(HeatStage.Singalong);
            }

            overlay?.SetDim(false);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                "특별 관객의 아이콘은 원하는 SPECIAL 카드를 뜻합니다.",
                "지금 특별 관객은 함께 노래할 퍼포먼스를 요청하고 있습니다. " +
                "요청 아이콘과 손패의 SPECIAL 카드를 비교하세요. (클릭해서 계속)",
                true);
        }

        void EnterSpecialUse()
        {
            SetPhase(Phase.SpecialUse);
            _allowAllCards = false;
            _blockedHint = "요청 아이콘과 같은 SPECIAL 카드를 골라 특별 관객에게 직접 전달하세요.";
            SetupSpecialHand();
            if (SpecialAudienceManager.HasInstance &&
                !SpecialAudienceManager.Instance.HasActiveRequest)
            {
                SpecialAudienceManager.Instance.ForceSpawn(HeatStage.Singalong);
            }

            overlay?.ShowMessage(
                "SPECIAL 카드는 특별 관객에게 직접 전달합니다.",
                "일반 카드처럼 무대 위로 던지지 마세요. 요청과 같은 SPECIAL 카드를 " +
                "특별 관객 위로 드래그해 놓으세요.",
                false);
        }

        void EnterSpecialExplain()
        {
            SetPhase(Phase.SpecialExplain);
            overlay?.ShowMessage(
                "SPECIAL HIT! 관객의 요청과 카드를 정확히 맞혔습니다.",
                "특별 관객에게 요청과 같은 SPECIAL 카드를 전달하면 강력한 보너스 점수를 얻습니다. " +
                "(클릭해서 계속)",
                true);
        }

        void EnterFinalIntro()
        {
            SetPhase(Phase.FinalIntro);
            _allowAllCards = true;
            if (SpecialAudienceManager.HasInstance)
                SpecialAudienceManager.Instance.StopSystem();
            SetupFixedHand();

            SetRoster(
                (CrowdPreference.Mosh, middleEngagement),
                (CrowdPreference.Mosh, middleEngagement),
                (CrowdPreference.Singalong, excitedEngagement),
                (CrowdPreference.Singalong, excitedEngagement),
                (CrowdPreference.Chill, calmEngagement),
                (CrowdPreference.Chill, calmEngagement));

            overlay?.SetDim(false);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                $"{finalDuration:0}초 안에 {finalScoreGoal:N0}점을 획득하세요.",
                "관객의 옷차림·움직임·조명·말풍선을 확인하고, 총 반응이 높은 카드를 선택하세요. " +
                "목표 점수에 도달하지 못하면 실패합니다. (클릭해서 시작)",
                true);
        }

        void EnterFinalRun()
        {
            SetPhase(Phase.FinalRun);
            _finalScore = 0;
            overlay?.ShowMessage("미니 공연 진행 중", "관객 구성을 읽고 총 반응이 높은 카드를 선택하세요.", false);
        }

        void FinishFinalRun()
        {
            bool success = _finalScore >= finalScoreGoal;

            if (success)
            {
                SetPhase(Phase.Complete);
                overlay?.ShowMessage(
                    "튜토리얼 완료! 관객의 맥락을 읽었습니다.",
                    "옷차림은 취향, 움직임은 현재 호응 상태, 조명은 다수 관객 성향을 알려줍니다. " +
                    "이 단서로 높은 총 반응을 만드세요. (클릭해서 진짜 공연 시작)",
                    true);
            }
            else
            {
                SetPhase(Phase.FinalFail);
                overlay?.ShowMessage(
                    "목표 점수에 도달하지 못했습니다.",
                    $"획득 점수 {_finalScore:N0} / 목표 {finalScoreGoal:N0}. " +
                    "관객 구성을 확인하고 총 반응이 양수가 되는 카드를 이어서 사용하세요. " +
                    "(클릭해서 재도전)",
                    true);
            }
        }

        // ---------------- 디버그 ----------------

        [ContextMenu("Debug/Start Tutorial")]
        void DebugStart()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Tutorial] Play Mode 전용."); return; }
            Save.SetBool(TutorialDoneKey, false);
            StartTutorial();
        }

        [ContextMenu("Debug/Reset Tutorial Done Flag")]
        void DebugResetFlag()
        {
            Save.SetBool(TutorialDoneKey, false);
            Debug.Log("[Tutorial] 완료 기록을 지웠습니다. 다음 공연 시작 시 다시 뜹니다.");
        }
    }
}
