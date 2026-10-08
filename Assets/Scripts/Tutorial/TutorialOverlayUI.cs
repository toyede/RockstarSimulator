using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 튜토리얼의 화면 표현만 담당한다. 진행 로직(TutorialFlow)을 전혀 모른다.
    ///
    /// - 딤: 월드 전용. Screen Space UI(카드·HUD·이 패널)는 어두워지지 않는다.
    ///   구현은 카메라를 덮는 검은 SpriteRenderer(sortingOrder 400) 하나다.
    /// - 스포트라이트: 대상 관객의 SpriteRenderer 들을 딤 위(+500)로 끌어올리고,
    ///   발밑에 런타임 생성한 원형 글로우를 깐다. 해제 시 원래 순서로 복구한다.
    /// - 메시지 패널: 상단 중앙. "클릭해서 계속" 단계에서는 패널 전체가 버튼이 된다.
    /// - 스킵: 항상 우상단. 눌리면 TutorialFlow 가 구독한 콜백이 호출된다.
    ///
    /// 모든 참조는 null-safe — 셋업 메뉴가 연결하지만, 빠져도 컴파일·실행된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialOverlayUI : MonoBehaviour
    {
        [Header("패널")]
        [SerializeField, Tooltip("메시지 패널 루트")] GameObject panelRoot;
        [SerializeField] Text mainText;
        [SerializeField] Text subText;
        [SerializeField, Tooltip("'클릭해서 계속' 안내 라벨")] GameObject continueHint;
        [SerializeField, Tooltip("패널 전체를 덮는 진행 버튼")] Button continueButton;
        [SerializeField] Button skipButton;

        [Header("타이포그래피")]
        [SerializeField, Min(10)] int titleFontSize = 28;
        [SerializeField, Min(10)] int titleMinFontSize = 24;
        [SerializeField, Min(10)] int bodyFontSize = 22;
        [SerializeField, Min(10)] int bodyMinFontSize = 20;
        [SerializeField, Min(10)] int continueFontSize = 20;

        [Header("딤")]
        [SerializeField, Range(0f, 1f), Tooltip("월드 딤 어둡기")] float dimAlpha = 0.6f;
        [SerializeField, Tooltip("딤 페이드 속도")] float dimFadeSpeed = 6f;

        [Header("스포트라이트")]
        [SerializeField, Tooltip("CHILL 관객 글로우 색")]
        Color chillGlowColor = new Color32(0x31, 0xDF, 0xEA, 0x8C);
        [SerializeField, Tooltip("SINGALONG 관객 글로우 색")]
        Color singalongGlowColor = new Color32(0x64, 0x31, 0xEA, 0x8C);
        [SerializeField, Tooltip("MOSH 관객 글로우 색")]
        Color moshGlowColor = new Color32(0xF0, 0x1F, 0x1F, 0x8C);
        [SerializeField, Tooltip("글로우 펄스 속도")] float glowPulseSpeed = 3f;

        public event System.Action ContinueClicked;
        public event System.Action SkipClicked;

        SpriteRenderer _dim;          // 월드 딤 (런타임 생성)
        SpriteRenderer _glow;         // 스포트라이트 발밑 글로우 (런타임 생성)
        Transform _spotTarget;
        float _dimTarget;             // 0 = 밝음, dimAlpha = 어두움
        float _dimCurrent;
        Button _fullScreenContinueButton;
        bool _continueArmed;
        Text _cueSheetLabel;

        // 스포트라이트로 끌어올린 렌더러들의 원래 sortingOrder
        readonly List<(SpriteRenderer renderer, int order)> _boosted =
            new List<(SpriteRenderer, int)>();

        const int DimOrder = 400;
        const int BoostOffset = 500;

        void Awake()
        {
            ConfigureMessageLayout();
            EnsureDim();
            EnsureGlow();
            EnsureFullScreenContinueButton();
            if (continueButton != null) continueButton.onClick.AddListener(RequestContinue);
            if (_fullScreenContinueButton != null)
                _fullScreenContinueButton.onClick.AddListener(RequestContinue);
            if (skipButton != null) skipButton.onClick.AddListener(() => SkipClicked?.Invoke());
            HideAll();
            // 씬에 저장된 버튼이 켜져 있어도 튜토리얼이 시작되기 전에는 보이면 안 된다 (다른 스테이지에서 버튼만 남던 버그)
            SetSkipVisible(false);
        }

        /// <summary>튜토리얼 전용 종이 배경. 미션 슬레이트와는 독립적이다.</summary>
        public Image PanelImage => panelRoot != null ? panelRoot.GetComponent<Image>() : null;
        public int TitleFontSize => titleFontSize;
        public int BodyFontSize => bodyFontSize;
        public Font GuideFont => subText != null ? subText.font : null;

        struct RectState
        {
            public RectTransform Rect;
            public Vector2 Min, Max, Pivot, Position, Size;
            public RectState(RectTransform rect)
            {
                Rect = rect;
                Min = rect.anchorMin; Max = rect.anchorMax; Pivot = rect.pivot;
                Position = rect.anchoredPosition; Size = rect.sizeDelta;
            }
            public void Restore()
            {
                if (Rect == null) return;
                Rect.anchorMin = Min; Rect.anchorMax = Max; Rect.pivot = Pivot;
                Rect.anchoredPosition = Position; Rect.sizeDelta = Size;
            }
        }
        RectState[] _specialLayout;

        /// <summary>교육 중에만 왼쪽에 짧은 안내를 표시한다. 저장된 씬 배치는 복원한다.</summary>
        public void SetSpecialLessonLayout(bool compact)
        {
            if (!compact)
            {
                if (_specialLayout != null)
                    foreach (var state in _specialLayout) state.Restore();
                _specialLayout = null;
                return;
            }
            if (_specialLayout != null || panelRoot == null || mainText == null || subText == null) return;
            var panel = (RectTransform)panelRoot.transform;
            _specialLayout = new[] { new RectState(panel), new RectState(mainText.rectTransform), new RectState(subText.rectTransform) };
            panel.anchorMin = new Vector2(0.015f, 0.33f);
            panel.anchorMax = new Vector2(0.35f, 0.73f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = Vector2.zero;
            ConfigureMessageLayout();
        }

        /// <summary>
        /// TutorialPanel 자체의 위치와 크기는 건드리지 않고, 그 안의 제목·본문·진행 안내만
        /// 현재 좁은 패널에서도 잘리지 않도록 재배치한다.
        /// </summary>
        void ConfigureMessageLayout()
        {
            Image paper = PanelImage;
            Sprite sprite = Resources.Load<Sprite>("UI/CueSheets/TutorialPaper");
            if (paper != null && sprite != null)
            {
                paper.sprite = sprite; paper.type = Image.Type.Simple; paper.color = Color.white;
                foreach (Shadow effect in paper.GetComponents<Shadow>()) effect.enabled = false;
                if (_cueSheetLabel == null)
                {
                    var label = new GameObject("CueSheetLabel", typeof(RectTransform), typeof(Text));
                    label.transform.SetParent(panelRoot.transform, false);
                    _cueSheetLabel = label.GetComponent<Text>();
                    _cueSheetLabel.font = mainText != null ? mainText.font : GuideFont;
                    _cueSheetLabel.fontSize = 22; _cueSheetLabel.alignment = TextAnchor.MiddleCenter;
                    _cueSheetLabel.color = new Color32(0xFF, 0xD6, 0x66, 0xFF); _cueSheetLabel.raycastTarget = false;
                    _cueSheetLabel.text = "공연 가이드 · CUE SHEET";
                    var r = _cueSheetLabel.rectTransform;
                    r.anchorMin = new Vector2(.14f, .75f); r.anchorMax = new Vector2(.94f, .86f);
                    r.offsetMin = r.offsetMax = Vector2.zero;
                }
            }
            if (mainText != null)
            {
                var rect = mainText.rectTransform;
                rect.anchorMin = new Vector2(.10f, .49f);
                rect.anchorMax = new Vector2(.90f, .70f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                mainText.color = new Color32(0x71, 0x36, 0x97, 0xFF);
                mainText.supportRichText = true;
                foreach (Shadow effect in mainText.GetComponents<Shadow>()) effect.enabled = false;

                mainText.fontSize = titleFontSize;
                mainText.resizeTextForBestFit = true;
                mainText.resizeTextMinSize = titleMinFontSize;
                mainText.resizeTextMaxSize = titleFontSize;
                mainText.alignment = TextAnchor.MiddleCenter;
                mainText.horizontalOverflow = HorizontalWrapMode.Wrap;
                mainText.verticalOverflow = VerticalWrapMode.Overflow;
                mainText.lineSpacing = 1.15f;
            }

            if (subText != null)
            {
                var rect = subText.rectTransform;
                rect.anchorMin = new Vector2(.10f, .16f);
                rect.anchorMax = new Vector2(.90f, .43f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                subText.color = MissionCueSheetView.Ink;
                subText.supportRichText = true;
                foreach (Shadow effect in subText.GetComponents<Shadow>()) effect.enabled = false;

                subText.fontSize = bodyFontSize;
                subText.resizeTextForBestFit = true;
                subText.resizeTextMinSize = bodyMinFontSize;
                subText.resizeTextMaxSize = bodyFontSize;
                subText.alignment = TextAnchor.UpperCenter;
                subText.horizontalOverflow = HorizontalWrapMode.Wrap;
                subText.verticalOverflow = VerticalWrapMode.Overflow;
                subText.lineSpacing = 1.15f;
            }

            if (continueHint != null)
            {
                var rect = continueHint.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(1f, .08f);
                    rect.anchorMax = new Vector2(1f, .08f);
                    rect.pivot = new Vector2(1f, 0f);
                    rect.anchoredPosition = new Vector2(-30f, 0f);
                    rect.sizeDelta = new Vector2(280f, 32f);
                }

                var hintText = continueHint.GetComponent<Text>();
                if (hintText != null)
                {
                    hintText.color = new Color32(0x24, 0x61, 0xAB, 0xFF);
                    foreach (Shadow effect in hintText.GetComponents<Shadow>()) effect.enabled = false;
                    hintText.fontSize = continueFontSize;
                    hintText.resizeTextForBestFit = true;
                    hintText.resizeTextMinSize = 16;
                    hintText.resizeTextMaxSize = continueFontSize;
                }
            }
        }

        void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(RequestContinue);
            if (_fullScreenContinueButton != null)
                _fullScreenContinueButton.onClick.RemoveListener(RequestContinue);
            if (skipButton != null) skipButton.onClick.RemoveAllListeners();
        }

        void LateUpdate()
        {
            // 딤 페이드
            _dimCurrent = Mathf.MoveTowards(_dimCurrent, _dimTarget, dimFadeSpeed * Time.unscaledDeltaTime);
            if (_dim != null)
            {
                _dim.color = new Color(0f, 0f, 0f, _dimCurrent);
                _dim.enabled = _dimCurrent > 0.001f;
                FitDimToCamera();
            }

            // 글로우가 대상을 따라다닌다 (관객은 반동·점프로 계속 움직인다)
            if (_glow != null)
            {
                bool show = _spotTarget != null && _dimCurrent > 0.01f;
                _glow.enabled = show;
                if (show)
                {
                    Vector3 p = _spotTarget.position;
                    _glow.transform.position = new Vector3(p.x, p.y - 0.35f, p.z);
                    float pulse = 1f + Mathf.Sin(Time.unscaledTime * glowPulseSpeed) * 0.12f;
                    _glow.transform.localScale = Vector3.one * 2.2f * pulse;
                }
            }
        }

        // ---------------- TutorialFlow 가 호출하는 API ----------------

        /// <summary>메시지 표시. clickToContinue 면 패널 클릭으로 진행한다.</summary>
        public void ShowMessage(string main, string sub, bool clickToContinue)
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            if (mainText != null) mainText.text = EmphasizeGuide(main);
            if (subText != null)
            {
                subText.text = EmphasizeGuide(sub);
                subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            }
            if (continueHint != null) continueHint.SetActive(clickToContinue);
            if (continueButton != null) continueButton.interactable = clickToContinue;
            _continueArmed = clickToContinue;
            if (_fullScreenContinueButton != null)
            {
                _fullScreenContinueButton.interactable = clickToContinue;
                _fullScreenContinueButton.gameObject.SetActive(clickToContinue);
            }
        }

        /// <summary>서브 텍스트만 잠깐 바꾼다 (틀린 카드 힌트 등).</summary>
        public void FlashSub(string sub)
        {
            if (subText == null) return;
            subText.text = EmphasizeGuide(sub);
            subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        }

        // 종이 위에서도 대비가 유지되는 성향/행동 강조색. 이미 작성된 리치 텍스트는 보존한다.
        static string EmphasizeGuide(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.IndexOf('<') >= 0) return text;
            string[] words = { "CHILL", "Chill", "SINGALONG", "Singalong", "MOSH", "Mosh", "FEVER", "Fever",
                "피버", "특별 관객", "총 반응", "2초", "10초", "1,000점", "카드를", "콤보" };
            string[] colors = { "087B86", "087B86", "6632A6", "6632A6", "B72835", "B72835", "986000", "986000",
                "986000", "A52B6C", "176EB2", "176EB2", "176EB2", "176EB2", "176EB2", "986000" };
            for (int i = 0; i < words.Length; i++)
                text = text.Replace(words[i], $"<color=#{colors[i]}>{words[i]}</color>");
            return text;
        }

        public void SetDim(bool on) => _dimTarget = on ? dimAlpha : 0f;

        /// <summary>대상 관객을 딤 위로 끌어올린다. null 이면 해제.</summary>
        public void Spotlight(AudienceMemberActor actor)
        {
            RestoreBoosted();
            _spotTarget = null;
            if (actor == null) return;

            _spotTarget = actor.transform;
            if (_glow != null) _glow.color = GlowColorFor(actor.Snapshot.Preference);
            foreach (var sr in actor.GetComponentsInChildren<SpriteRenderer>(true))
            {
                _boosted.Add((sr, sr.sortingOrder));
                sr.sortingOrder += BoostOffset;
            }
        }

        Color GlowColorFor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillGlowColor;
                case CrowdPreference.Singalong: return singalongGlowColor;
                case CrowdPreference.Mosh: return moshGlowColor;
                default: return chillGlowColor;
            }
        }

        public void HideAll()
        {
            _continueArmed = false;
            if (_fullScreenContinueButton != null)
                _fullScreenContinueButton.gameObject.SetActive(false);
            if (panelRoot != null) panelRoot.SetActive(false);
            SetDim(false);
            Spotlight(null);
        }

        public void SetSkipVisible(bool visible)
        {
            if (skipButton != null) skipButton.gameObject.SetActive(visible);
        }

        // ---------------- 내부 ----------------

        void RequestContinue()
        {
            if (!_continueArmed) return;

            // 한 클릭으로 두 단계가 연속 진행되는 것을 막는다.
            _continueArmed = false;
            if (continueButton != null) continueButton.interactable = false;
            if (_fullScreenContinueButton != null)
                _fullScreenContinueButton.gameObject.SetActive(false);
            ContinueClicked?.Invoke();
        }

        void EnsureFullScreenContinueButton()
        {
            if (_fullScreenContinueButton != null) return;

            var go = new GameObject(
                "TutorialFullScreenContinue",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            _fullScreenContinueButton = go.GetComponent<Button>();
            _fullScreenContinueButton.transition = Selectable.Transition.None;
            go.SetActive(false);
        }

        void RestoreBoosted()
        {
            for (int i = 0; i < _boosted.Count; i++)
                if (_boosted[i].renderer != null)
                    _boosted[i].renderer.sortingOrder = _boosted[i].order;
            _boosted.Clear();
        }

        void EnsureDim()
        {
            if (_dim != null) return;
            var go = new GameObject("TutorialDim");
            go.transform.SetParent(transform, false);
            _dim = go.AddComponent<SpriteRenderer>();
            _dim.sprite = CreateSolidSprite();
            _dim.sortingOrder = DimOrder;
            _dim.enabled = false;
        }

        void EnsureGlow()
        {
            if (_glow != null) return;
            var go = new GameObject("TutorialGlow");
            go.transform.SetParent(transform, false);
            _glow = go.AddComponent<SpriteRenderer>();
            _glow.sprite = CreateRadialSprite();
            _glow.color = chillGlowColor; // Spotlight() 호출 시 대상 성향 색으로 바뀐다
            _glow.sortingOrder = DimOrder + 1; // 딤 바로 위, 부스트된 관객 아래
            _glow.enabled = false;
        }

        void FitDimToCamera()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float h = cam.orthographicSize * 2f * 1.2f; // 흔들림 여유
            float w = h * cam.aspect * 1.2f;
            Vector3 c = cam.transform.position;
            _dim.transform.position = new Vector3(c.x, c.y, 0f);
            _dim.transform.localScale = new Vector3(w, h, 1f);
        }

        static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var pixels = new Color32[4];
            for (int i = 0; i < 4; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        }

        static Sprite CreateRadialSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                byte a = (byte)(255f * Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            titleFontSize = Mathf.Max(10, titleFontSize);
            titleMinFontSize = Mathf.Clamp(titleMinFontSize, 10, titleFontSize);
            bodyFontSize = Mathf.Max(10, bodyFontSize);
            bodyMinFontSize = Mathf.Clamp(bodyMinFontSize, 10, bodyFontSize);
            continueFontSize = Mathf.Max(10, continueFontSize);
        }
#endif
    }
}
