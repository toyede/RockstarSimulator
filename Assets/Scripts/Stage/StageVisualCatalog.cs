using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>스테이지 하나의 비주얼·룰 카드 데이터. (에셋 명세 §13 StageVisualCatalog)</summary>
    [Serializable]
    public sealed class StageVisualEntry
    {
        [Tooltip("StageDefinition.stageId 와 같은 값")]
        public string stageId = "";

        [Header("배경 (1920×1080, PPU 100)")]
        [Tooltip("불투명 Base 배경. night_city_ground 자리를 대체한다")]
        public Sprite backgroundBase;

        [Tooltip("Base 위에 겹치는 투명 조명 레이어 (Sorting Order 1)")]
        public List<Sprite> lightOverlays = new List<Sprite>();

        [Tooltip("화면 좌측 가장자리 전경 소품 (Sorting Order 46)")]
        public Sprite foregroundLeft;

        [Tooltip("화면 우측 가장자리 전경 소품 (Sorting Order 46)")]
        public Sprite foregroundRight;

        [Header("장식 관객")]
        [Tooltip("이 공연장 전용 장식 관객 스프라이트. 비우면 기본 people1/people2 유지")]
        public List<Sprite> decorativeCrowdVariants = new List<Sprite>();

        [Header("UI")]
        [Tooltip("맵 노드 썸네일 (512×288)")]
        public Sprite mapThumbnail;

        [Tooltip("룰 카드 아이콘 (128×128)")]
        public Sprite ruleIcon;

        [Tooltip("룰 카드 제목. 예: [골목 버스킹]")]
        public string ruleTitle = "";

        [TextArea(2, 4), Tooltip("룰 카드 본문. '무슨 일이 생기는가 / 무엇을 해야 하는가' 두 줄")]
        public string ruleBody = "";

        public bool HasBackground => backgroundBase != null;
        public bool HasRuleCard => !string.IsNullOrWhiteSpace(ruleTitle);
    }

    /// <summary>
    /// stageId → 비주얼 데이터. StageDefinition 에 Sprite 를 직접 넣지 않는다 (에셋 명세 §13).
    /// 아트 교체가 카드·점수·증강 코드에 영향을 주지 않도록 분리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageVisualCatalog", menuName = "ContextStage/Tour/Stage Visual Catalog")]
    public sealed class StageVisualCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Stages/StageVisualCatalog";

        [SerializeField]
        List<StageVisualEntry> entries = new List<StageVisualEntry>();

        public IReadOnlyList<StageVisualEntry> Entries => entries;

        public static StageVisualCatalog LoadDefault() =>
            Resources.Load<StageVisualCatalog>(ResourcesPath);

        public bool TryGet(string stageId, out StageVisualEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(stageId) || entries == null) return false;

            string trimmed = stageId.Trim();
            for (int i = 0; i < entries.Count; i++)
            {
                StageVisualEntry candidate = entries[i];
                if (candidate != null &&
                    string.Equals(candidate.stageId, trimmed, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>카탈로그가 없거나 항목이 없어도 null 대신 false 만 돌려주는 편의 접근자.</summary>
        public static bool TryResolve(string stageId, out StageVisualEntry entry)
        {
            StageVisualCatalog catalog = LoadDefault();
            if (catalog != null) return catalog.TryGet(stageId, out entry);
            entry = null;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] 같은 stageId 항목이 있으면 그 항목을 돌려주고, 없으면 새로 만든다.</summary>
        public StageVisualEntry EditorGetOrAddEntry(string stageId)
        {
            if (TryGet(stageId, out StageVisualEntry existing)) return existing;
            var created = new StageVisualEntry { stageId = stageId };
            entries.Add(created);
            return created;
        }
#endif
    }
}
