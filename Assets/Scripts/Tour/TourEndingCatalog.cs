using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 엔딩별 표시 자료(제목·일러스트·한 줄 요약)와 결말 화면 문구. Resources/Tour/TourEndingCatalog.asset —
    /// Tools/Tour/Setup Ending Catalog 가 만든다. 일러스트가 비어 있으면 마지막 공연 배경을 어둡게 깐다.
    /// (파일 이름 = 클래스 이름이어야 에셋이 스크립트를 찾는다)
    /// </summary>
    [CreateAssetMenu(fileName = "TourEndingCatalog", menuName = "ContextStage/Tour/Ending Catalog")]
    public sealed class TourEndingCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Augments/Tour/TourEndingCatalog";

        [Serializable]
        public sealed class Entry
        {
            public TourEndingKind kind;
            [Tooltip("결말 화면 제목 (엔딩 대사 중에는 좌상단 공연장 이름 자리에 뜬다)")] public string title = "";
            [Tooltip("엔딩 일러스트 (윤빈). 비우면 마지막 공연 배경")] public Sprite illustration;
            [Tooltip("결말 화면 한 줄 요약. {0} = 총점, {1} = 가장 호응한 관객 성향")] public string summary = "";
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        [Header("결말 화면 문구")]
        [SerializeField] string totalScoreLabel = "TOTAL SCORE";
        [SerializeField] string ranksLabel = "RANKS";
        [SerializeField] string topAudienceLabel = "가장 호응한 관객";
        [SerializeField] string nameEntryLabel = "이름을 남기고 순위에 기록하세요";
        [SerializeField] string nameEntryButton = "기록하기";
        [SerializeField] string nameEntryDone = "기록됨";
        [SerializeField] string returnButton = "타이틀로";
        [SerializeField] string dialogueConfirmLabel = "결말 보기";

        public string TotalScoreLabel => totalScoreLabel;
        public string RanksLabel => ranksLabel;
        public string TopAudienceLabel => topAudienceLabel;
        public string NameEntryLabel => nameEntryLabel;
        public string NameEntryButton => nameEntryButton;
        public string NameEntryDone => nameEntryDone;
        public string ReturnButton => returnButton;
        public string DialogueConfirmLabel => dialogueConfirmLabel;

        public static TourEndingCatalog LoadDefault() => Resources.Load<TourEndingCatalog>(ResourcesPath);

        public Entry Find(TourEndingKind kind)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].kind == kind) return entries[i];
            return null;
        }

        /// <summary>셋업 메뉴용: 항목이 없으면 기본 문구로 채운다 (있는 항목은 유지).</summary>
        public void EnsureDefaults()
        {
            Ensure(TourEndingKind.Bad, "공연 중단", "장비를 빼달라는 말을 들었다. 총점 {0}.");
            Ensure(TourEndingKind.AllS, "전 공연 S 랭크", "스타디움 전광판에 다음 투어 광고가 떴다. 총점 {0}.");
            Ensure(TourEndingKind.Mosh, "크라우드서핑", "지하 클럽의 팬들은 물러서지 않았다. 총점 {0}, 가장 뜨거운 관객 {1}.");
            Ensure(TourEndingKind.Singalong, "떼창", "밖에서는 아직도 후렴이 들린다. 총점 {0}, 가장 뜨거운 관객 {1}.");
            Ensure(TourEndingKind.Chill, "첫 싱글", "직접 녹음한 곡이 추천 목록에 올랐다. 총점 {0}, 가장 뜨거운 관객 {1}.");
        }

        void Ensure(TourEndingKind kind, string title, string summary)
        {
            if (Find(kind) != null) return;
            entries.Add(new Entry { kind = kind, title = title, summary = summary });
        }
    }
}
