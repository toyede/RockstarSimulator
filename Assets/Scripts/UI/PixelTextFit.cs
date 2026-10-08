using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>도트 UI는 소수점 자동 축소 대신 2px 단위로 맞춘다. 공유 폰트/재질은 변경하지 않는다.</summary>
    internal static class PixelTextFit
    {
        public static void Apply(TMP_Text label)
        {
            if (label == null) return;
            label.enableAutoSizing = false;
            float width = label.rectTransform.rect.width;
            float height = label.rectTransform.rect.height;
            if (width <= 0f || height <= 0f) return;
            int maximum = Mathf.FloorToInt(label.fontSizeMax / 2f) * 2;
            int minimum = Mathf.CeilToInt(label.fontSizeMin / 2f) * 2;
            maximum = Mathf.Max(minimum, maximum);
            for (int size = maximum; size >= minimum; size -= 2)
            {
                label.fontSize = size;
                Vector2 preferred = label.GetPreferredValues(label.text, width, Mathf.Infinity);
                if (preferred.x <= width + 0.5f && preferred.y <= height + 0.5f) break;
            }
        }
    }
}
