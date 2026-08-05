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
        [SerializeField, Min(10)] int titleFontSize = 34;
        [SerializeField, Min(10)] int titleMinFontSize = 26;
        [SerializeField, Min(10)] int bodyFontSize = 25;
        [SerializeField, Min(10)] int bodyMinFontSize = 20;
        [SerializeField, Min(10)] int continueFontSize = 19;

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
        }

        /// <summary>
        /// TutorialPanel 자체의 위치와 크기는 건드리지 않고, 그 안의 제목·본문·진행 안내만
        /// 현재 좁은 패널에서도 잘리지 않도록 재배치한다.
        /// </summary>
        void ConfigureMessageLayout()
        {
            if (mainText != null)
            {
                var rect = mainText.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -14f);
                rect.sizeDelta = new Vector2(-32f, 88f);

                mainText.fontSize = titleFontSize;
                mainText.resizeTextForBestFit = true;
                mainText.resizeTextMinSize = titleMinFontSize;
                mainText.resizeTextMaxSize = titleFontSize;
                mainText.alignment = TextAnchor.UpperCenter;
                mainText.horizontalOverflow = HorizontalWrapMode.Wrap;
                mainText.verticalOverflow = VerticalWrapMode.Overflow;
                mainText.lineSpacing = 0.9f;
            }

            if (subText != null)
            {
                var rect = subText.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -108f);
                rect.sizeDelta = new Vector2(-40f, 190f);

                subText.fontSize = bodyFontSize;
                subText.resizeTextForBestFit = true;
                subText.resizeTextMinSize = bodyMinFontSize;
                subText.resizeTextMaxSize = bodyFontSize;
                subText.alignment = TextAnchor.UpperCenter;
                subText.horizontalOverflow = HorizontalWrapMode.Wrap;
                subText.verticalOverflow = VerticalWrapMode.Overflow;
                subText.lineSpacing = 0.9f;
            }

            if (continueHint != null)
            {
                var rect = continueHint.transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(1f, 0f);
                    rect.anchoredPosition = new Vector2(-18f, 14f);
                    rect.sizeDelta = new Vector2(280f, 32f);
                }

                var hintText = continueHint.GetComponent<Text>();
                if (hintText != null)
                {
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
            if (mainText != null) mainText.text = main ?? "";
            if (subText != null)
            {
                subText.text = sub ?? "";
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
            subText.text = sub ?? "";
            subText.gameObject.SetActive(!string.IsNullOrEmpty(sub));
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
