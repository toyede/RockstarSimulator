using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 신문 결과창의 스킨과 문구. 문구·아트를 코드와 독립적으로 고칠 수 있게 전부 여기 둔다.
    /// Resources/Tour/ResultNewspaperCatalog.asset — Tools/Tour/Setup Result Newspaper 가 만든다.
    /// </summary>
    [CreateAssetMenu(fileName = "ResultNewspaperCatalog", menuName = "ContextStage/Tour/Result Newspaper Catalog")]
    public sealed class ResultNewspaperCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Tour/ResultNewspaperCatalog";

        [Serializable]
        public sealed class Skin
        {
            [Tooltip("StageDefinition.StageId. 비우면 기본 스킨")] public string stageId = "";
            [Tooltip("신문 지면 (제호 포함 초안)")] public Sprite paper;
            [Tooltip("매체 이름 (로그·제호 정보용)")] public string paperName = "";
            [Tooltip("도장·포인트 색")] public Color accent = new Color32(0xB3, 0x38, 0x31, 0xFF);
        }

        [Serializable]
        public sealed class RankIcon
        {
            public string label = "F";
            public Sprite icon;
        }

        [Header("스킨 (stageId 별). 없으면 첫 항목")]
        [SerializeField] List<Skin> skins = new List<Skin>();

        [Header("등급 아이콘 (기존 랭크 에셋 재사용)")]
        [SerializeField] List<RankIcon> rankIcons = new List<RankIcon>();

        [Header("사진 — 밴드 포즈 (현재는 너구리 1종씩)")]
        [SerializeField] Sprite bandClear;
        [SerializeField] Sprite bandFail;
        [SerializeField, Min(0.5f)] float bandSpriteScale = 3f;

        [Header("헤드라인 — 일반 공연 (등급별)")]
        [SerializeField] string headlineS = "오늘 밤의 주인공, 너구리 밴드!";
        [SerializeField] string headlineA = "쏟아지는 환호… 제대로 이름 알렸다!";
        [SerializeField] string headlineB = "관객 사로잡은 무대, 다음 공연도 기대";
        [SerializeField] string headlineC = "가능성 보여준 너구리 밴드의 한 무대";
        [SerializeField] string headlineD = "첫 관문 넘었다! 다음 무대 향하는 밴드";
        [SerializeField] string headlineFail = "왕관은 화려했지만, 오늘 공연은 삐걱";

        [Header("헤드라인 — 보스전")]
        [SerializeField] string headlineBossWin = "라이벌과의 정면 승부, 너구리 밴드 웃었다";
        [SerializeField] string headlineBossLose = "라이벌 무대에 조명이… 너구리 밴드는 재정비";

        [Header("부제 — 대표 기록 하나만 ({0} = 값). 조건 미달이면 기본 부제")]
        [SerializeField, Min(1)] int comboSubtitleMin = 10;
        [SerializeField] string comboSubtitle = "최고 {0}콤보, 끊이지 않는 호응으로 공연 이끌어";
        [SerializeField, Min(1)] int feverSubtitleMin = 2;
        [SerializeField] string feverSubtitle = "피버 {0}회 발동… 무대를 달군 연속 공연";
        [SerializeField, Min(1)] int specialSubtitleMin = 2;
        [SerializeField] string specialSubtitle = "특별 관객 요청 {0}회 성공, 까다로운 팬도 만족";
        [SerializeField, Min(1)] int crisisSubtitleMin = 1;
        [SerializeField] string crisisSubtitle = "이탈 위기에서 관객 {0}명 붙잡아";
        [SerializeField] string defaultSubtitle = "목표 달성률 {0}%로 공연을 마쳤다";
        [SerializeField] string failSubtitle = "목표까지 {0}점 부족… 밴드는 다음 무대를 기약했다";
        [SerializeField] string bossWinSubtitle = "상대 팬 {0}명이 너구리 밴드 쪽으로 자리를 옮겼다";
        [SerializeField, Tooltip("영입한 팬이 0명일 때 ({0} = 성공한 패턴 수)")] string bossWinSubtitleNoRecruit = "패턴 {0}회 성공으로 라이벌 무대를 잠재웠다";
        [SerializeField] string bossLoseSubtitle = "패턴 {0}회 성공에 그쳐… 다음 대결을 기약했다";

        [Header("사진 설명")]
        [SerializeField] string captionHigh = "환호 속에 공연을 마친 너구리 밴드. 오늘 밤은 이들의 것이었다.";
        [SerializeField] string captionNormal = "공연을 마친 너구리 밴드. 왕관만큼은 끝까지 지켰다.";
        [SerializeField] string captionFail = "삐뚤어진 왕관을 고쳐 쓰는 너구리. 동료들은 조용히 장비를 정리했다.";
        [SerializeField] string captionBossWin = "라이벌을 뒤로하고 앙코르 무대에 선 너구리 밴드.";
        [SerializeField] string captionBossLose = "조명은 라이벌 무대로 향했다. 너구리 밴드는 다음을 준비한다.";

        [Header("도장")]
        [SerializeField] string stampSuccess = "공연 성공";
        [SerializeField] string stampFail = "공연 실패";
        [SerializeField] string stampBossWin = "대결 승리";
        [SerializeField] string stampBossLose = "대결 패배";
        [SerializeField] Color stampFailColor = new Color32(0x62, 0x55, 0x65, 0xFF);

        [Header("다음 진행 버튼")]
        [SerializeField] string buttonAugment = "증강 선택하기 >";
        [SerializeField] string buttonEnding = "투어의 결말 보기 >";
        [SerializeField] string buttonFailed = "투어 결과 확인 >";

        [Header("기믹 기사 — 스테이지별 한 칸 ({0},{1} = 값)")]
        [SerializeField] string articleBuskingTitle = "거리의 발길이 멈췄다";
        [SerializeField] string articleBuskingBody = "지나가던 관객 {0}명이 공연 중 합류";
        [SerializeField] string articleSpecialTitle = "까다로운 손님도 고개 끄덕였다";
        [SerializeField] string articleSpecialBody = "특별 관객 요청 {0} / {1}회 성공";
        [SerializeField] string articleCrisisTitle = "옆 공연의 유혹에도 자리를 지킨 팬들";
        [SerializeField] string articleCrisisBody = "위기 대상 {1}명 중 {0}명 방어";
        [SerializeField] string articleBossTitle = "라이벌과의 정면 승부";
        [SerializeField] string articleBossBody = "패턴 {0} / {1}회 성공 · 상대 팬 영입 {2}명 · 빼앗긴 팬 {3}명";
        [SerializeField] string articleDefaultTitle = "공연 후기";
        [SerializeField] string articleDefaultBody = "공연 중 최다 관객 {0}명";

        [Header("보스 결말 한 줄")]
        [SerializeField] string outcomeDefeated = "라이벌 격파";
        [SerializeField] string outcomeStreak = "연속 패턴 성공으로 승리";
        [SerializeField] string outcomeTimeOut = "제한 시간 종료 — 라이벌을 꺾지 못했습니다";

        public IReadOnlyList<Skin> Skins => skins;
        public Sprite BandClear => bandClear;
        public Sprite BandFail => bandFail;
        public float BandSpriteScale => bandSpriteScale;
        public string HeadlineFail => headlineFail;
        public string HeadlineBossWin => headlineBossWin;
        public string HeadlineBossLose => headlineBossLose;
        public int ComboSubtitleMin => comboSubtitleMin;
        public string ComboSubtitle => comboSubtitle;
        public int FeverSubtitleMin => feverSubtitleMin;
        public string FeverSubtitle => feverSubtitle;
        public int SpecialSubtitleMin => specialSubtitleMin;
        public string SpecialSubtitle => specialSubtitle;
        public int CrisisSubtitleMin => crisisSubtitleMin;
        public string CrisisSubtitle => crisisSubtitle;
        public string DefaultSubtitle => defaultSubtitle;
        public string FailSubtitle => failSubtitle;
        public string BossWinSubtitle => bossWinSubtitle;
        public string BossWinSubtitleNoRecruit => bossWinSubtitleNoRecruit;
        public string BossLoseSubtitle => bossLoseSubtitle;
        public string CaptionHigh => captionHigh;
        public string CaptionNormal => captionNormal;
        public string CaptionFail => captionFail;
        public string CaptionBossWin => captionBossWin;
        public string CaptionBossLose => captionBossLose;
        public string StampSuccess => stampSuccess;
        public string StampFail => stampFail;
        public string StampBossWin => stampBossWin;
        public string StampBossLose => stampBossLose;
        public Color StampFailColor => stampFailColor;
        public string ButtonAugment => buttonAugment;
        public string ButtonEnding => buttonEnding;
        public string ButtonFailed => buttonFailed;
        public string ArticleBuskingTitle => articleBuskingTitle;
        public string ArticleBuskingBody => articleBuskingBody;
        public string ArticleSpecialTitle => articleSpecialTitle;
        public string ArticleSpecialBody => articleSpecialBody;
        public string ArticleCrisisTitle => articleCrisisTitle;
        public string ArticleCrisisBody => articleCrisisBody;
        public string ArticleBossTitle => articleBossTitle;
        public string ArticleBossBody => articleBossBody;
        public string ArticleDefaultTitle => articleDefaultTitle;
        public string ArticleDefaultBody => articleDefaultBody;
        public string OutcomeDefeated => outcomeDefeated;
        public string OutcomeStreak => outcomeStreak;
        public string OutcomeTimeOut => outcomeTimeOut;

        public static ResultNewspaperCatalog LoadDefault() => Resources.Load<ResultNewspaperCatalog>(ResourcesPath);

        /// <summary>등급별 헤드라인. 모르는 라벨은 D 문구.</summary>
        public string HeadlineForRank(string rank)
        {
            switch (rank)
            {
                case "S": return headlineS;
                case "A": return headlineA;
                case "B": return headlineB;
                case "C": return headlineC;
                default: return headlineD;
            }
        }

        public Skin ResolveSkin(string stageId)
        {
            Skin fallback = null;
            for (int i = 0; i < skins.Count; i++)
            {
                Skin skin = skins[i];
                if (skin == null) continue;
                if (string.Equals(skin.stageId, stageId, StringComparison.Ordinal)) return skin;
                if (fallback == null) fallback = skin;
            }
            return fallback;
        }

        public Sprite RankIconFor(string label)
        {
            for (int i = 0; i < rankIcons.Count; i++)
                if (rankIcons[i] != null && rankIcons[i].label == label) return rankIcons[i].icon;
            return null;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] 스킨을 추가하거나(있으면) 비어 있는 스프라이트만 채운다.</summary>
        public void EditorSetSkin(string stageId, Sprite paper, string paperName, Color accent)
        {
            for (int i = 0; i < skins.Count; i++)
            {
                if (skins[i] == null || skins[i].stageId != stageId) continue;
                if (skins[i].paper == null) skins[i].paper = paper;
                if (string.IsNullOrEmpty(skins[i].paperName)) skins[i].paperName = paperName;
                return;
            }
            skins.Add(new Skin { stageId = stageId, paper = paper, paperName = paperName, accent = accent });
        }

        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorSetRankIcon(string label, Sprite icon)
        {
            for (int i = 0; i < rankIcons.Count; i++)
            {
                if (rankIcons[i] == null || rankIcons[i].label != label) continue;
                if (rankIcons[i].icon == null) rankIcons[i].icon = icon;
                return;
            }
            rankIcons.Add(new RankIcon { label = label, icon = icon });
        }

        /// <summary>[에디터 셋업 전용] 비어 있을 때만 채운다.</summary>
        public void EditorSetBandSprites(Sprite clear, Sprite fail)
        {
            if (bandClear == null) bandClear = clear;
            if (bandFail == null) bandFail = fail;
        }
#endif
    }
}
