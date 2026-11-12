using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    public readonly struct TutorialPlaybackChanged
    {
        public TutorialPlaybackChanged(bool isRunning) { IsRunning = isRunning; }
        public bool IsRunning { get; }
    }

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
    /// 기본 교육은 tutorial_done, Stage 2 특별 관객 교육은 tutorial_special_done으로 따로 저장한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialFlow : MonoBehaviour
    {
        static string L(string ko, string en) => GameLanguage.Pick(ko, en);
        [Header("체험형 교육 비교 옵션")]
        [SerializeField, Tooltip("기존 흐름을 유지하려면 끔. 켜면 총 반응·실패 설명을 다음 행동 단계에 합친다.")]
        bool compactExperience = false;
        int _lastRawAudienceScore;
        int _lastGainedScore;
        public const string TutorialDoneKey = "tutorial_done";
        public const string SpecialTutorialDoneKey = "tutorial_special_done";
        bool _specialLesson;
        int _specialLessonIndex;
        TutorialDragGuideUI _dragGuide;
        System.Func<int, bool> _previousUseFilter;
        System.Func<int, SpecialCardRequest, bool> _previousRequestFilter;

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

        [SerializeField, Tooltip(
            "투어에서는 Stage 1 기본 교육과 Stage 2 특별 관객 교육만 실행한다")]
        bool skipDuringTourStage = true;

        [Header("관객 몰입도 프리셋 (0~100)")]
        [SerializeField, Tooltip("지루(Calm) 상태로 만들 값")] float calmEngagement = 16f;
        [SerializeField, Tooltip("튜토리얼 기본 관객 값. 하(Calm)이면서 경고 바 기준보다 높게 둔다")] float middleEngagement = 40f;
        [SerializeField, Tooltip("흥분(Excited) 상태로 만들 값")] float excitedEngagement = 88f;

        [Header("최종 미니 공연")]
        [SerializeField, Tooltip("제한 시간(초)")] float finalDuration = 10f;
        [SerializeField, Tooltip("목표 총 반응 점수")] int finalScoreGoal = 400;

        [Header("관객 Hover 학습")]
        [SerializeField, Min(0.5f), Tooltip("테두리와 말풍선을 읽도록 Hover를 유지해야 하는 시간(초)")]
        float hoverInspectionDuration = 2f;

        /// <summary>튜토리얼 진행 중인가. (TutorialTips 가 이때는 조용히 있는다)</summary>
        public static bool IsRunning { get; private set; }
        public static bool IsOutfitFocusActive { get; private set; }

        Phase _phase = Phase.Idle;
        float _phaseTimer;
        float _pendingStartAt = -1f;
        bool _hasStartedThisScene;

        CrowdPreference _expectedPref;
        string _blockedHint = "";
        bool _allowAllCards;
        bool _hoverTaught;
        float _hoverInspectionTimer;
        AudienceId _inspectionTargetId;
        AudienceMemberActor _inspectionActor;

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
            _pendingStartAt = -1f;
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            CardInput.UseBlocked -= OnCardBlocked;

            if (overlay != null)
            {
                overlay.ContinueClicked -= OnContinueClicked;
                overlay.SkipClicked -= SkipTutorial;
            }

            if (IsRunning)
            {
                PlaytestMetrics.EndTutorial("abandoned");
                ReleaseControl(); // Disable 로도 훅이 남지 않게
            }
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
            if ((_phase == Phase.HoverHint || _phase == Phase.CrowdChange) && !_hoverTaught)
                BindInspectionGuide();

            switch (_phase)
            {
                case Phase.CrowdChange:
                    bool isInspectingNewAudience =
                        !_hoverTaught &&
                        _inspectionTargetId.IsValid &&
                        _hoverController != null &&
                        _hoverController.RevealedActor != null &&
                        _hoverController.RevealedActor.BoundId == _inspectionTargetId;

                    if (isInspectingNewAudience)
                    {
                        _hoverInspectionTimer += Time.unscaledDeltaTime;
                        if (_hoverInspectionTimer >= hoverInspectionDuration)
                        {
                            _hoverTaught = true;
                            _dragGuide?.Hide();
                            overlay?.ShowMessage(
                                L("새 관객의 정보를 확인했습니다.", "You checked the new fan."),
                                L("관객 구성이 바뀌면 호응을 크게 이끌어낼 카드도 달라집니다. 새 관객의 취향을 기억하세요. (클릭해서 계속)",
                                    "As the crowd changes, so does your best card. Remember what the new fan likes. (Click to continue)"),
                                true);
                        }
                    }
                    else if (!_hoverTaught)
                    {
                        _hoverInspectionTimer = 0f;
                    }
                    break;

                case Phase.HoverHint:
                    bool isInspecting = _inspectionTargetId.IsValid && _hoverController != null &&
                        _hoverController.RevealedActor != null &&
                        _hoverController.RevealedActor.BoundId == _inspectionTargetId;
                    if (!_hoverTaught && isInspecting)
                    {
                        _hoverInspectionTimer += Time.unscaledDeltaTime;
                        if (_hoverInspectionTimer >= hoverInspectionDuration)
                        {
                            _hoverTaught = true;
                            _dragGuide?.Hide();
                            overlay?.ShowMessage(
                                L("관객의 힌트를 확인했습니다.", "You spotted the crowd clues."),
                                L("테두리 색은 선호하는 카드 계열을, 말풍선은 원하는 행동과 현재 기분을 알려줍니다. (클릭해서 계속)",
                                    "The outline shows their favorite card style. The bubble shows what they want and how they feel. (Click to continue)"),
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
                    // 요청 타이머는 Manager가 보류한다. 반복 재생성하지 않는다.
                    break;

                case Phase.FinalRun:
                    float remain = Mathf.Max(0f, finalDuration - _phaseTimer);
                    overlay?.FlashSub(
                        L($"남은 시간 {remain:0}초 · 총 반응 {_finalScore:+0;-0;0} / {finalScoreGoal}",
                          $"{remain:0}s left · Response {_finalScore:+0;-0;0} / {finalScoreGoal}"));
                    if (remain <= 0f) FinishFinalRun();
                    break;
            }
        }

        // ---------------- 시작 / 종료 ----------------

        /// <summary>[테스트 전용] true 면 자동 실행하지 않는다 (E2E 스크립트가 켠다).</summary>
        public static bool SuppressForTests;

        [SerializeField, Tooltip("에디터·개발 빌드에서는 완료 기록과 상관없이 Stage 1/2의 해당 교육을 띄운다")]
        bool forceInDebugBuilds = true;

        /// <summary>
        /// Stage 1 기본 교육 / Stage 2 특별 관객 교육의 독립적인 완료 기록을 확인한다.
        /// 에디터·개발 빌드는 forceInDebugBuilds 로 항상. 투어 밖(Main 직접 실행)은 예전처럼 매번.
        /// </summary>
        /// <summary>이 씬의 첫 공연 시작 때 튜토리얼이 자동으로 뜨는지. 시작 연출 순서 판단용.</summary>
        public bool WillAutoRunOnStart =>
            autoRunOnFirstPlay && !SuppressForTests && !_hasStartedThisScene && ShouldAutoRunInTour();

        bool ShouldAutoRunInTour()
        {
            StageDefinition stage = StageRuntimeDirector.CurrentStage;
            if (stage == null) return true; // 투어가 아니다
            if (stage.StageId != "stage_01" && stage.StageId != "stage_02") return false;

            bool force = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            force = forceInDebugBuilds;
#endif
            return !skipDuringTourStage || force || !Save.GetBool(
                stage.StageId == "stage_02" ? SpecialTutorialDoneKey : TutorialDoneKey, false);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver)
                _pendingStartAt = -1f;
            // 첫 공연 자동 실행 (투어에서는 첫 스테이지 첫 플레이만 — ShouldAutoRunInTour)
            bool tourStageActive = SuppressForTests || !ShouldAutoRunInTour();
            if (autoRunOnFirstPlay &&
                !tourStageActive &&
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
            _specialLesson = StageRuntimeDirector.CurrentStage != null &&
                             StageRuntimeDirector.CurrentStage.StageId == "stage_02";
            PlaytestMetrics.StartTutorial(_specialLesson ? "stage_02" : "stage_01");
            TakeControl();
            EventBus.Raise(new TutorialPlaybackChanged(true));
            if (_specialLesson) EnterSpecialIntro();
            else EnterIntro();
        }

        /// <summary>스킵. 완료로 기록해 다음부터 뜨지 않는다.</summary>
        public void SkipTutorial() => EndTutorial(markDone: true, restartRun: true, skipped: true);

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

            _previousUseFilter = CardInput.UseFilter;
            _previousRequestFilter = CardInput.RequestFilter;
            CardInput.UseFilter = FilterCard;
            CardInput.RequestFilter = FilterRequest;
            _allowAllCards = false;
            if (!_specialLesson) SetupFixedHand();

            overlay?.SetSkipVisible(true);
        }

        void ReleaseControl()
        {
            EndInspection();
            IsRunning = false;
            IsOutfitFocusActive = false;

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
            if (CardInput.UseFilter == FilterCard) CardInput.UseFilter = _previousUseFilter;
            if (CardInput.RequestFilter == FilterRequest) CardInput.RequestFilter = _previousRequestFilter;
            if (_dragGuide != null) _dragGuide.Hide();
            overlay?.SetSpecialLessonLayout(false);
            overlay?.HideAll();
            overlay?.SetSkipVisible(false);
            EventBus.Raise(new TutorialPlaybackChanged(false));
        }

        void EndTutorial(bool markDone, bool restartRun, bool skipped = false)
        {
            if (!IsRunning) return;
            PlaytestMetrics.EndTutorial(skipped ? "skipped" : markDone ? "completed" : "abandoned");
            _pendingStartAt = -1f;
            _phase = Phase.Idle;
            ReleaseControl();

            // 완료(끝까지 보거나 건너뜀)를 기록해 두면 투어 첫 스테이지에서 다시 뜨지 않는다 (에디터·개발 빌드는 forceInDebugBuilds 로 항상)
            if (markDone) Save.SetBool(_specialLesson ? SpecialTutorialDoneKey : TutorialDoneKey, true);

            if (restartRun && GameManager.HasInstance && GameManager.Instance.IsPlaying)
            {
                // Ready 이벤트에서 덱·관객·콤보 자원·공연 결과 집계를 초기화하고,
                // Playing 이벤트에서 타이머·호응도·공연 시스템을 새로 시작한다.
                GameManager.Instance.ResetGame();
                // 미뤄 둔 증강 시작 연출이 있으면 연출이 끝난 뒤 그쪽이 공연을 시작한다.
                if (!PerformanceIntroDirector.TryRunAfterTutorial())
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

        static readonly (HeatStage stage, string cardId, string cardName, string effect)[] SpecialLessons =
        {
            (HeatStage.Chill, "response_call", "호응 유도",
                "몰입도가 낮은 관객을 회복시켜요. 관심이 식은 관객이 많을수록 점수가 커져요."),
            (HeatStage.Singalong, "pass_mic", "마이크 넘기기",
                "모든 관객의 몰입도를 높여요. 관객이 많이 모였을수록 점수가 커져요."),
            (HeatStage.Mosh, "open_mosh_pit", "모쉬핏 열어!",
                "이미 신난 관객의 열기를 점수로 바꿔요. 몰입도를 더 올리는 카드는 아니에요.")
        };
        readonly CardDefinition[] _specialCards = new CardDefinition[SpecialLessons.Length];

        bool FindSpecialLessonCards()
        {
            if (!CardSystem.HasInstance) return false;
            var config = CardSystem.Instance.Config;
            var catalog = CardCatalog.LoadDefault();
            for (int i = 0; i < SpecialLessons.Length; i++)
            {
                var lesson = SpecialLessons[i];
                CardDefinition card = null;
                if (config != null)
                {
                    foreach (var entry in config.CardPool)
                    {
                        if (entry == null || !entry.IsUsable || entry.Prefab.Id != lesson.cardId) continue;
                        card = entry.Prefab;
                        break;
                    }
                }
                // 카드가 실제 투어 덱에 없어도 연습 손패로만 빌려 쓴다.
                if (card == null && catalog != null) catalog.TryGetCard(lesson.cardId, out card);
                if (card == null || card.Role != CardRole.Special || card.TargetStage != lesson.stage ||
                    card.SpecialTargetEffect == null || !card.SpecialTargetEffect.TryValidate(out _))
                {
                    Debug.LogWarning($"[Tutorial] 연습 카드 {lesson.cardId}의 설정을 확인하세요.");
                    return false;
                }
                _specialCards[i] = card;
            }
            return true;
        }

        /// <summary>교체 중 빈 로스터가 되지 않도록 기존 관객 한 명 이상을 첫 입장까지 남긴다.</summary>
        void SetRoster(params (CrowdPreference pref, float engagement)[] members)
        {
            var roster = AudienceRosterSystem.Instance;

            var previous = new List<AudienceId>();
            foreach (var m in roster.Members) previous.Add(m.Id);

            int nextRemoval = 0;
            while (previous.Count - nextRemoval > 1 && roster.Capacity - roster.Count < members.Length)
                roster.TryRemove(previous[nextRemoval++], AudienceDepartureReason.RuntimeRemoval, out _);

            _spawned.Clear();
            foreach (var (pref, engagement) in members)
            {
                // 정원까지 채우는 연습 구성이라면 새 관객 입장 후에 마지막 기존 관객도 정리한다.
                if (_spawned.Count > 0 && roster.Count >= roster.Capacity && nextRemoval < previous.Count)
                    roster.TryRemove(previous[nextRemoval++], AudienceDepartureReason.RuntimeRemoval, out _);
                if (roster.TryAdd(pref, engagement, AudienceJoinReason.RuntimeCommand, out var snapshot))
                    _spawned.Add(snapshot.Id);
            }

            if (_spawned.Count == 0) return;
            for (int i = nextRemoval; i < previous.Count; i++)
                roster.TryRemove(previous[i], AudienceDepartureReason.RuntimeRemoval, out _);
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

        void SpotlightPref(CrowdPreference pref, AudienceEngagementStage? stage = null, bool outfitFocus = false)
        {
            overlay?.SetDim(true, outfitFocus);
            overlay?.Spotlight(FindActor(pref, stage), outfitFocus);
        }

        // ---------------- 카드 게이트 ----------------

        bool FilterCard(int handIndex)
        {
            if (_phase == Phase.HoverHint || _phase == Phase.CrowdChange) return false;
            if (_specialLesson && _phase != Phase.SpecialUse) return false;
            if (_allowAllCards) return true;
            if (!CardSystem.HasInstance) return true;

            var card = CardSystem.Instance.GetCard(handIndex);
            if (card == null) return false;

            if (_phase == Phase.SpecialUse)
            {
                return card.Role == CardRole.Special &&
                       card.TargetStage == SpecialLessons[_specialLessonIndex].stage;
            }

            // 손패에 정답 카드가 없으면 소프트락 방지를 위해 전부 허용한다
            if (!HandHasMatch()) return true;

            return MatchesPracticeCard(card);
        }

        bool FilterRequest(int handIndex, SpecialCardRequest request)
        {
            if (_phase == Phase.HoverHint || _phase == Phase.CrowdChange) return false;
            if (!_specialLesson) return true;
            return _phase == Phase.SpecialUse && request.IsActive &&
                   request.RequestedStage == SpecialLessons[_specialLessonIndex].stage &&
                   SpecialAudienceManager.HasInstance &&
                   SpecialAudienceManager.Instance.HasActiveRequest &&
                   SpecialAudienceManager.Instance.CurrentRequestType == request.RequestedStage;
        }

        bool HandHasMatch()
        {
            var system = CardSystem.Instance;
            for (int i = 0; i < system.HandCount; i++)
            {
                var card = system.GetCard(i);
                if (MatchesPracticeCard(card))
                    return true;
            }
            return false;
        }

        bool MatchesPracticeCard(CardDefinition card) =>
            card != null && (card.Role == CardRole.Normal || card.Role == CardRole.Special) &&
            card.TargetPreference == _expectedPref;

        void OnCardBlocked(int handIndex)
        {
            if (IsRunning) PlaytestMetrics.RecordTutorialBlock();
            if (!IsRunning || string.IsNullOrEmpty(_blockedHint)) return;
            overlay?.FlashSub(_blockedHint);
        }

        // ---------------- 진행 ----------------

        void OnContinueClicked()
        {
            if (!IsRunning) return;
            if ((_phase == Phase.HoverHint || _phase == Phase.CrowdChange) && !_hoverTaught) return;
            switch (_phase)
            {
                case Phase.Intro:        EnterPrefMosh(); break;
                case Phase.HoverHint:    EnterCrossUse(); break;
                case Phase.CrossExplain: EnterExcitement(); break;
                case Phase.GameOverExplain: EnterCrowdChange(); break;
                case Phase.CrowdChange:  EnterLightingHint(); break;
                case Phase.LightingHint: EnterFeverIntro(); break;
                case Phase.FeverExplain: EnterFinalIntro(); break;
                case Phase.SpecialIntro: EnterSpecialUse(); break;
                case Phase.SpecialExplain: AdvanceSpecialLesson(); break;
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
                case Phase.CrossUse:
                    _lastRawAudienceScore = e.RawAudienceScore;
                    _lastGainedScore = e.GainedScore;
                    if (compactExperience) EnterExcitement();
                    else EnterCrossExplain(e.RawAudienceScore, e.GainedScore);
                    break;
                case Phase.Excitement:
                    if (compactExperience) EnterCrowdChange();
                    else EnterGameOverExplain();
                    break;
                case Phase.FeverIntro:    EnterFeverExplain(e.GainedScore); break;
                case Phase.SpecialUse:
                    if (e.IsSpecialHit && e.CardId == SpecialLessons[_specialLessonIndex].cardId)
                        EnterSpecialExplain();
                    else
                    {
                        CardSystem.Instance.SetHand(new[] { _specialCards[_specialLessonIndex] });
                        overlay?.FlashSub(
                            L("요청 아이콘이 뜬 관객의 몸 위에 카드를 놓으세요.", "Drop the card directly on the fan with the request icon."));
                    }
                    break;
                case Phase.FinalRun:      _finalScore += e.GainedScore; break;
            }
        }

        void SetPhase(Phase phase)
        {
            EndInspection();
            _phase = phase;
            _phaseTimer = 0f;
            IsOutfitFocusActive = phase is Phase.Intro or Phase.PrefMosh or Phase.PrefSingalong or
                Phase.PrefChill or Phase.HoverHint or Phase.CrowdChange;
        }

        // ---------------- 각 단계 ----------------

        void EnterIntro()
        {
            SetPhase(Phase.Intro);
            SetRoster((CrowdPreference.Mosh, excitedEngagement)); // 첫 관객의 옷차림을 선명하게
            SpotlightPref(CrowdPreference.Mosh, outfitFocus: true);
            overlay?.ShowMessage(
                L("관객을 읽고, 알맞은 행동 카드를 사용하세요.", "Read the crowd and play the right card."),
                L("이 게임은 관객의 복장과 상태를 살펴 호응을 이끌어내는 공연 게임입니다. 옷차림은 취향을, 움직임은 현재 신난 정도를 보여줍니다. (클릭해서 계속)",
                  "Win the crowd over by watching their outfits and movement. Outfits tell you what they like; movement shows how excited they are. (Click to continue)"),
                true);
        }

        void EnterPrefMosh()
        {
            SetPhase(Phase.PrefMosh);
            _expectedPref = CrowdPreference.Mosh;
            _blockedHint = L("MOSH 관객은 격렬한 연주를 좋아합니다. 기타 솔로 카드를 골라보세요.", "MOSH fans like a heavy sound. Try Guitar Solo.");
            SpotlightPref(CrowdPreference.Mosh, outfitFocus: true);
            overlay?.ShowMessage(
                L("검은 옷과 밴드 패치 = MOSH 관객", "Black clothes and band patches = MOSH"),
                L("MOSH 관객은 거칠고 강한 연주에 크게 호응합니다. 기타 솔로 카드를 위로 드래그해 사용하세요.",
                  "MOSH fans love loud, heavy playing. Drag Guitar Solo up to play it."),
                false);
        }

        void EnterPrefSingalong()
        {
            SetPhase(Phase.PrefSingalong);
            _expectedPref = CrowdPreference.Singalong;
            _blockedHint = L("SINGALONG 관객은 함께 노래하고 싶어 합니다. 손 머리 위로! 카드를 골라보세요.", "SINGALONG fans want to join in. Try Hands Up!");
            SetRoster((CrowdPreference.Singalong, excitedEngagement)); // Mosh 퇴장, Singalong 등장
            SpotlightPref(CrowdPreference.Singalong, outfitFocus: true);
            overlay?.ShowMessage(
                L("트레이닝복을 입은 관객 = SINGALONG 관객", "Tracksuits = SINGALONG"),
                L("SINGALONG 관객은 함께 노래하고 공연에 참여하는 행동을 좋아합니다. 손 머리 위로! 카드를 사용하세요.",
                  "SINGALONG fans love joining the show. Play Hands Up!"),
                false);
        }

        void EnterPrefChill()
        {
            SetPhase(Phase.PrefChill);
            _expectedPref = CrowdPreference.Chill;
            _blockedHint = L("CHILL 관객은 여유로운 공연을 좋아합니다. 청록색 CHILL 계열 카드를 골라보세요.", "CHILL fans like a relaxed show. Try a teal CHILL card.");
            SetRoster((CrowdPreference.Chill, excitedEngagement)); // Singalong 퇴장, Chill 등장
            SpotlightPref(CrowdPreference.Chill, outfitFocus: true);
            overlay?.ShowMessage(
                L("차분한 복장 = CHILL 관객", "Relaxed outfits = CHILL"),
                L("CHILL 관객은 편안하고 여유로운 공연에 크게 호응합니다. 이번에는 옷차림을 단서로 알맞은 카드를 골라보세요.",
                  "CHILL fans love a relaxed show. Read their outfit and choose a card."),
                false);
        }

        void EnterHoverHint()
        {
            SetPhase(Phase.HoverHint);
            _hoverTaught = false;
            _hoverInspectionTimer = 0f;
            // 직전 카드의 점프·반동이 남은 관객 대신 새 관객으로 확인을 연습한다.
            _blockedHint = L("카드는 잠시 기다려주세요. 새 관객 위에서 2초 동안 마음을 확인하세요.", "Hold off on cards. Inspect the new fan first.");
            SetRoster((CrowdPreference.Chill, excitedEngagement));
            if (_spawned.Count > 0) _inspectionTargetId = _spawned[0];
            BindInspectionGuide();
            overlay?.ShowMessage(
                L("새 관객 위에 마우스를 올려보세요.", "Hover over the new fan."),
                L($"모바일에서는 길게 누르세요.\n{hoverInspectionDuration:0.#}초 동안 유지하며 테두리색과 말풍선으로 관객의 마음을 확인하세요.",
                  $"On mobile, press and hold.\nHold for {hoverInspectionDuration:0.#}s to read the outline and speech bubble."),
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
                L("카드 한 장은 모든 관객에게 서로 다르게 작용합니다.", "One card, different reactions."),
                L("한 관객은 열광해도 다른 관객은 싫어할 수 있습니다. 아무 공연 카드나 사용해 세 관객의 반응을 비교하세요.",
                  "One fan might love what another hates. Play any performance card and compare the three reactions."),
                false);
        }

        void EnterCrossExplain(int rawAudienceScore, int gainedScore)
        {
            SetPhase(Phase.CrossExplain);
            _allowAllCards = false;
            overlay?.ShowMessage(
                L("관객 반응의 합이 양수면 콤보가 이어집니다.", "A positive total keeps the combo going."),
                L($"방금 관객 반응 합계 {rawAudienceScore:+0;-0;0}, 최종 획득 점수 {gainedScore:+0;-0;0}. 누구의 호응을 얻고 누구의 실망을 감수할지 판단하세요. (클릭해서 계속)",
                  $"Crowd response: {rawAudienceScore:+0;-0;0}. Final score: {gainedScore:+0;-0;0}. Decide who to win over. (Click to continue)"),
                true);
        }

        void EnterExcitement()
        {
            SetPhase(Phase.Excitement);
            _allowAllCards = false;
            _expectedPref = CrowdPreference.Chill;
            _blockedHint = L("지루해진 CHILL 관객이 떠나기 전에 CHILL 계열 카드로 관심을 되찾으세요.", "Win the bored CHILL fan back with a CHILL card before they leave.");

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
                L("옷차림은 취향, 움직임은 현재 호응 상태를 뜻합니다.", "Outfits show taste. Movement shows engagement."),
                (compactExperience ? L($"방금 반응 합계 {_lastRawAudienceScore:+0;-0;0}, 획득 {_lastGainedScore:+0;-0;0}점. ",
                                       $"Response: {_lastRawAudienceScore:+0;-0;0}. Score: {_lastGainedScore:+0;-0;0}. ") : "") +
                L("움직임이 줄어든 CHILL 관객에게 CHILL 카드를 내세요. ", "Play a CHILL card for the fan who's stopped moving. ") +
                (compactExperience ? L("모든 관객이 떠나면 즉시 실패합니다.", "If everyone leaves, the show ends.")
                                   : L("떠나기 전에 관심을 되찾으세요.", "Win them back before they leave.")),
                false);
        }

        void EnterCrowdChange()
        {
            SetPhase(Phase.CrowdChange);
            _allowAllCards = true;
            _hoverTaught = false;
            _hoverInspectionTimer = 0f;
            _blockedHint = L("새로 들어온 SINGALONG 관객 위에서 2초 동안 마음을 확인하세요.", "Inspect the new SINGALONG fan first.");

            bool added = AudienceRosterSystem.Instance.TryAdd(
                CrowdPreference.Singalong, excitedEngagement,
                AudienceJoinReason.RuntimeCommand, out var snapshot);

            if (added) _inspectionTargetId = snapshot.Id;
            BindInspectionGuide();

            overlay?.ShowMessage(
                L("새로운 SINGALONG 관객이 들어왔습니다.", "A new SINGALONG fan walked in."),
                L($"관객 구성이 바뀌면 높은 총 반응을 얻기 좋은 카드도 달라집니다. 새 관객 위에 마우스를 올리거나 길게 누르고 {hoverInspectionDuration:0.#}초 동안 옷차림·움직임·말풍선을 확인하세요.",
                  $"New fans change which cards work best. Hover or hold on the newcomer for {hoverInspectionDuration:0.#}s to inspect them."),
                false);
        }

        void BindInspectionGuide()
        {
            if (_inspectionActor != null || !_inspectionTargetId.IsValid || _presenter == null || overlay == null)
                return;
            if (!_presenter.TryGetActor(_inspectionTargetId, out var actor)) return;
            _inspectionActor = actor;
            actor.SetTutorialInspectionPose(true);
            overlay.SetDim(true, outfitFocus: true);
            overlay.Spotlight(actor, outfitFocus: true);
            if (_dragGuide == null) _dragGuide = overlay.gameObject.AddComponent<TutorialDragGuideUI>();
            _dragGuide.ShowInspection(overlay, actor, hoverInspectionDuration);
        }

        void EndInspection()
        {
            if (_inspectionActor != null && _inspectionActor.BoundId == _inspectionTargetId)
                _inspectionActor.SetTutorialInspectionPose(false);
            _inspectionActor = null;
            _inspectionTargetId = default;
            _dragGuide?.Hide();
        }

        void EnterGameOverExplain()
        {
            SetPhase(Phase.GameOverExplain);
            _allowAllCards = false;
            overlay?.SetDim(false);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                L("관심을 잃은 관객은 공연장을 떠납니다.", "Fans leave when they lose interest."),
                L("모든 관객이 떠나면 즉시 GAME OVER입니다. 제한 시간이 끝났을 때 목표 점수에 도달하지 못해도 실패합니다. (클릭해서 계속)",
                  "If everyone leaves, it's game over. You also lose if time runs out before you meet the score goal. (Click to continue)"),
                true);
        }

        void EnterLightingHint()
        {
            SetPhase(Phase.LightingHint);
            overlay?.SetDim(false);
            overlay?.Spotlight(null);
            overlay?.ShowMessage(
                L("조명 색은 현재 가장 많은 관객 성향을 알려줍니다.", "The lights show the biggest fan group."),
                L("지금 보라색 조명은 SINGALONG 관객이 가장 많다는 힌트입니다. 옷차림·움직임·말풍선과 함께 확인하세요. (클릭해서 계속)",
                  "Purple means most fans are SINGALONG. Use the lights alongside outfits, movement and bubbles. (Click to continue)"),
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
                L("카드의 총 반응이 1점 이상이면 COMBO가 이어집니다.", "Score at least 1 response point to keep your COMBO."),
                L($"긍정적인 총 반응을 {interval}번 연속으로 만들면 FEVER TIME이 시작됩니다. 지금 공연 카드를 한 장 사용해 피버 보너스를 확인하세요.",
                  $"Get {interval} positive reactions in a row to start FEVER TIME. Play a card to see the bonus."),
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
                L("FEVER TIME에는 모든 카드가 금색으로 빛납니다.", "Cards glow gold during FEVER TIME."),
                L($"피버 중에는 현재 관객 수에 따른 보너스 점수를 얻습니다. 방금 획득 점수: {gainedScore:+0;-0;0}. (클릭해서 계속)",
                  $"A bigger crowd means a bigger Fever bonus. You just scored {gainedScore:+0;-0;0}. (Click to continue)"),
                true);
        }

        void EnterSpecialIntro()
        {
            SetPhase(Phase.SpecialIntro);
            _specialLessonIndex = 0;
            _allowAllCards = false;
            if (FeverSystem.HasInstance) FeverSystem.Instance.CancelFever();
            if (!SpecialAudienceManager.HasInstance || SpecialAudienceManager.Instance.Config == null ||
                overlay == null || !FindSpecialLessonCards() || !PrepareSpecialLesson())
            {
                AbortSpecialLesson();
                return;
            }

            overlay.SetSpecialLessonLayout(true);
            overlay.SetDim(false);
            overlay.Spotlight(null);
            overlay.ShowMessage(
                L("특별 관객의 요청에 응답해 보세요.", "Answer a special fan's request."),
                L("요청 아이콘과 같은 SPECIAL 카드를 관객에게 전달해 세 가지 효과를 익혀요.",
                  "Drop a matching SPECIAL card on the fan to learn all three effects."),
                true);
        }

        bool PrepareSpecialLesson()
        {
            var manager = SpecialAudienceManager.Instance;
            var lesson = SpecialLessons[_specialLessonIndex];
            manager.StopSystem();
            switch (lesson.stage)
            {
                case HeatStage.Chill:
                    SetRoster((CrowdPreference.Chill, calmEngagement), (CrowdPreference.Chill, middleEngagement),
                        (CrowdPreference.Mosh, middleEngagement));
                    break;
                case HeatStage.Singalong:
                    SetRoster((CrowdPreference.Chill, middleEngagement), (CrowdPreference.Singalong, middleEngagement),
                        (CrowdPreference.Mosh, middleEngagement), (CrowdPreference.Singalong, excitedEngagement));
                    break;
                case HeatStage.Mosh:
                    SetRoster((CrowdPreference.Mosh, excitedEngagement), (CrowdPreference.Singalong, excitedEngagement));
                    break;
            }
            CardSystem.Instance.SetHand(new[] { _specialCards[_specialLessonIndex] });
            manager.ForceSpawn(lesson.stage);
            manager.SetTutorialHold(true);
            var target = SpecialAudience.CurrentDropTarget;
            return manager.HasActiveRequest && target != null && target.HitCollider != null;
        }

        void AbortSpecialLesson()
        {
            Debug.LogWarning("[Tutorial] 특별 관객 교육 연결을 확인하세요. 완료 기록 없이 본 공연을 시작합니다.");
            EndTutorial(markDone: false, restartRun: true);
        }

        void EnterSpecialUse()
        {
            SetPhase(Phase.SpecialUse);
            var lesson = SpecialLessons[_specialLessonIndex];
            _blockedHint = L("요청 아이콘이 뜬 관객의 몸에 카드를 겹친 뒤 놓으세요.", "Drop the card directly on the fan with the request icon.");
            string cardName = L(lesson.cardName, EnglishSpecialName(lesson.stage));
            overlay.ShowMessage(
                $"{_specialLessonIndex + 1}/{SpecialLessons.Length}  {cardName}",
                L($"{lesson.stage.ToString().ToUpperInvariant()} 특별 관객에게 카드를 끌어다 놓으세요.",
                  $"Drop the card on the {lesson.stage.ToString().ToUpperInvariant()} special fan."),
                false);
            if (_dragGuide == null) _dragGuide = overlay.gameObject.AddComponent<TutorialDragGuideUI>();
            _dragGuide.Show(overlay, lesson.stage);
        }

        void EnterSpecialExplain()
        {
            SetPhase(Phase.SpecialExplain);
            var lesson = SpecialLessons[_specialLessonIndex];
            bool last = _specialLessonIndex == SpecialLessons.Length - 1;
            overlay?.ShowMessage(
                L($"성공! {lesson.cardName}", $"Nice! {EnglishSpecialName(lesson.stage)}"),
                L(lesson.effect, EnglishSpecialEffect(lesson.stage)) + (last ?
                    L("\n본 공연에서는 요청 게이지가 끝나기 전에 전달하세요.", "\nIn a real show, answer before the request expires.") : ""),
                true);
        }

        static string EnglishSpecialName(HeatStage type) => type switch
        {
            HeatStage.Chill => "Get a Response",
            HeatStage.Singalong => "Pass the Mic",
            _ => "Open the Mosh Pit!"
        };

        static string EnglishSpecialEffect(HeatStage type) => type switch
        {
            HeatStage.Chill => "Revives disengaged fans. The more fans are losing interest, the more it scores.",
            HeatStage.Singalong => "Raises everyone's engagement. Scores more with a bigger crowd.",
            _ => "Turns the energy of excited fans into points. It doesn't raise engagement further."
        };

        void AdvanceSpecialLesson()
        {
            if (_specialLessonIndex == SpecialLessons.Length - 1)
            {
                EndTutorial(markDone: true, restartRun: true);
                return;
            }

            _specialLessonIndex++;
            if (PrepareSpecialLesson()) EnterSpecialUse();
            else AbortSpecialLesson();
        }

        void EnterFinalIntro()
        {
            if (FeverSystem.HasInstance) FeverSystem.Instance.CancelFever();
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
                L($"{finalDuration:0}초 안에 {finalScoreGoal:N0}점을 획득하세요.", $"Score {finalScoreGoal:N0} points in {finalDuration:0}s."),
                L("관객의 옷차림·움직임·조명·말풍선을 확인하고, 총 반응이 높은 카드를 선택하세요. 목표 점수에 도달하지 못하면 실패합니다. (클릭해서 시작)",
                  "Read the outfits, movements, lights and bubbles. Choose cards the whole crowd will like. (Click to start)"),
                true);
        }

        void EnterFinalRun()
        {
            SetPhase(Phase.FinalRun);
            _finalScore = 0;
            overlay?.ShowMessage(L("미니 공연 진행 중", "Mini-show in progress"),
                L("관객 구성을 읽고 총 반응이 높은 카드를 선택하세요.", "Read the crowd and aim for a strong total response."), false);
        }

        void FinishFinalRun()
        {
            bool success = _finalScore >= finalScoreGoal;

            if (success)
            {
                SetPhase(Phase.Complete);
                overlay?.ShowMessage(
                    L("튜토리얼 완료! 관객의 맥락을 읽었습니다.", "Tutorial complete! You read the crowd."),
                    L("옷차림은 취향, 움직임은 현재 호응 상태, 조명은 다수 관객 성향을 알려줍니다. 이 단서로 높은 총 반응을 만드세요. (클릭해서 진짜 공연 시작)",
                      "Outfits show taste, movement shows engagement, and lighting shows the majority. Use those clues in the real show. (Click to start)"),
                    true);
            }
            else
            {
                SetPhase(Phase.FinalFail);
                overlay?.ShowMessage(
                    L("목표 점수에 도달하지 못했습니다.", "You fell short of the goal."),
                    L($"획득 점수 {_finalScore:N0} / 목표 {finalScoreGoal:N0}. 관객 구성을 확인하고 총 반응이 양수가 되는 카드를 이어서 사용하세요. (클릭해서 재도전)",
                      $"Score: {_finalScore:N0} / {finalScoreGoal:N0}. Read the crowd and keep playing cards with a positive total response. (Click to retry)"),
                    true);
            }
        }

        // ---------------- 디버그 ----------------

        [ContextMenu("Debug/Start Tutorial")]
        void DebugStart()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Tutorial] Play Mode 전용."); return; }
            StartTutorial();
        }

        [ContextMenu("Debug/Reset Tutorial Done Flag")]
        void DebugResetFlag()
        {
            Save.SetBool(TutorialDoneKey, false);
            Save.SetBool(SpecialTutorialDoneKey, false);
            Debug.Log("[Tutorial] 완료 기록을 지웠습니다. 다음 공연 시작 시 다시 뜹니다.");
        }
    }
}
