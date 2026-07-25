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
    /// 진행: Intro → 성향 3연습(Mosh 정답 공개 → Singalong 절반 힌트 → Chill 자율)
    ///       → 교차 반응 → 상태 읽기(지루한 관객 구하기) → 관객 교체 → 30초 최종 미니 공연 → 완료
    ///
    /// 다른 시스템을 제어하는 방법 (전부 기존 공개 API·한 줄 훅):
    ///   관객 고정      : AudienceRosterSystem.TryAdd/TryRemove/TrySetEngagement
    ///                    + SuppressNaturalArrivals(유입 정지) + SuppressEngagementDecay(개별 몰입도 감소 정지)
    ///   손패 고정      : CardSystem.SetHand (성향별 카드 1장씩, 총 3장)
    ///   카드 제한      : CardInput.UseFilter (거부된 카드는 소모 없이 손패로 복귀)
    ///   호응도 정지    : Hype.SetDecayPaused(true)
    ///   특별 관객 정지 : SpecialAudienceManager.StopSystem()
    ///   위기 정지      : NearbyConcertCrisisDirector.enabled = false
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
            CrossUse, CrossExplain,
            Excitement,
            CrowdChange,
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
        [SerializeField, Tooltip("제한 시간(초)")] float finalDuration = 30f;
        [SerializeField, Tooltip("목표 총 반응 점수")] int finalScoreGoal = 100;
        [SerializeField, Tooltip("허용 이탈 인원")] int finalMaxDepartures = 1;

        /// <summary>튜토리얼 진행 중인가. (TutorialTips 가 이때는 조용히 있는다)</summary>
        public static bool IsRunning { get; private set; }

        Phase _phase = Phase.Idle;
        float _phaseTimer;
        float _pendingStartAt = -1f;

        CrowdPreference _expectedPref;
        string _blockedHint = "";
        bool _allowAllCards;

        // 최종 미니 공연 집계
        int _finalScore;
        int _finalDepartures;

        // 복구용
        NearbyConcertCrisisDirector _crisis;
        AudienceRosterPresenter _presenter;
        bool _crisisWasEnabled;

        void Awake()
        {
            if (overlay == null) overlay = FindFirstObjectByType<TutorialOverlayUI>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<AudienceDeparted>(OnAudienceDeparted);
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
            EventBus.Unsubscribe<AudienceDeparted>(OnAudienceDeparted);
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
                    // 새 관객을 2초는 관찰하게 한 뒤에 진행 버튼을 살린다
                    if (_phaseTimer >= 2f && overlay != null)
                        overlay.ShowMessage("새로운 관객이 들어왔습니다!",
                            "관객이 바뀌면 좋은 카드도 바뀝니다. (클릭해서 계속)", true);
                    break;

                case Phase.FinalRun:
                    float remain = Mathf.Max(0f, finalDuration - _phaseTimer);
                    overlay?.FlashSub(
                        $"남은 시간 {remain:0}초 · 총 반응 {_finalScore:+0;-0;0} / {finalScoreGoal} · 이탈 {_finalDepartures}/{finalMaxDepartures}");
                    if (remain <= 0f) FinishFinalRun();
                    break;
            }
        }

        // ---------------- 시작 / 종료 ----------------

        void OnGameStateChanged(GameStateChanged e)
        {
            // 첫 공연 자동 실행
            if (autoRunOnFirstPlay &&
                !IsRunning &&
                e.Previous == GameState.Ready && e.Current == GameState.Playing &&
                !Save.GetBool(TutorialDoneKey, false))
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
            if (SpecialAudienceManager.HasInstance) SpecialAudienceManager.Instance.StopSystem();

            _crisis = FindFirstObjectByType<NearbyConcertCrisisDirector>();
            _crisisWasEnabled = _crisis != null && _crisis.enabled;
            if (_crisis != null) _crisis.enabled = false;

            _presenter = FindFirstObjectByType<AudienceRosterPresenter>();

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
            if (_crisis != null) _crisis.enabled = _crisisWasEnabled;
            CardInput.UseFilter = null;
            overlay?.HideAll();
            overlay?.SetSkipVisible(false);
        }

        void EndTutorial(bool markDone, bool restartRun)
        {
            _phase = Phase.Idle;
            ReleaseControl();

            if (markDone) Save.SetBool(TutorialDoneKey, true);

            if (restartRun && GameManager.HasInstance && GameManager.Instance.IsPlaying)
            {
                // 튜토리얼 흔적을 지우고 진짜 공연을 깨끗하게 시작한다
                if (AudienceRosterSystem.HasInstance) AudienceRosterSystem.Instance.ResetRoster();
                if (ComboSystem.HasInstance) ComboSystem.Instance.ResetCombo();
                GameManager.Instance.SetScore(0);
                if (HypeSystem.HasInstance) HypeSystem.Instance.ResetHype();
                if (SpecialAudienceManager.HasInstance) SpecialAudienceManager.Instance.StartSystem();
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
                case Phase.CrossExplain: EnterExcitement(); break;
                case Phase.CrowdChange:  EnterFinalIntro(); break;
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
                case Phase.PrefChill:     EnterCrossUse(); break;
                case Phase.CrossUse:      EnterCrossExplain(e.GainedScore); break;
                case Phase.Excitement:    EnterCrowdChange(); break;
                case Phase.FinalRun:      _finalScore += e.GainedScore; break;
            }
        }

        void OnAudienceDeparted(AudienceDeparted e)
        {
            if (!IsRunning || _phase != Phase.FinalRun) return;
            if (e.Reason == AudienceDepartureReason.EngagementDepleted) _finalDepartures++;
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
                "좋은 카드가 항상 좋은 반응을 만드는 것은 아닙니다.",
                "지금 이 관객에게 맞는 카드를 찾아야 합니다. (클릭해서 계속)",
                true);
        }

        void EnterPrefMosh()
        {
            SetPhase(Phase.PrefMosh);
            _expectedPref = CrowdPreference.Mosh;
            _blockedHint = "이 관객은 거칠고 강한 연주를 원합니다. 다른 카드를 골라보세요.";
            SpotlightPref(CrowdPreference.Mosh);
            overlay?.ShowMessage(
                "검은 옷에 밴드 패치 — 격렬한 공연을 좋아하는 MOSH 관객입니다.",
                "이 관객이 좋아할 카드를 위로 드래그해 사용해 보세요.",
                false);
        }

        void EnterPrefSingalong()
        {
            SetPhase(Phase.PrefSingalong);
            _expectedPref = CrowdPreference.Singalong;
            _blockedHint = "이 관객은 함께 부르고 싶어 합니다. 옷차림과 손동작을 다시 확인해보세요.";
            SetRoster((CrowdPreference.Singalong, middleEngagement)); // Mosh 퇴장, Singalong 등장
            SpotlightPref(CrowdPreference.Singalong);
            overlay?.ShowMessage(
                "이 관객은 따라 부르고 싶어 합니다.",
                "옷차림을 보고 맞는 카드를 골라보세요.",
                false);
        }

        void EnterPrefChill()
        {
            SetPhase(Phase.PrefChill);
            _expectedPref = CrowdPreference.Chill;
            _blockedHint = "옷차림을 다시 보세요. 차분한 복장의 관객은 여유로운 공연을 좋아합니다.";
            SetRoster((CrowdPreference.Chill, middleEngagement)); // Singalong 퇴장, Chill 등장
            SpotlightPref(CrowdPreference.Chill);
            overlay?.ShowMessage(
                "이번에는 힌트가 없습니다.",
                "옷차림만 보고 직접 판단해 보세요.",
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
                "카드 한 장은 무대의 모든 관객에게 영향을 줍니다.",
                "아무 공연 카드나 사용하고, 세 관객의 반응 차이를 지켜보세요.",
                false);
        }

        void EnterCrossExplain(int gainedScore)
        {
            SetPhase(Phase.CrossExplain);
            _allowAllCards = false;
            overlay?.ShowMessage(
                "같은 카드라도 관객마다 반응이 다릅니다.",
                $"방금 카드의 총 반응: {gainedScore:+0;-0;0}. 누구를 열광시키고 누구의 실망을 감수할지 " +
                "선택하는 것이 이 게임의 핵심입니다. (클릭해서 계속)",
                true);
        }

        void EnterExcitement()
        {
            SetPhase(Phase.Excitement);
            _expectedPref = CrowdPreference.Chill;
            _blockedHint = "지루한 관객의 취향에 맞는 카드가 필요합니다.";

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
                "옷차림 = 무엇을 좋아하는가 · 움직임 = 지금 얼마나 신났는가",
                "CHILL 관객이 지루해하고 있습니다. 떠나기 전에 취향에 맞는 카드로 관심을 끌어보세요.",
                false);
        }

        void EnterCrowdChange()
        {
            SetPhase(Phase.CrowdChange);
            _allowAllCards = true;

            AudienceRosterSystem.Instance.TryAdd(
                CrowdPreference.Singalong, excitedEngagement,
                AudienceJoinReason.RuntimeCommand, out var snapshot);

            overlay?.SetDim(true);
            if (_presenter != null && _presenter.TryGetActor(snapshot.Id, out var actor))
                overlay?.Spotlight(actor);

            // 2초 관찰 후 Update 에서 진행 버튼을 살린다
            overlay?.ShowMessage(
                "새로운 관객이 들어왔습니다!",
                "관객이 바뀌면 좋은 카드도 바뀝니다. 새 관객의 옷차림과 움직임을 확인해 보세요.",
                false);
        }

        void EnterFinalIntro()
        {
            SetPhase(Phase.FinalIntro);
            _allowAllCards = true;

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
                $"{finalDuration:0}초 미니 공연!",
                $"목표: 이탈 {finalMaxDepartures}명 이하 · 총 반응 +{finalScoreGoal}. " +
                "지루한 관객부터 챙겨보세요. (클릭해서 시작)",
                true);
        }

        void EnterFinalRun()
        {
            SetPhase(Phase.FinalRun);
            _finalScore = 0;
            _finalDepartures = 0;
            overlay?.ShowMessage("공연 중!", "", false);
        }

        void FinishFinalRun()
        {
            bool success = _finalScore >= finalScoreGoal && _finalDepartures <= finalMaxDepartures;

            if (success)
            {
                SetPhase(Phase.Complete);
                overlay?.ShowMessage(
                    "관객을 읽었습니다!",
                    "옷으로 취향을 보고, 움직임으로 현재 상태를 확인하세요. (클릭해서 진짜 공연 시작)",
                    true);
            }
            else
            {
                SetPhase(Phase.FinalFail);
                overlay?.ShowMessage(
                    "다시 한 번!",
                    $"총 반응 {_finalScore:+0;-0;0} · 이탈 {_finalDepartures}명. " +
                    "지루해진 관객의 취향부터 맞춰보세요. (클릭해서 재도전)",
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
