using System;
using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>포스터 위 인쇄 영역. 아트·표시 전용이며 공연 규칙과 독립적이다.</summary>
    [Serializable]
    public sealed class StagePosterPresentation
    {
        [Header("공연 제목 · 인쇄 문구")]
        [TextArea(1, 3)] public string headline = "";
        public string artistLine = "RACCOON ROLL / LIVE";
        public bool showRuleLabel = true;
        [TextArea(2, 4), Tooltip("비우면 기존 ruleBody. 규칙을 바꾸지 않는 소개용 요약")]
        public string summary = "";
        public Sprite headlineLogo;
        public TMP_FontAsset headlineFont;

        [Header("원본 포스터 내 영역 (0~1, 좌하단 기준)")]
        [Range(600f, 1000f)] public float height = 960f;
        public Rect headlineArea = new Rect(.15f, .79f, .7f, .13f);
        public Rect artistArea = new Rect(.15f, .75f, .7f, .035f);
        public Rect ruleLabelArea = new Rect(.2f, .26f, .65f, .035f);
        public Rect bodyArea = new Rect(.2f, .13f, .65f, .12f);
        public Rect entryArea = new Rect(.25f, .055f, .5f, .065f);
        [TextArea(1, 2)] public string entryText = "공연 시작";

        [Header("글자 · 잉크")]
        [Range(28f, 100f)] public float headlineSize = 64f;
        [Range(22f, 40f)] public float bodySize = 28f;
        [Range(16f, 28f)] public float captionSize = 19f;
        [Range(24f, 40f)] public float entrySize = 30f;
        [Range(-5f, 5f)] public float headlineAngle;
        [Range(-5f, 5f)] public float entryAngle = -2f;
        public Color headlineColor = new Color32(0x27, 0x20, 0x29, 255);
        public Color inkColor = new Color32(0x27, 0x20, 0x29, 255);
        public Color accentColor = new Color32(0x9b, 0x37, 0x53, 255);
        public TextAlignmentOptions bodyAlignment = TextAlignmentOptions.TopLeft;
    }
}
