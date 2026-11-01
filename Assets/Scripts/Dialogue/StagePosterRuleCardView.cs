using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>포스터 자체가 공연 설명 화면. 포스터가 없으면 원래 수동 배치와 스타일을 복구한다.</summary>
    public sealed class StagePosterRuleCardView : MonoBehaviour
    {
        struct Layout
        {
            public Vector2 Min, Max, Pivot, Position, Size;
            public Quaternion Rotation;
            public Layout(RectTransform rect)
            { Min = rect.anchorMin; Max = rect.anchorMax; Pivot = rect.pivot; Position = rect.anchoredPosition; Size = rect.sizeDelta; Rotation=rect.localRotation; }
            public void Restore(RectTransform rect)
            { rect.anchorMin = Min; rect.anchorMax = Max; rect.pivot = Pivot; rect.anchoredPosition = Position; rect.sizeDelta = Size; rect.localRotation=Rotation; }
        }
        readonly Dictionary<RectTransform, Layout> _originals = new Dictionary<RectTransform, Layout>();
        readonly Dictionary<TMP_Text, TextStyle> _textStyles = new Dictionary<TMP_Text, TextStyle>();
        readonly Dictionary<Graphic, Color> _graphicColors = new Dictionary<Graphic, Color>();
        readonly Dictionary<Behaviour, bool> _enabledStates = new Dictionary<Behaviour, bool>();
        Image _poster;
        Image _logo, _stampTop, _stampBottom;
        RectTransform _entryArrow;
        Image _arrowUpper, _arrowLower;
        TMP_Text _artist, _ruleLabel;
        Button _confirm;
        Selectable.Transition _confirmTransition;
        StagePosterEntryFeedback _entryFeedback;

        struct TextStyle
        {
            public Color Color;
            public float Size, Min, Max, Outline, Spacing;
            public bool Auto;
            public FontStyles FontStyle;
            public TextAlignmentOptions Alignment;
            public TMP_FontAsset Font;
            public TextStyle(TMP_Text text)
            { Color=text.color; Size=text.fontSize; Min=text.fontSizeMin; Max=text.fontSizeMax; Outline=text.outlineWidth; Spacing=text.lineSpacing; Auto=text.enableAutoSizing; FontStyle=text.fontStyle; Alignment=text.alignment; Font=text.font; }
            public void Restore(TMP_Text text)
            { text.font=Font; text.color=Color; text.fontSize=Size; text.fontSizeMin=Min; text.fontSizeMax=Max; text.outlineWidth=Outline; text.lineSpacing=Spacing; text.enableAutoSizing=Auto; text.fontStyle=FontStyle; text.alignment=Alignment; }
        }

        public void Show(Sprite sprite, TMP_Text title, TMP_Text body, Button confirm,
            StagePosterPresentation presentation = null, string ruleTitle = null)
        {
            var root = (RectTransform)transform;
            Remember(root);
            if (title != null) Remember(title.rectTransform);
            if (body != null) Remember(body.rectTransform);
            if (confirm != null) Remember((RectTransform)confirm.transform);
            RememberText(title); RememberText(body);
            var confirmLabel = confirm != null ? confirm.GetComponentInChildren<TMP_Text>(true) : null;
            RememberText(confirmLabel);
            if (confirmLabel != null) Remember(confirmLabel.rectTransform);
            RememberEnabled(root.GetComponent<Image>());
            RememberEnabled(root.GetComponent<Outline>());
            if (confirm != null)
            {
                if (_confirm == null) { _confirm=confirm; _confirmTransition=confirm.transition; }
                var background = confirm.GetComponent<Graphic>();
                if (background != null && !_graphicColors.ContainsKey(background)) _graphicColors.Add(background, background.color);
                RememberEnabled(confirm.GetComponent<Outline>());
            }
            if (_entryFeedback != null) _entryFeedback.enabled=false;
            foreach (var item in _originals) item.Value.Restore(item.Key);
            foreach (var item in _textStyles) item.Value.Restore(item.Key);
            foreach (var item in _graphicColors) item.Key.color = item.Value;
            foreach (var item in _enabledStates) item.Key.enabled = item.Value;
            if (_confirm != null) _confirm.transition=_confirmTransition;
            if (title != null) title.gameObject.SetActive(true);
            SetExtraVisible(false);
            if (_poster != null) _poster.gameObject.SetActive(sprite != null);
            if (sprite == null) return;
            presentation = ResolvePresentation(sprite, presentation);
            // 포스터 자체가 화면이다. 기존 회색 카드/흰 테두리는 그리지 않는다.
            root.sizeDelta = new Vector2(presentation.height * sprite.rect.width / sprite.rect.height, presentation.height);
            if (root.TryGetComponent<Image>(out var panelImage)) panelImage.enabled = false;
            if (root.TryGetComponent<Outline>(out var panelOutline)) panelOutline.enabled = false;
            if (_poster == null)
            {
                var go = new GameObject("StagePoster", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                _poster = go.GetComponent<Image>();
                _poster.preserveAspect = true;
                _poster.raycastTarget = true;
            }
            _poster.gameObject.SetActive(true);
            _poster.sprite = sprite;
            _poster.transform.SetAsFirstSibling();
            var rect = _poster.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (title != null)
            {
                if (!string.IsNullOrWhiteSpace(presentation.headline)) title.text=presentation.headline;
                if (presentation.headlineFont != null) title.font=presentation.headlineFont;
                PlaceText(title, presentation.headlineArea, presentation.headlineColor, presentation.headlineSize);
                title.fontStyle=FontStyles.Bold;
                title.rectTransform.localRotation=Quaternion.Euler(0,0,presentation.headlineAngle);
                title.gameObject.SetActive(presentation.headlineLogo == null);
            }
            if (_logo == null) _logo=CreateImage(transform,"PosterHeadlineLogo");
            _logo.gameObject.SetActive(presentation.headlineLogo != null);
            _logo.sprite=presentation.headlineLogo; _logo.preserveAspect=true;
            Place(_logo.rectTransform,presentation.headlineArea);
            if (body != null)
            {
                if (!string.IsNullOrWhiteSpace(presentation.summary)) body.text=presentation.summary;
                PlaceText(body, presentation.bodyArea, presentation.inkColor, presentation.bodySize);
                body.alignment=presentation.bodyAlignment;
            }
            if (_artist == null) _artist=CreateText("PosterArtist",body != null ? body : title);
            _artist.text=presentation.artistLine;
            _artist.gameObject.SetActive(!string.IsNullOrWhiteSpace(_artist.text));
            PlaceText(_artist,presentation.artistArea,presentation.headlineColor,presentation.captionSize);
            if (_ruleLabel == null) _ruleLabel=CreateText("PosterRuleLabel",body != null ? body : title);
            _ruleLabel.text=string.IsNullOrWhiteSpace(ruleTitle) ? "공연 가이드" : $"공연 가이드 / {ruleTitle}";
            _ruleLabel.gameObject.SetActive(presentation.showRuleLabel);
            PlaceText(_ruleLabel,presentation.ruleLabelArea,presentation.accentColor,presentation.captionSize);
            _ruleLabel.alignment=TextAlignmentOptions.MidlineLeft;
            if (confirm != null)
            {
                var button = (RectTransform)confirm.transform;
                Place(button, presentation.entryArea);
                button.localRotation=Quaternion.Euler(0,0,presentation.entryAngle);
                confirm.transition=Selectable.Transition.None;
                if (confirm.TryGetComponent<Graphic>(out var background)) background.color = Color.clear;
                if (confirm.TryGetComponent<Outline>(out var outline)) outline.enabled = false;
                PlaceText(confirmLabel, new Rect(.05f,.12f,.75f,.76f), presentation.inkColor, presentation.entrySize);
                if (confirmLabel != null) confirmLabel.fontStyle=FontStyles.Bold;
                // 폰트에 없는 삼각형 문자가 네모로 대체되지 않도록 실제 선 두 개로 그린다.
                if (_entryArrow == null)
                {
                    _entryArrow=new GameObject("EntryArrow",typeof(RectTransform)).GetComponent<RectTransform>();
                    _entryArrow.SetParent(button,false);
                    _arrowUpper=CreateImage(_entryArrow,"Upper"); _arrowLower=CreateImage(_entryArrow,"Lower");
                    ConfigureArrowLine(_arrowUpper.rectTransform,4f,-45f);
                    ConfigureArrowLine(_arrowLower.rectTransform,-4f,45f);
                }
                Place(_entryArrow,new Rect(.83f,.32f,.08f,.36f));
                _entryArrow.gameObject.SetActive(true);
                if (_stampTop == null) _stampTop=CreateImage(button,"EntryStampTop");
                if (_stampBottom == null) _stampBottom=CreateImage(button,"EntryStampBottom");
                Place(_stampTop.rectTransform,new Rect(.03f,.92f,.94f,.03f));
                Place(_stampBottom.rectTransform,new Rect(.03f,.05f,.94f,.03f));
                _stampTop.gameObject.SetActive(true); _stampBottom.gameObject.SetActive(true);
                if (_entryFeedback == null) _entryFeedback=confirm.gameObject.AddComponent<StagePosterEntryFeedback>();
                _entryFeedback.enabled=true;
                _entryFeedback.Configure(confirm,confirmLabel,_stampTop,_stampBottom,_arrowUpper,_arrowLower,presentation.inkColor,presentation.accentColor);
            }
        }
        void Remember(RectTransform rect)
        { if (!_originals.ContainsKey(rect)) _originals.Add(rect, new Layout(rect)); }
        void RememberText(TMP_Text text)
        { if (text != null && !_textStyles.ContainsKey(text)) _textStyles.Add(text, new TextStyle(text)); }
        void RememberEnabled(Behaviour component)
        { if (component != null && !_enabledStates.ContainsKey(component)) _enabledStates.Add(component, component.enabled); }
        static StagePosterPresentation ResolvePresentation(Sprite sprite, StagePosterPresentation requested)
        {
            if (requested != null) return requested;
            var catalog=StageVisualCatalog.LoadDefault();
            if (catalog != null)
                foreach(var entry in catalog.Entries)
                    if(entry != null && entry.stagePoster == sprite && entry.posterPresentation != null) return entry.posterPresentation;
            return new StagePosterPresentation();
        }
        void SetExtraVisible(bool visible)
        {
            if(_logo!=null)_logo.gameObject.SetActive(visible);
            if(_artist!=null)_artist.gameObject.SetActive(visible);
            if(_ruleLabel!=null)_ruleLabel.gameObject.SetActive(visible);
            if(_stampTop!=null)_stampTop.gameObject.SetActive(visible);
            if(_stampBottom!=null)_stampBottom.gameObject.SetActive(visible);
            if(_entryArrow!=null)_entryArrow.gameObject.SetActive(visible);
        }
        static void ConfigureArrowLine(RectTransform rect,float y,float angle)
        {
            rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(14f,2.5f);rect.anchoredPosition=new Vector2(0,y);
            rect.localRotation=Quaternion.Euler(0,0,angle);
        }
        TMP_Text CreateText(string name, TMP_Text source)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
            go.transform.SetParent(transform,false);
            var text=go.GetComponent<TextMeshProUGUI>();
            if(source!=null)text.font=source.font;
            text.raycastTarget=false;
            return text;
        }
        static Image CreateImage(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>(); image.raycastTarget=false;
            return image;
        }
        static void PlaceText(TMP_Text text, Rect area, Color color, float size)
        {
            if (text == null) return;
            Place(text.rectTransform, area);
            text.color = color; text.fontSize = size;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(16f,size-2f); text.fontSizeMax = size;
            text.lineSpacing = 3f; text.alignment = TextAlignmentOptions.Center;
            text.outlineWidth = color.r > .7f ? .12f : 0f;
        }
        static void Place(RectTransform rect, Rect area)
        {
            rect.anchorMin = area.min; rect.anchorMax = area.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
