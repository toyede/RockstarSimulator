using UnityEngine;

namespace ContextStage
{
    /// <summary>일반 이벤트와 보스 미션의 공통 화면 자리. 튜토리얼 종이는 독립 배치한다.</summary>
    public static class MissionCueSheetLayout
    {
        public static readonly Vector2 Position = new Vector2(32f, 0f);
        public static readonly Vector2 Size = new Vector2(512f, 384f);

        public static void Apply(RectTransform panel, Vector2 position, Vector2 size)
        {
            panel.anchorMin = panel.anchorMax = new Vector2(0f, .5f);
            panel.pivot = new Vector2(0f, .5f);
            panel.anchoredPosition = position;
            panel.sizeDelta = size;
        }
    }
}
