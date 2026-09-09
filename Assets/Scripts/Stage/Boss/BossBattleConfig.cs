using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 보스전(월드 스타디움) 수치. 코드에 숫자를 박지 않고 여기서 조절한다.
    /// 체력은 목표 점수와 같고, 카드 점수가 그대로 피해다.
    /// </summary>
    [CreateAssetMenu(fileName = "BossBattleConfig", menuName = "ContextStage/Tour/Boss Battle Config")]
    public sealed class BossBattleConfig : ScriptableObject
    {
        [Header("라이벌")]
        [SerializeField] string rivalName = "LUX//FAUNA";

        [Header("스케줄")]
        [SerializeField, Min(0f), Tooltip("첫 패턴 예고까지(초)")] float firstPatternDelay = 10f;
        [SerializeField, Min(1f), Tooltip("패턴 종료 후 다음 패턴까지(초)")] float patternInterval = 12f;
        [SerializeField, Min(1f), Tooltip("PEAK TIME 의 패턴 간격(초)")] float peakPatternInterval = 8f;
        [SerializeField, Min(1f), Tooltip("패턴 창(초). GUEST LIST 는 특별 관객 요청 시간을 따른다")] float patternWindow = 8f;
        [SerializeField, Min(1f), Tooltip("PEAK TIME GUEST LIST 요청 시간(초)")] float guestListEnhancedWindow = 6f;
        [SerializeField, Min(0f), Tooltip("시작 가능한 패턴이 없을 때 재시도 간격(초)")] float retryDelay = 2f;

        [Header("체력")]
        [SerializeField, Range(0.01f, 1f), Tooltip("패턴 성공 시 최대 체력 대비 피해")] float successDamageRatio = 0.12f;
        [SerializeField, Range(0f, 1f), Tooltip("패턴 실패 시 최대 체력 대비 회복")] float failHealRatio = 0.05f;
        [SerializeField, Range(0.05f, 0.95f), Tooltip("이 비율 이하가 되면 PEAK TIME")] float peakTimeRatio = 0.5f;
        [SerializeField, Min(1), Tooltip("이만큼 연속 성공하면 즉시 격파")] int streakToClear = 5;
        [SerializeField, Min(0), Tooltip("조기 격파 시 남은 초당 가산 점수")] int earlyClearBonusPerSecond = 40;

        [Header("관객 이동")]
        [SerializeField, Min(0), Tooltip("상대 대기 팬 수 (성공할 때마다 우리 쪽으로 온다)")] int rivalFanPool = 6;
        [SerializeField, Min(0)] int recruitOnSuccess = 1;
        [SerializeField, Min(0)] int recruitOnSuccessEnhanced = 2;
        [SerializeField, Range(0f, 100f)] float recruitEngagement = 55f;
        [SerializeField, Min(0), Tooltip("실패 시 떠나는 우리 팬 수")] int loseFanOnFail = 1;
        [SerializeField, Min(1), Tooltip("실패로 팬을 보내도 이만큼은 남긴다")] int minimumSurvivors = 3;

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

        [Header("연출 (라이벌 무대 왕복)")]
        [SerializeField, Tooltip("연출 중 공연 제한시간·호응 감소를 멈춘다")] bool pauseTimerDuringCinematic = true;
        [SerializeField, Range(0f, 1f), Tooltip("연출 중 화면 틴트 알파")] float cinematicTintAlpha = 0.35f;
        [SerializeField, Min(0f), Tooltip("틴트 페이드·손패 하강 시간(초)")] float cinematicFadeDuration = 0.3f;
        [SerializeField, Min(0f), Tooltip("우리 무대 → 라이벌 무대 팬(초)")] float cinematicPanOutDuration = 1.2f;
        [SerializeField, Min(0f), Tooltip("라이벌 무대 체류(초)")] float cinematicHoldDuration = 1f;
        [SerializeField, Min(0f), Tooltip("라이벌 무대 → 우리 무대 복귀(초)")] float cinematicPanBackDuration = 1f;
        [SerializeField, Min(0f), Tooltip("격파 시 라이벌 무대 소등 체류(초)")] float defeatHoldDuration = 1.5f;
        [SerializeField, Min(0f), Tooltip("손패를 화면 아래로 내리는 거리(px, 1080 기준)")] float handStowDistance = 420f;

        public string RivalName => rivalName;
        public float FirstPatternDelay => firstPatternDelay;
        public float PatternInterval => patternInterval;
        public float PeakPatternInterval => peakPatternInterval;
        public float PatternWindow => patternWindow;
        public float GuestListEnhancedWindow => guestListEnhancedWindow;
        public float RetryDelay => retryDelay;
        public float SuccessDamageRatio => successDamageRatio;
        public float FailHealRatio => failHealRatio;
        public float PeakTimeRatio => peakTimeRatio;
        public int StreakToClear => streakToClear;
        public int EarlyClearBonusPerSecond => earlyClearBonusPerSecond;
        public int RivalFanPool => rivalFanPool;
        public int RecruitOnSuccess => recruitOnSuccess;
        public int RecruitOnSuccessEnhanced => recruitOnSuccessEnhanced;
        public float RecruitEngagement => recruitEngagement;
        public int LoseFanOnFail => loseFanOnFail;
        public int MinimumSurvivors => minimumSurvivors;
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
        public float DefeatHoldDuration => defeatHoldDuration;
        public float HandStowDistance => handStowDistance;

        /// <summary>예고 연출에서 자막이 보이는 시간 (팬 아웃 + 체류 + 복귀). 돌아오면 패턴 패널이 이어받는다.</summary>
        public float AnnounceDisplaySeconds => cinematicFadeDuration + cinematicPanOutDuration + cinematicHoldDuration + cinematicPanBackDuration;
    }
}
