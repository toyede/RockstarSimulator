using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 모든 일반 관객이 공유하는 Hover 말풍선 하나. 관객마다 Canvas를 만들지 않고
    /// 현재 가리키는 Actor의 화면 위치만 따라간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudienceHoverDialogueView : MonoBehaviour
    {
        Canvas _canvas;
        CanvasGroup _canvasGroup;
        RectTransform _bubble;
        Image _background;
        Outline _outline;
        TMP_Text _dialogueText;
        AudienceMemberActor _target;
        AudiencePreferenceHoverConfig _config;
        Camera _worldCamera;
        float _targetAlpha;
        Coroutine _typingRoutine;

        static readonly char[] CompatibilityInitials =
        {
            'ㄱ', 'ㄲ', 'ㄴ', 'ㄷ', 'ㄸ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅃ', 'ㅅ',
            'ㅆ', 'ㅇ', 'ㅈ', 'ㅉ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ',
        };

        public bool IsVisible => _target != null && _targetAlpha > 0f;

        public void Configure(
            AudiencePreferenceHoverConfig config,
            Camera worldCamera)
        {
            _config = config;
            _worldCamera = worldCamera != null ? worldCamera : Camera.main;
            EnsureVisualTree();
            ApplyStyle();
            Hide(immediate: true);
        }

        public void Show(
            AudienceMemberActor target,
            string richText,
            Color accentColor)
        {
            if (target == null || string.IsNullOrWhiteSpace(richText))
            {
                Hide(immediate: false);
                return;
            }

            EnsureVisualTree();
            _target = target;
            StopTyping();
            if (_config != null && _config.UseHangulTypewriter && isActiveAndEnabled)
            {
                _dialogueText.text = string.Empty;
                _typingRoutine = StartCoroutine(TypeDialogue(richText));
            }
            else
            {
                _dialogueText.text = richText;
            }
            _outline.effectColor = accentColor;
            _targetAlpha = 1f;
            UpdatePosition();
        }

        public void Hide(bool immediate)
        {
            StopTyping();
            _targetAlpha = 0f;
            if (!immediate) return;

            _target = null;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        void OnDisable()
        {
            StopTyping();
        }

        IEnumerator TypeDialogue(string richText)
        {
            List<TypeFrame> frames = BuildTypingFrames(richText);
            float interval = _config != null ? _config.DialogueTypingInterval : 0.035f;

            for (int i = 0; i < frames.Count; i++)
            {
                _dialogueText.text = frames[i].Text;
                float wait = interval + frames[i].ExtraDelay;
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            }

            _dialogueText.text = richText;
            _typingRoutine = null;
        }

        List<TypeFrame> BuildTypingFrames(string richText)
        {
            var frames = new List<TypeFrame>(richText.Length * 2);
            var committed = new StringBuilder(richText.Length + 16);

            for (int i = 0; i < richText.Length; i++)
            {
                char value = richText[i];
                if (value == '<')
                {
                    int tagEnd = richText.IndexOf('>', i);
                    if (tagEnd >= i)
                    {
                        committed.Append(richText, i, tagEnd - i + 1);
                        i = tagEnd;
                        continue;
                    }
                }

                if (TryDecomposeHangul(value, out char initial, out char medialForm, out bool hasFinal))
                {
                    frames.Add(new TypeFrame(committed.ToString() + initial, 0f));
                    frames.Add(new TypeFrame(committed.ToString() + medialForm, 0f));
                    if (hasFinal)
                        frames.Add(new TypeFrame(committed.ToString() + value, 0f));
                    committed.Append(value);
                    continue;
                }

                committed.Append(value);
                if (char.IsWhiteSpace(value)) continue;

                float punctuationDelay = IsPunctuation(value) && _config != null
                    ? _config.DialoguePunctuationDelay
                    : 0f;
                frames.Add(new TypeFrame(committed.ToString(), punctuationDelay));
            }

            if (frames.Count == 0 || frames[frames.Count - 1].Text != richText)
                frames.Add(new TypeFrame(richText, 0f));
            return frames;
        }

        static bool TryDecomposeHangul(
            char value,
            out char initial,
            out char medialForm,
            out bool hasFinal)
        {
            const int hangulBase = 0xAC00;
            const int hangulLast = 0xD7A3;
            int code = value;
            if (code < hangulBase || code > hangulLast)
            {
                initial = default;
                medialForm = default;
                hasFinal = false;
                return false;
            }

            int syllableIndex = code - hangulBase;
            int initialIndex = syllableIndex / 588;
            int medialIndex = (syllableIndex % 588) / 28;
            int finalIndex = syllableIndex % 28;

            initial = CompatibilityInitials[initialIndex];
            medialForm = (char)(hangulBase + initialIndex * 588 + medialIndex * 28);
            hasFinal = finalIndex > 0;
            return true;
        }

        static bool IsPunctuation(char value) =>
            value == '.' || value == ',' || value == '!' || value == '?' ||
            value == '…' || value == '。' || value == '！' || value == '？';

        void StopTyping()
        {
            if (_typingRoutine == null) return;
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
        }

        readonly struct TypeFrame
        {
            public TypeFrame(string text, float extraDelay)
            {
                Text = text;
                ExtraDelay = extraDelay;
            }

            public string Text { get; }
            public float ExtraDelay { get; }
        }

        void LateUpdate()
        {
            if (_canvasGroup == null) return;

            float speed = _config != null ? _config.DialogueFadeSpeed : 12f;
            _canvasGroup.alpha = speed <= 0f
                ? _targetAlpha
                : Mathf.MoveTowards(
                    _canvasGroup.alpha,
                    _targetAlpha,
                    speed * Time.unscaledDeltaTime);

            if (_targetAlpha <= 0f && _canvasGroup.alpha <= 0.001f)
            {
                _canvasGroup.alpha = 0f;
                _target = null;
                return;
            }

            UpdatePosition();
        }

        void UpdatePosition()
        {
            if (_target == null || _bubble == null) return;
            if (_worldCamera == null) _worldCamera = Camera.main;
            if (_worldCamera == null)
            {
                Hide(immediate: true);
                return;
            }

            float offset = _config != null ? _config.DialogueWorldOffset : 1.15f;
            Vector3 anchor = _target.PreferenceHoverCenter + Vector2.up * offset;
            Vector3 screen = _worldCamera.WorldToScreenPoint(anchor);
            if (screen.z <= 0f)
            {
                Hide(immediate: true);
                return;
            }

            float scale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;
            Vector2 size = _bubble.rect.size * scale;
            float x = Mathf.Clamp(screen.x, size.x * 0.5f + 12f, Screen.width - size.x * 0.5f - 12f);
            float y = Mathf.Clamp(screen.y, size.y * 0.5f + 12f, Screen.height - size.y * 0.5f - 12f);
            _bubble.position = new Vector3(x, y, 0f);
        }

        void EnsureVisualTree()
        {
            if (_canvas != null) return;

            var canvasObject = new GameObject(
                "AudienceHoverDialogueCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            var bubbleObject = new GameObject(
                "AudienceThoughtBubble",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline));
            bubbleObject.transform.SetParent(canvasObject.transform, false);
            _bubble = bubbleObject.GetComponent<RectTransform>();
            _bubble.anchorMin = new Vector2(0.5f, 0.5f);
            _bubble.anchorMax = new Vector2(0.5f, 0.5f);
            // The screen clamping code works from the bubble centre. Keeping the
            // pivot centred prevents the lower half from being pushed off-screen.
            _bubble.pivot = new Vector2(0.5f, 0.5f);

            _background = bubbleObject.GetComponent<Image>();
            _background.raycastTarget = false;

            _outline = bubbleObject.GetComponent<Outline>();
            _outline.effectDistance = new Vector2(3f, -3f);
            _outline.useGraphicAlpha = true;

            var textObject = new GameObject(
                "DialogueText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(bubbleObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(22f, 12f);
            textRect.offsetMax = new Vector2(-22f, -12f);

            _dialogueText = textObject.GetComponent<TextMeshProUGUI>();
            _dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            _dialogueText.textWrappingMode = TextWrappingModes.Normal;
            _dialogueText.overflowMode = TextOverflowModes.Ellipsis;
            _dialogueText.richText = true;
            _dialogueText.raycastTarget = false;
            _dialogueText.color = Color.white;
        }

        void ApplyStyle()
        {
            if (_config == null || _bubble == null) return;

            _bubble.sizeDelta = _config.DialogueSize;
            _background.color = _config.DialogueBackground;
            _dialogueText.fontSize = _config.DialogueFontSize;
            if (_config.DialogueFont != null)
                _dialogueText.font = _config.DialogueFont;
        }
    }
}
