using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>판정 로직 없이 슬레이트 아트와 텍스트 영역만 구성한다.</summary>
    public sealed class MissionCueSheetView : MonoBehaviour
    {
        public static readonly Color Ink = new Color32(0xF1, 0xF3, 0xEF, 0xFF);
        public TMP_Text Title { get; private set; }
        public TMP_Text Body { get; private set; }
        public TMP_Text Progress { get; private set; }
        public TMP_Text Timer { get; private set; }
        public TMP_Text Secondary { get; private set; }
        readonly Image[] _bits = new Image[12];
        Coroutine _entrance;
        Vector2 _home;
        bool _presenting;
        CanvasGroup _group;

        public void PlayEntrance() => Animate(false, true);
        public void PlayResult(bool success) => Animate(true, success);

        void Animate(bool result, bool success)
        {
            if (!isActiveAndEnabled) return;
            if (_entrance != null) StopCoroutine(_entrance);
            var root = (RectTransform)transform;
            if (!_presenting) _home = root.anchoredPosition;
            _presenting = true;
            if (_group == null) _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _entrance = StartCoroutine(Present(result, success));
        }

        IEnumerator Present(bool result, bool success)
        {
            var root = (RectTransform)transform;
            Color accent = result ? success ? new Color32(80,225,185,255) : new Color32(255,135,78,255) : new Color32(255,215,125,255);
            for (int i = 0; i < _bits.Length; i++)
            {
                if (_bits[i] == null)
                {
                    var go = new GameObject("CuePaperPixel", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(transform, false); go.transform.SetAsFirstSibling();
                    _bits[i] = go.GetComponent<Image>(); _bits[i].raycastTarget = false;
                    _bits[i].rectTransform.sizeDelta = new Vector2(i % 3 == 0 ? 24 : 10, 6);
                }
                _bits[i].gameObject.SetActive(true);
            }
            float age = 0f;
            while (age < .48f)
            {
                if (GameJamKit.GameManager.HasInstance && GameJamKit.GameManager.Instance.State == GameJamKit.GameState.Paused) { yield return null; continue; }
                age += Time.deltaTime;
                float entry = Mathf.Clamp01(age / .2f);
                root.anchoredPosition = _home + Vector2.left * (18f * (1f-entry) * (1f-entry));
                _group.alpha = Mathf.Lerp(.85f, 1f, entry);
                for (int i = 0; i < _bits.Length; i++)
                {
                    bool left = (i & 1) == 0;
                    var rect = _bits[i].rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(left ? 0f : 1f, .66f);
                    rect.anchoredPosition = new Vector2((left ? -1 : 1) * (12f + age*(80f+i*7)),
                        (i%3)*12f + age*(result && !success ? -100f : 90f) - 80f*age*age);
                    rect.localRotation = Quaternion.Euler(0,0,i*25+age*160);
                    _bits[i].color = new Color(accent.r,accent.g,accent.b,1f-age/.48f);
                }
                yield return null;
            }
            ResetPresentation();
        }

        void OnDisable()
        {
            if (_entrance != null) StopCoroutine(_entrance);
            ResetPresentation();
        }

        void ResetPresentation()
        {
            if (_presenting) ((RectTransform)transform).anchoredPosition = _home;
            _presenting = false; _entrance = null;
            if (_group != null) _group.alpha = 1f;
            foreach (var bit in _bits) if (bit != null) bit.gameObject.SetActive(false);
        }

        public static MissionCueSheetView Create(RectTransform root, TMP_FontAsset font)
        {
            var view = root.gameObject.AddComponent<MissionCueSheetView>();
            var image = root.gameObject.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>("UI/CueSheets/MissionSlate");
            image.color = image.sprite != null ? Color.white : new Color32(0xE2, 0xE8, 0xDF, 0xFF);
            image.raycastTarget = false;
            // 새 아트는 글을 쓰는 판 내부가 투명하다. 테두리 안쪽만 채우고 빗금 아래에 글을 둔다.
            var backing = new GameObject("SlateBacking", typeof(RectTransform), typeof(Image));
            var backingRect = backing.GetComponent<RectTransform>();
            backingRect.SetParent(root, false);
            backingRect.anchorMin = new Vector2(.025f, .025f);
            backingRect.anchorMax = new Vector2(.975f, .60f);
            backingRect.offsetMin = backingRect.offsetMax = Vector2.zero;
            var backingImage = backing.GetComponent<Image>();
            backingImage.color = new Color32(0x10, 0x10, 0x10, 0xFA);
            backingImage.raycastTarget = false;
            view.Title = Label(root, "Title", font, 32, Color.white, new Vector2(.06f, .49f), new Vector2(.75f, .60f));
            view.Timer = Label(root, "Timer", font, 28, Color.white, new Vector2(.76f, .49f), new Vector2(.95f, .60f));
            view.Timer.alignment = TextAlignmentOptions.MidlineRight;
            view.Body = Label(root, "Body", font, 28, Ink, new Vector2(.06f, .22f), new Vector2(.94f, .48f));
            view.Progress = Label(root, "Progress", font, 28, new Color32(0x76, 0xD6, 0xFF, 0xFF), new Vector2(.06f, .11f), new Vector2(.94f, .25f));
            view.Secondary = Label(root, "Secondary", font, 24, new Color32(0xDB, 0xAF, 0xFF, 0xFF), new Vector2(.06f, .08f), new Vector2(.94f, .28f));
            return view;
        }

        public void SetSecondary(string text)
        {
            Secondary.text = text ?? string.Empty;
            Secondary.gameObject.SetActive(!string.IsNullOrEmpty(text));
            // 부가 정보는 종이 아래쪽의 독립 영역에서 표시한다.
            bool visible = !string.IsNullOrEmpty(text);
            Body.rectTransform.anchorMin = new Vector2(.06f, visible ? .31f : .22f);
            Progress.rectTransform.anchorMin = new Vector2(.06f, visible ? .23f : .08f);
            Progress.rectTransform.anchorMax = new Vector2(.94f, visible ? .30f : .20f);
            Secondary.rectTransform.anchorMin = new Vector2(.06f, .04f);
            Secondary.rectTransform.anchorMax = new Vector2(.94f, .22f);
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
