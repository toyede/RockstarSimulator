using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 보스전 「관객 쟁탈전」 수치 (Docs/BOSS_STAGE_REDESIGN_KO.md v2). 코드에 숫자를 박지 않고 여기서 조절한다.
    /// 체력은 없다. 라이벌 팬 수만큼 주기적으로 점수가 깎이고, 시간 종료 시 점수 ≥ 목표면 승리.
    /// </summary>
    [CreateAssetMenu(fileName = "BossBattleConfig", menuName = "ContextStage/Tour/Boss Battle Config")]
    public sealed class BossBattleConfig : ScriptableObject
    {
        [Header("라이벌")]
        [SerializeField] string rivalName = "LUX//FAUNA";
        [SerializeField, Min(0), Tooltip("라이벌 팬 시작 수. 우리 관객 시작 수는 audience_stadium_final 프리셋")] int rivalFanPool = 14;

        [Header("드레인 (라이벌 팬 1명당 주기적으로 깎이는 점수)")]
        [SerializeField, Min(0f), Tooltip("첫 감소까지(초)")] float drainStartDelay = 8f;
        [SerializeField, Min(0.5f), Tooltip("감소 주기(초)")] float drainInterval = 4f;
        [SerializeField, Min(0), Tooltip("라이벌 팬 1명당 감소 점수")] int drainPerFan = 12;

        [Header("패턴 스케줄")]
        [SerializeField, Min(0f), Tooltip("첫 패턴 예고까지(초)")] float firstPatternDelay = 10f;
        [SerializeField, Min(1f), Tooltip("패턴 종료 후 다음 패턴까지(초)")] float patternInterval = 10f;
        [SerializeField, Min(1f), Tooltip("REVENGE TIME 의 패턴 간격(초)")] float revengePatternInterval = 7f;
        [SerializeField, Min(1f), Tooltip("패턴 창(초). GUEST LIST 는 특별 관객 요청 시간을 따른다")] float patternWindow = 8f;
        [SerializeField, Min(1f), Tooltip("REVENGE GUEST LIST 요청 시간(초)")] float guestListEnhancedWindow = 6f;
        [SerializeField, Min(0f), Tooltip("시작 가능한 패턴이 없을 때 재시도 간격(초)")] float retryDelay = 2f;

        [Header("패턴 결과")]
        [SerializeField, Min(0), Tooltip("성공 시 라이벌 → 우리 팬 수")] int stealOnSuccess = 2;
        [SerializeField, Min(0), Tooltip("강화 패턴 성공 시")] int stealOnSuccessEnhanced = 3;
        [SerializeField, Min(0), Tooltip("DROP 성공 시")] int stealOnDrop = 3;
        [SerializeField, Range(0f, 100f), Tooltip("합류하는 팬의 몰입도")] float recruitEngagement = 55f;
        [SerializeField, Range(0f, 1f), Tooltip("성공 보너스 = 목표 점수 × 이 값")] float patternBonusRatio = 0.06f;
        [SerializeField, Range(0f, 1f), Tooltip("DROP 성공 보너스 = 목표 점수 × 이 값")] float dropBonusRatio = 0.10f;
        [SerializeField, Min(0), Tooltip("실패 시 라이벌로 건너가는 우리 팬 수")] int loseFanOnFail = 1;
        [SerializeField, Min(1), Tooltip("실패로 팬을 보내도 이만큼은 남긴다")] int minimumSurvivors = 3;

        [Header("REVENGE TIME! (라이벌 팬 과반수를 뺏으면 반격)")]
        [SerializeField, Min(0), Tooltip("라이벌 팬이 이 수 이하가 되면 진입")] int revengeThreshold = 6;
        [SerializeField, Min(0), Tooltip("라이벌 팬이 이 수 이상으로 돌아오면 해제 (히스테리시스)")] int revengeReleaseThreshold = 9;

        [Header("B2B (맞받아치기)")]
        [SerializeField, Min(1)] int b2bCards = 1;
        [SerializeField, Min(1)] int b2bCardsEnhanced = 2;

        [Header("BEATMATCH (흐름 유지)")]
        [SerializeField, Min(1)] int beatmatchCards = 2;
        [SerializeField, Min(1)] int beatmatchCardsEnhanced = 3;

        [Header("KILL SWITCH (성향 봉인)")]
        [SerializeField, Min(1)] int killSwitchBannedPreferences = 1;
        [SerializeField, Min(1)] int killSwitchBannedPreferencesEnhanced = 2;
        [SerializeField, Min(1)] int killSwitchPositives = 2;
        [SerializeField, Min(1)] int killSwitchPositivesEnhanced = 3;

        [Header("연출 (라이벌 무대 왕복 · 엿보기)")]
        [SerializeField, Tooltip("예고 연출 중 공연 제한시간·호응 감소를 멈춘다 (엿보기는 멈추지 않는다)")] bool pauseTimerDuringCinematic = true;
        [SerializeField, Range(0f, 1f), Tooltip("연출 중 화면 틴트 알파")] float cinematicTintAlpha = 0.35f;
        [SerializeField, Min(0f), Tooltip("틴트 페이드·손패 하강 시간(초)")] float cinematicFadeDuration = 0.3f;
        [SerializeField, Min(0f), Tooltip("우리 무대 → 라이벌 무대 팬(초)")] float cinematicPanOutDuration = 1.2f;
        [SerializeField, Min(0f), Tooltip("라이벌 무대 체류(초)")] float cinematicHoldDuration = 1f;
        [SerializeField, Min(0f), Tooltip("라이벌 무대 → 우리 무대 복귀(초)")] float cinematicPanBackDuration = 1f;
        [SerializeField, Min(0f), Tooltip("엿보기 팬 시간(초, 왕복 각각)")] float peekPanDuration = 1f;
        [SerializeField, Min(0f), Tooltip("손패를 화면 아래로 내리는 거리(px, 1080 기준)")] float handStowDistance = 420f;

        public string RivalName => rivalName;
        public int RivalFanPool => rivalFanPool;
        public float DrainStartDelay => drainStartDelay;
        public float DrainInterval => drainInterval;
        public int DrainPerFan => drainPerFan;
        public float FirstPatternDelay => firstPatternDelay;
        public float PatternInterval => patternInterval;
        public float RevengePatternInterval => revengePatternInterval;
        public float PatternWindow => patternWindow;
        public float GuestListEnhancedWindow => guestListEnhancedWindow;
        public float RetryDelay => retryDelay;
        public int StealOnSuccess => stealOnSuccess;
        public int StealOnSuccessEnhanced => stealOnSuccessEnhanced;
        public int StealOnDrop => stealOnDrop;
        public float RecruitEngagement => recruitEngagement;
        public float PatternBonusRatio => patternBonusRatio;
        public float DropBonusRatio => dropBonusRatio;
        public int LoseFanOnFail => loseFanOnFail;
        public int MinimumSurvivors => minimumSurvivors;
        public int RevengeThreshold => revengeThreshold;
        public int RevengeReleaseThreshold => revengeReleaseThreshold;
        public int B2BCards => b2bCards;
        public int B2BCardsEnhanced => b2bCardsEnhanced;
        public int BeatmatchCards => beatmatchCards;
        public int BeatmatchCardsEnhanced => beatmatchCardsEnhanced;
        public int KillSwitchBannedPreferences => killSwitchBannedPreferences;
        public int KillSwitchBannedPreferencesEnhanced => killSwitchBannedPreferencesEnhanced;
        public int KillSwitchPositives => killSwitchPositives;
        public int KillSwitchPositivesEnhanced => killSwitchPositivesEnhanced;
        public bool PauseTimerDuringCinematic => pauseTimerDuringCinematic;
        public float CinematicTintAlpha => cinematicTintAlpha;
        public float CinematicFadeDuration => cinematicFadeDuration;
        public float CinematicPanOutDuration => cinematicPanOutDuration;
        public float CinematicHoldDuration => cinematicHoldDuration;
        public float CinematicPanBackDuration => cinematicPanBackDuration;
        public float PeekPanDuration => peekPanDuration;
        public float HandStowDistance => handStowDistance;

        /// <summary>예고 연출에서 자막이 보이는 시간 (팬 아웃 + 체류 + 복귀).</summary>
        public float AnnounceDisplaySeconds => cinematicFadeDuration + cinematicPanOutDuration + cinematicHoldDuration + cinematicPanBackDuration;
    }
}
