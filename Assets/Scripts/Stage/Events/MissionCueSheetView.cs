using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>판정 로직 없이 슬레이트 아트와 텍스트 영역만 구성한다.</summary>
    public sealed class MissionCueSheetView : MonoBehaviour
    {
        public static readonly Color Ink = new Color32(0x2D, 0x27, 0x35, 0xFF);
        public TMP_Text Title { get; private set; }
        public TMP_Text Body { get; private set; }
        public TMP_Text Progress { get; private set; }
        public TMP_Text Timer { get; private set; }
        public TMP_Text Secondary { get; private set; }

        public static MissionCueSheetView Create(RectTransform root, TMP_FontAsset font)
        {
            var view = root.gameObject.AddComponent<MissionCueSheetView>();
            var image = root.gameObject.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/CueSheets/MissionSlate");
            image.color = image.sprite != null ? Color.white : new Color32(0xE2, 0xE8, 0xDF, 0xFF);
            image.raycastTarget = false;
            view.Title = Label(root, "Title", font, 32, Color.white, new Vector2(.06f, .66f), new Vector2(.75f, .82f));
            view.Timer = Label(root, "Timer", font, 28, Color.white, new Vector2(.76f, .66f), new Vector2(.95f, .82f));
            view.Timer.alignment = TextAlignmentOptions.MidlineRight;
            view.Body = Label(root, "Body", font, 28, Ink, new Vector2(.06f, .27f), new Vector2(.94f, .63f));
            view.Progress = Label(root, "Progress", font, 28, new Color32(0x1B, 0x64, 0x9C, 0xFF), new Vector2(.06f, .11f), new Vector2(.94f, .25f));
            view.Secondary = Label(root, "Secondary", font, 24, new Color32(0x71, 0x36, 0x97, 0xFF), new Vector2(.06f, .08f), new Vector2(.94f, .28f));
            return view;
        }

        public void SetSecondary(string text)
        {
            Secondary.text = text ?? string.Empty;
            Secondary.gameObject.SetActive(!string.IsNullOrEmpty(text));
            // 부가 정보는 종이 아래쪽의 독립 영역에서 표시한다.
            bool visible = !string.IsNullOrEmpty(text);
            Body.rectTransform.anchorMin = new Vector2(.06f, visible ? .38f : .27f);
            Progress.rectTransform.anchorMin = new Vector2(.06f, visible ? .29f : .11f);
            Progress.rectTransform.anchorMax = new Vector2(.94f, visible ? .38f : .25f);
            Secondary.rectTransform.anchorMin = new Vector2(.06f, .08f);
            Secondary.rectTransform.anchorMax = new Vector2(.94f, .28f);
            PixelTextFit.Apply(Title); PixelTextFit.Apply(Timer); PixelTextFit.Apply(Body);
            PixelTextFit.Apply(Progress); PixelTextFit.Apply(Secondary);
        }

        static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, float size, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = label.fontSizeMax = size;
            label.fontSizeMin = size == 32 ? 26 : 24;
            label.enableAutoSizing = false;
            label.extraPadding = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.color = color;
            label.outlineWidth = 0;
            label.raycastTarget = false;
            return label;
        }
    }
}
