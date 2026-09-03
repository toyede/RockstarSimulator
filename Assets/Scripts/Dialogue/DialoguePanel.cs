using System;
using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 대화창 화면 + 진행 제어. (기획서 §14 DialogueController)
    ///
    /// - 클릭 / Space / Enter / 모바일 탭 → 다음
    /// - 타이핑 중 입력 → 현재 줄 즉시 완성, 완성된 줄에서 입력 → 다음 줄
    /// - 마지막 줄 뒤에 룰 카드(있으면) → 확인 → onComplete 한 번만 호출
    /// - 스킵 → 대사를 전부 건너뛰되 룰 카드와 마지막 전환은 그대로 한 번
    ///
    /// 표현 수치는 <see cref="DialogueStyle"/> 에셋에서 읽고, 하위 오브젝트 참조는 프리팹 또는
    /// <see cref="DialoguePanelFactory"/> 가 <see cref="Bind"/> 로 채운다. 모든 참조는 null-safe.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class DialoguePanel : MonoBehaviour
    {
        /// <summary>팩토리·프리팹이 하위 참조를 한 번에 넘길 때 쓰는 묶음.</summary>
        public struct Parts
        {
            public Image backdrop;
            public Image dim;
            public GameObject venueRoot;
            public TMP_Text venueText;
            public Image venueIcon;
            public Image portraitLeft;
            public Image portraitRight;
            public GameObject boxRoot;
            public Image boxBackground;
            public Outline boxOutline;
            public Image nameTag;
            public TMP_Text nameText;
            public TMP_Text bodyText;
            public Image arrow;
            public Button advanceButton;
            public Button skipButton;
            public TMP_Text skipLabel;
            public Image skipKeyIcon;
            public UIBlurBackdrop blurBackdrop;
            public GameObject ruleCardRoot;
            public Image ruleCardBackground;
            public Outline ruleCardOutline;
            public Image ruleIcon;
            public TMP_Text ruleTitle;
            public TMP_Text ruleBody;
            public Button ruleConfirmButton;
            public TMP_Text ruleConfirmLabel;
        }

        enum State
        {
            Hidden,
            Typing,
            LineComplete,
            RuleCard,
            Finishing
        }

        [Header("스타일")]
        [SerializeField, Tooltip("비워두면 DialogueCatalog 의 스타일을 쓴다")]
        DialogueStyle style;
        [SerializeField, Tooltip(
            "켜면 상자·이름·본문·화살표·스킵 키의 위치와 크기를 DialogueStyle 값으로 매번 덮어쓴다. " +
            "씬/프리팹에서 직접 배치해 쓰려면 끈다 (기본). 폰트·색·스프라이트는 항상 스타일을 따른다")]
        bool layoutFromStyle = false;

        [Header("루트")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField, Tooltip("이전 화면 블러 (일시정지와 같은 UIBlurBackdrop). 스타일의 useScreenBlur 가 켜져 있을 때만 켠다")]
        UIBlurBackdrop blurBackdrop;
        [SerializeField] Image backdropImage;
        [SerializeField] Image dimImage;

        [Header("공연장 표시 (좌상단)")]
        [SerializeField] GameObject venueRoot;
        [SerializeField] TMP_Text venueText;
        [SerializeField] Image venueIcon;

        [Header("초상화")]
        [SerializeField] Image portraitLeft;
        [SerializeField] Image portraitRight;

        [Header("대사 상자")]
        [SerializeField] GameObject boxRoot;
        [SerializeField] Image boxBackground;
        [SerializeField] Outline boxOutline;
        [SerializeField] Image nameTag;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text bodyText;
        [SerializeField, Tooltip("줄이 끝나면 위아래로 튀는 '다음' 화살표")]
        Image arrowImage;

        [Header("입력")]
        [SerializeField, Tooltip("화면 전체를 덮는 투명 버튼. 클릭·탭으로 진행")]
        Button advanceButton;
        [SerializeField] Button skipButton;
        [SerializeField] TMP_Text skipLabel;
        [SerializeField, Tooltip("스킵 키 아이콘 ([F])")] Image skipKeyIcon;

        [Header("룰 카드 (마지막에 표시)")]
        [SerializeField] GameObject ruleCardRoot;
        [SerializeField] Image ruleCardBackground;
        [SerializeField] Outline ruleCardOutline;
        [SerializeField] Image ruleIconImage;
        [SerializeField] TMP_Text ruleTitleText;
        [SerializeField] TMP_Text ruleBodyText;
        [SerializeField] Button ruleConfirmButton;
        [SerializeField] TMP_Text ruleConfirmLabel;

        [Header("문구")]
        [SerializeField, Tooltip("DungGeunMo 에 ▶ 글리프가 없어 >> 를 쓴다")]
        string skipText = "건너뛰기 >>";
        [SerializeField] string defaultConfirmText = "공연 시작";

        public bool IsPlaying => _state != State.Hidden;
        public bool IsTyping => _state == State.Typing;
        public int CurrentLineIndex => _lineIndex;

        /// <summary>디버그·테스트용 현재 상태 이름 (Hidden / Typing / LineComplete / RuleCard / Finishing).</summary>
        public string DebugState => _state.ToString();

        /// <summary>true 면 진행 로그를 콘솔에 남긴다 (검증용).</summary>
        public bool VerboseLogs { get; set; }

        /// <summary>대화(와 룰 카드)가 끝났을 때. Play 에 넘긴 콜백과 별개로 항상 발행된다.</summary>
        public event Action Completed;

        DialogueSequence _sequence;
        DialoguePresentationContext _context;
        Action _onComplete;
        State _state = State.Hidden;
        int _lineIndex = -1;
        Coroutine _typingRoutine;
        Coroutine _fadeRoutine;
        RectTransform _arrowRect;
        Vector2 _arrowBasePosition;
        Sprite _generatedArrow;
        bool _completeInvoked;
        bool _listenersBound;

        int LineCount => _sequence != null ? _sequence.LineCount : 0;

        // ---------------- 수명 주기 ----------------

        void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            CacheArrow();
            BindListeners();
            ApplyStyle();
            // Play() 가 SetActive(true) 로 깨우는 순간에도 Awake 가 돌기 때문에
            // 여기서는 오브젝트를 끄지 않고 보이지만 않게 정리한다.
            PrepareHidden();
        }

        void OnDestroy()
        {
            if (advanceButton != null) advanceButton.onClick.RemoveListener(Advance);
            if (skipButton != null) skipButton.onClick.RemoveListener(Skip);
            if (ruleConfirmButton != null) ruleConfirmButton.onClick.RemoveListener(ConfirmRuleCard);
            if (_generatedArrow != null)
            {
                if (_generatedArrow.texture != null) Destroy(_generatedArrow.texture);
                Destroy(_generatedArrow);
            }
        }

        void Update()
        {
            if (_state == State.Hidden || _state == State.Finishing) return;

            if (AdvancePressedThisFrame()) Advance();
            else if ((style == null || style.SkipWithFKey) && SkipPressedThisFrame()) Skip();

            if (_state == State.LineComplete) AnimateArrow();
        }

        bool UseScreenBlur => blurBackdrop != null && (style == null || style.UseScreenBlur);

        // ---------------- 공개 API ----------------

        /// <summary>프리팹 없이 코드로 만든 계층을 연결한다. (DialoguePanelFactory 전용)</summary>
        public void Bind(Parts parts)
        {
            backdropImage = parts.backdrop;
            dimImage = parts.dim;
            venueRoot = parts.venueRoot;
            venueText = parts.venueText;
            venueIcon = parts.venueIcon;
            portraitLeft = parts.portraitLeft;
            portraitRight = parts.portraitRight;
            boxRoot = parts.boxRoot;
            boxBackground = parts.boxBackground;
            boxOutline = parts.boxOutline;
            nameTag = parts.nameTag;
            nameText = parts.nameText;
            bodyText = parts.bodyText;
            arrowImage = parts.arrow;
            advanceButton = parts.advanceButton;
            skipButton = parts.skipButton;
            skipLabel = parts.skipLabel;
            skipKeyIcon = parts.skipKeyIcon;
            blurBackdrop = parts.blurBackdrop;
            ruleCardRoot = parts.ruleCardRoot;
            ruleCardBackground = parts.ruleCardBackground;
            ruleCardOutline = parts.ruleCardOutline;
            ruleIconImage = parts.ruleIcon;
            ruleTitleText = parts.ruleTitle;
            ruleBodyText = parts.ruleBody;
            ruleConfirmButton = parts.ruleConfirmButton;
            ruleConfirmLabel = parts.ruleConfirmLabel;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            CacheArrow();
            _listenersBound = false; // Awake 가 빈 참조로 먼저 돌았을 수 있다
            BindListeners();
            ApplyStyle();
            HideImmediate();
        }

        /// <summary>스타일을 바꾸고 즉시 적용한다. null 이면 현재 스타일 유지.</summary>
        public void Configure(DialogueStyle newStyle)
        {
            if (newStyle != null) style = newStyle;
            ApplyStyle();
        }

        /// <summary>
        /// 시퀀스를 재생한다. sequence 가 null 이거나 비어 있으면 룰 카드만(있으면) 보여주고 끝난다.
        /// 이미 재생 중이면 현재 대화를 조용히 버리고 새로 시작한다 (이전 콜백은 호출하지 않는다).
        /// </summary>
        public void Play(DialogueSequence sequence, DialoguePresentationContext context, Action onComplete)
        {
            StopTyping();
            StopFade();

            _sequence = sequence;
            _context = context;
            _onComplete = onComplete;
            _completeInvoked = false;
            _lineIndex = -1;

            // 블러는 오브젝트가 켜지는 순간(OnEnable) 이전 화면을 캡처하므로 SetActive 전에 결정한다
            bool useBlur = UseScreenBlur;
            if (blurBackdrop != null) blurBackdrop.gameObject.SetActive(useBlur);

            gameObject.SetActive(true);
            ApplyStyle();
            ApplyContext();
            ResetPortraits();

            if (skipButton != null) skipButton.gameObject.SetActive(true);
            if (ruleCardRoot != null) ruleCardRoot.SetActive(false);

            // 블러 캡처 프레임에는 대화창이 보이지 않아야 하므로 한 프레임 뒤에 페이드를 시작한다
            FadeTo(1f, style != null ? style.FadeInDuration : 0.15f, null, waitOneFrame: useBlur);

            if (LineCount == 0)
            {
                if (boxRoot != null) boxRoot.SetActive(false);
                if (_context.HasRuleCard) ShowRuleCard();
                else Finish();
                return;
            }

            ShowLine(0);
        }

        /// <summary>다음으로. 타이핑 중이면 현재 줄을 즉시 완성한다.</summary>
        public void Advance()
        {
            switch (_state)
            {
                case State.Typing:
                    CompleteLineInstantly();
                    break;
                case State.LineComplete:
                    if (_lineIndex + 1 < LineCount) ShowLine(_lineIndex + 1);
                    else EndLines();
                    break;
                case State.RuleCard:
                    ConfirmRuleCard();
                    break;
            }
        }

        /// <summary>대사를 전부 건너뛴다. 룰 카드가 있으면 룰 카드로, 없으면 바로 종료.</summary>
        public void Skip()
        {
            if (_state == State.Hidden || _state == State.Finishing) return;
            if (_state == State.RuleCard)
            {
                ConfirmRuleCard();
                return;
            }

            EndLines();
        }

        /// <summary>연출 없이 즉시 닫는다. 콜백은 호출하지 않는다 (씬 전환 정리용).</summary>
        public void HideImmediate()
        {
            StopTyping();
            StopFade();
            PrepareHidden();
            gameObject.SetActive(false);
        }

        /// <summary>오브젝트는 켜 둔 채 아무것도 보이지 않게 한다.</summary>
        void PrepareHidden()
        {
            _state = State.Hidden;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (boxRoot != null) boxRoot.SetActive(false);
            if (ruleCardRoot != null) ruleCardRoot.SetActive(false);
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (arrowImage != null) arrowImage.enabled = false;
        }

        // ---------------- 진행 ----------------

        void ShowLine(int index)
        {
            StopTyping();
            _lineIndex = index;
            DialogueLine line = _sequence.Lines[index];

            if (boxRoot != null) boxRoot.SetActive(true);
            if (ruleCardRoot != null) ruleCardRoot.SetActive(false);

            bool hasName = !string.IsNullOrWhiteSpace(line.speakerName);
            if (nameTag != null) nameTag.gameObject.SetActive(hasName);
            if (nameText != null) nameText.text = hasName ? line.speakerName : string.Empty;

            ApplyPortraits(line);

            if (!string.IsNullOrEmpty(line.sfxId)) Sound.Play(line.sfxId);

            if (arrowImage != null) arrowImage.enabled = false;

            string text = line.text ?? string.Empty;
            bool typewriter = style == null || style.UseHangulTypewriter;
            if (bodyText == null || !typewriter || !isActiveAndEnabled)
            {
                if (bodyText != null) bodyText.text = text;
                _state = State.LineComplete;
                if (arrowImage != null) arrowImage.enabled = true;
                return;
            }

            _state = State.Typing;
            bodyText.text = string.Empty;
            _typingRoutine = StartCoroutine(TypeLine(text));
        }

        IEnumerator TypeLine(string text)
        {
            float interval = style != null ? style.TypingInterval : 0.03f;
            float punctuation = style != null ? style.PunctuationDelay : 0.1f;
            string soundId = style != null ? style.TypingSoundId : string.Empty;
            int soundEvery = style != null ? style.TypingSoundEvery : 3;

            List<TypewriterFrame> frames = HangulTypewriter.BuildFrames(text, punctuation);
            for (int i = 0; i < frames.Count; i++)
            {
                bodyText.text = frames[i].Text;
                if (!string.IsNullOrEmpty(soundId) && i % soundEvery == 0) Sound.Play(soundId);

                float wait = interval + frames[i].ExtraDelay;
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            }

            bodyText.text = text;
            _typingRoutine = null;
            _state = State.LineComplete;
            if (arrowImage != null) arrowImage.enabled = true;
        }

        void CompleteLineInstantly()
        {
            StopTyping();
            if (bodyText != null && _sequence != null && _lineIndex >= 0 && _lineIndex < LineCount)
                bodyText.text = _sequence.Lines[_lineIndex].text ?? string.Empty;
            _state = State.LineComplete;
            if (arrowImage != null) arrowImage.enabled = true;
        }

        void EndLines()
        {
            StopTyping();
            if (arrowImage != null) arrowImage.enabled = false;
            if (_context.HasRuleCard) ShowRuleCard();
            else Finish();
        }

        void ShowRuleCard()
        {
            _state = State.RuleCard;
            if (boxRoot != null) boxRoot.SetActive(false);
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (ruleCardRoot == null)
            {
                Finish();
                return;
            }

            ruleCardRoot.SetActive(true);
            if (ruleTitleText != null) ruleTitleText.text = _context.ruleTitle ?? string.Empty;
            if (ruleBodyText != null) ruleBodyText.text = _context.ruleBody ?? string.Empty;
            if (ruleIconImage != null)
            {
                ruleIconImage.sprite = _context.ruleIcon;
                ruleIconImage.enabled = _context.ruleIcon != null;
            }
            if (ruleConfirmLabel != null)
            {
                ruleConfirmLabel.text = string.IsNullOrWhiteSpace(_context.confirmLabel)
                    ? defaultConfirmText
                    : _context.confirmLabel;
            }
        }

        void ConfirmRuleCard()
        {
            if (_state != State.RuleCard) return;
            Finish();
        }

        void Finish()
        {
            if (_state == State.Finishing || _state == State.Hidden) return;
            _state = State.Finishing;
            StopTyping();
            if (arrowImage != null) arrowImage.enabled = false;
            if (canvasGroup != null) canvasGroup.interactable = false;
            Log("Finish → fade out");

            FadeTo(0f, style != null ? style.FadeOutDuration : 0.15f, () =>
            {
                Log("fade out done → hide + complete");
                HideImmediate();
                InvokeComplete();
            });
        }

        void InvokeComplete()
        {
            if (_completeInvoked) return;
            _completeInvoked = true;

            Action callback = _onComplete;
            _onComplete = null;
            Log($"InvokeComplete (callback={(callback != null ? "yes" : "none")})");
            callback?.Invoke();
            Completed?.Invoke();
        }

        void Log(string message)
        {
            if (VerboseLogs) Debug.Log($"[Dialogue] {message} (state={_state}, line={_lineIndex})", this);
        }

        // ---------------- 표현 ----------------

        void ApplyContext()
        {
            bool showVenue = style == null || style.ShowVenueLabel;
            bool hasVenue = showVenue && !string.IsNullOrWhiteSpace(_context.venueName);
            if (venueRoot != null) venueRoot.SetActive(hasVenue);
            if (venueText != null) venueText.text = hasVenue ? _context.venueName : string.Empty;
            if (venueIcon != null)
            {
                venueIcon.sprite = _context.ruleIcon;
                venueIcon.enabled = _context.ruleIcon != null;
            }

            bool useBlur = UseScreenBlur;
            if (backdropImage != null)
            {
                backdropImage.sprite = _context.backdrop;
                backdropImage.enabled = !useBlur && _context.backdrop != null;
                backdropImage.color = style != null ? style.BackdropTint : new Color(0.4f, 0.4f, 0.45f, 1f);
            }

            if (dimImage != null)
            {
                float alpha = style != null ? style.DimAlpha : 0.35f;
                // 블러 배경이면 어둡기는 블러 색이 담당한다. 배경 그림도 블러도 없으면 단색으로 진하게 가린다
                if (useBlur) alpha = Mathf.Min(alpha, 0.15f);
                else if (_context.backdrop == null) alpha = Mathf.Max(alpha, 0.9f);
                dimImage.color = new Color(0f, 0f, 0f, alpha);
            }
        }

        void ResetPortraits()
        {
            if (portraitLeft != null) portraitLeft.enabled = false;
            if (portraitRight != null) portraitRight.enabled = false;
        }

        void ApplyPortraits(DialogueLine line)
        {
            Image active = line.side == DialogueSpeakerSide.Left ? portraitLeft : portraitRight;
            Image other = line.side == DialogueSpeakerSide.Left ? portraitRight : portraitLeft;

            if (active != null)
            {
                if (line.portrait != null)
                {
                    active.sprite = line.portrait;
                    active.enabled = true;
                    active.color = Color.white;
                }
                else
                {
                    active.enabled = false;
                }
            }

            if (other != null && other.enabled)
                other.color = style != null ? style.InactivePortraitColor : new Color(0.45f, 0.45f, 0.5f, 1f);
        }

        void ApplyStyle()
        {
            if (style == null) return;

            TMP_FontAsset font = style.Font;
            ApplyText(bodyText, font, style.BodyFontSize, style.BodyTextColor);
            ApplyText(nameText, font, style.NameFontSize, style.NameTextColor);
            ApplyText(venueText, font, style.VenueFontSize, style.BodyTextColor);
            ApplyText(skipLabel, font, style.NameFontSize * 0.8f, style.BodyTextColor);
            ApplyText(ruleTitleText, font, style.RuleTitleFontSize, style.BoxSprite != null ? style.NameTextColor : style.BoxBorderColor);
            ApplyText(ruleBodyText, font, style.RuleBodyFontSize, style.BodyTextColor);
            ApplyText(ruleConfirmLabel, font, style.NameFontSize, style.NameTextColor);

            // 상자: 아트가 있으면 그림을 통째로 늘려 쓰고(테두리·이름 띠 포함), 없으면 단색 + 테두리
            bool hasBoxArt = style.BoxSprite != null;
            if (boxBackground != null)
            {
                boxBackground.sprite = hasBoxArt ? style.BoxSprite : null;
                boxBackground.type = Image.Type.Simple;
                boxBackground.preserveAspect = false;
                boxBackground.color = hasBoxArt ? Color.white : style.BoxColor;
            }
            if (boxOutline != null)
            {
                boxOutline.effectColor = style.BoxBorderColor;
                boxOutline.enabled = !hasBoxArt;
            }
            if (nameTag != null)
            {
                nameTag.color = style.NameTagColor;
                nameTag.enabled = !hasBoxArt;
            }
            if (bodyText != null)
                bodyText.alignment = style.CenterBodyText ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;

            if (layoutFromStyle) ApplyLayoutFromStyle();
            // 룰 카드는 상자와 같은 팔레트(검정 + 흰 테두리)를 쓴다. 상자 아트가 없을 때만 스타일 색을 따른다
            if (!hasBoxArt)
            {
                if (ruleCardBackground != null && ruleCardBackground.sprite == null) ruleCardBackground.color = style.BoxColor;
                if (ruleCardOutline != null) ruleCardOutline.effectColor = style.BoxBorderColor;
            }

            if (arrowImage != null)
            {
                arrowImage.color = style.ArrowColor;
                Sprite arrow = style.ArrowSprite;
                if (arrow == null) arrow = EnsureGeneratedArrow();
                arrowImage.sprite = arrow;
                arrowImage.preserveAspect = true;
            }

            if (skipKeyIcon != null)
            {
                skipKeyIcon.sprite = style.SkipKeySprite;
                skipKeyIcon.enabled = style.SkipKeySprite != null;
            }
            if (skipLabel != null)
                skipLabel.text = string.IsNullOrEmpty(style.SkipLabelText) ? skipText : style.SkipLabelText;
        }

        /// <summary>
        /// 상자·이름·본문·화살표·스킵 키의 위치와 크기를 DialogueStyle 값으로 맞춘다.
        /// 팩토리가 처음 만들 때 한 번 쓰고, 그 뒤로는 씬/프리팹에 배치된 값이 기준이다 (layoutFromStyle 이 켜져 있지 않은 한).
        /// </summary>
        public void ApplyLayoutFromStyle()
        {
            if (style == null) return;

            if (boxBackground != null)
            {
                var boxRect = boxBackground.rectTransform;
                boxRect.sizeDelta = style.BoxSize;
                boxRect.anchoredPosition = style.BoxOffset;
            }
            if (nameTag != null)
            {
                nameTag.rectTransform.anchorMin = style.NameAreaMin;
                nameTag.rectTransform.anchorMax = style.NameAreaMax;
                nameTag.rectTransform.offsetMin = Vector2.zero;
                nameTag.rectTransform.offsetMax = Vector2.zero;
            }
            if (bodyText != null)
            {
                bodyText.rectTransform.anchorMin = style.BodyAreaMin;
                bodyText.rectTransform.anchorMax = style.BodyAreaMax;
                bodyText.rectTransform.offsetMin = Vector2.zero;
                bodyText.rectTransform.offsetMax = Vector2.zero;
            }
            if (_arrowRect == null) CacheArrow();
            if (_arrowRect != null)
            {
                _arrowRect.sizeDelta = style.ArrowSize;
                _arrowRect.anchoredPosition = style.ArrowOffset;
                _arrowBasePosition = style.ArrowOffset;
            }
            if (skipKeyIcon != null) skipKeyIcon.rectTransform.sizeDelta = style.SkipKeySize;
        }

        static void ApplyText(TMP_Text text, TMP_FontAsset font, float size, Color color)
        {
            if (text == null) return;
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
        }

        void CacheArrow()
        {
            if (arrowImage == null) return;
            _arrowRect = arrowImage.rectTransform;
            _arrowBasePosition = _arrowRect.anchoredPosition;
        }

        void AnimateArrow()
        {
            if (_arrowRect == null || style == null) return;

            float amplitude = style.ArrowBobAmplitude;
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * style.ArrowBobSpeed);
            float offset = amplitude * wave;
            if (style.ArrowStepMotion)
                offset = Mathf.Round(offset / style.ArrowStepSize) * style.ArrowStepSize;

            _arrowRect.anchoredPosition = _arrowBasePosition + new Vector2(0f, -offset);
        }

        Sprite EnsureGeneratedArrow()
        {
            if (_generatedArrow != null) return _generatedArrow;

            // 픽셀 아트 아래 방향 삼각형 (12×8). 아트가 오면 DialogueStyle.arrowSprite 로 교체
            const int width = 12;
            const int height = 8;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[width * height];
            for (int row = 0; row < height; row++)
            {
                // row 0 = 위(넓음) … height-1 = 아래(뾰족)
                int half = Mathf.RoundToInt((height - 1 - row) * (width * 0.5f) / (height - 1));
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    bool filled = x >= width / 2 - half && x < width / 2 + half;
                    pixels[y * width + x] = filled
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            _generatedArrow = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f);
            _generatedArrow.hideFlags = HideFlags.HideAndDontSave;
            return _generatedArrow;
        }

        // ---------------- 입력 ----------------

        void BindListeners()
        {
            if (_listenersBound) return;
            _listenersBound = true;
            if (advanceButton != null) advanceButton.onClick.AddListener(Advance);
            if (skipButton != null) skipButton.onClick.AddListener(Skip);
            if (ruleConfirmButton != null) ruleConfirmButton.onClick.AddListener(ConfirmRuleCard);
        }

        static bool AdvancePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.spaceKey.wasPressedThisFrame ||
                   keyboard.enterKey.wasPressedThisFrame ||
                   keyboard.numpadEnterKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
        }

        static bool SkipPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.fKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F);
#endif
        }

        // ---------------- 페이드 ----------------

        void FadeTo(float target, float duration, Action onComplete, bool waitOneFrame = false)
        {
            StopFade();
            if (canvasGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            if ((duration <= 0f && !waitOneFrame) || !isActiveAndEnabled)
            {
                SetAlpha(target);
                onComplete?.Invoke();
                return;
            }

            _fadeRoutine = StartCoroutine(FadeRoutine(target, duration, onComplete, waitOneFrame));
        }

        IEnumerator FadeRoutine(float target, float duration, Action onComplete, bool waitOneFrame)
        {
            if (waitOneFrame)
            {
                // 블러가 이 프레임 끝에서 화면을 캡처한다. 그동안 대화창은 투명하게 둔다
                SetAlpha(0f);
                yield return new WaitForEndOfFrame();
                yield return null;
            }

            float from = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetAlpha(target);
            _fadeRoutine = null;
            onComplete?.Invoke();
        }

        void SetAlpha(float alpha)
        {
            canvasGroup.alpha = alpha;
            bool visible = alpha > 0.01f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible && _state != State.Finishing;
        }

        void StopTyping()
        {
            if (_typingRoutine == null) return;
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
        }

        void StopFade()
        {
            if (_fadeRoutine == null) return;
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }
    }
}
