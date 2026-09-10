using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 정전 연출 담당 (Stage 4 「정전」). 규칙(ArenaBlackoutRule)이 Begin/End 를 부르고, 이 클래스만이 화면을 건드린다.
    ///
    ///   - 캐릭터 실루엣: 관객·밴드·장식 관객·소품 SpriteRenderer 색을 검정으로 보간 (원래 색은 기억했다가 복구)
    ///   - 조명: StageLightController.SetBlackout — Global 짙은 남색, 왼쪽 조명 OFF, 오른쪽은 붉은 비상등 스윕
    ///   - 정보 숨김: 관객 호버 성향 표시 차단 (카드 판정 텍스트는 남긴다)
    ///   - BGM 일시정지, 진입·복구 효과음
    ///   - 방송 사고 오버레이: 스캔라인 + "SIGNAL LOST" 깜빡임
    /// 공연이 끝나거나 룰이 꺼지면 즉시 전부 복구한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackoutPresentation : MonoBehaviour
    {
        [Header("실루엣")]
        [SerializeField] Color silhouetteColor = new Color(0.02f, 0.02f, 0.04f, 1f);
        [SerializeField, Min(0.01f)] float fadeDuration = 0.25f;
        [SerializeField, Tooltip("이 Sorting Order 의 SpriteRenderer 도 실루엣으로 (밴드 = 45)")]
        int[] silhouetteSortingOrders = { 45 };
        [SerializeField, Tooltip("관객·장식 관객·소품 외에 실루엣으로 만들 루트 오브젝트 이름")]
        string[] extraSilhouetteObjectNames = { "Raccoon", "Band" };

        [Header("소리")]
        [SerializeField] bool pauseBgm = true;
        [SerializeField, Tooltip("정전 진입 효과음 ID (비우면 없음)")] string powerDownSoundId = "";
        [SerializeField, Tooltip("복구 효과음 ID (비우면 없음)")] string restoreSoundId = "crowd_high";

        [Header("오버레이")]
        [SerializeField] int overlaySortingOrder = 150;
        [SerializeField, Range(0f, 1f)] float scanlineAlpha = 0.14f;
        [SerializeField] Color signalColor = new Color32(0xF0, 0x4F, 0x78, 0xFF);
        [SerializeField] string signalText = "● SIGNAL LOST";
        [SerializeField, Tooltip("복구 때 조명이 깜빡이는 횟수")] int restoreFlickers = 2;

        readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        readonly List<Color> _originalColors = new List<Color>();
        Coroutine _fade;
        Coroutine _blink;
        Canvas _overlay;
        Image _scanlines;
        TMP_Text _signal;
        bool _active;
        bool _bgmPaused;
        AudiencePreferenceHoverController _hover;
        StageLightController _lights;

        public bool IsActive => _active;

        void OnEnable() => EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            if (_active) End(immediate: true);
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (_active && (e.Current == GameState.GameOver || e.Current == GameState.Ready)) End(immediate: true);
        }

        // ---------------- 시작 ----------------

        public void Begin()
        {
            if (_active) return;
            _active = true;

            CollectRenderers();
            if (_lights == null) _lights = FindFirstObjectByType<StageLightController>();
            if (_hover == null) _hover = FindFirstObjectByType<AudiencePreferenceHoverController>();

            if (_lights != null) _lights.SetBlackout(true);
            if (_hover != null) _hover.RevealAllowed = false;

            if (pauseBgm && AudioManager.HasInstance)
            {
                AudioManager.Instance.PauseBgm();
                _bgmPaused = true;
            }
            if (!string.IsNullOrEmpty(powerDownSoundId)) Sound.Play(powerDownSoundId);

            EnsureOverlay();
            _overlay.gameObject.SetActive(true);
            StopRoutines();
            _fade = StartCoroutine(FadeTint(1f, fadeDuration));
            _blink = StartCoroutine(BlinkSignal());
        }

        // ---------------- 종료 ----------------

        public void End(bool immediate = false)
        {
            if (!_active) return;
            _active = false;

            if (_lights != null) _lights.SetBlackout(false);
            if (_hover != null) _hover.RevealAllowed = true;
            if (_bgmPaused && AudioManager.HasInstance) AudioManager.Instance.ResumeBgm();
            _bgmPaused = false;

            StopRoutines();
            if (immediate || !isActiveAndEnabled)
            {
                RestoreColors();
                if (_overlay != null) _overlay.gameObject.SetActive(false);
                return;
            }

            if (!string.IsNullOrEmpty(restoreSoundId)) Sound.Play(restoreSoundId);
            _fade = StartCoroutine(RestoreSequence());
        }

        IEnumerator RestoreSequence()
        {
            // 조명이 두 번 깜빡이며 돌아온다
            for (int i = 0; i < restoreFlickers; i++)
            {
                yield return FadeTint(0f, 0.06f);
                yield return FadeTint(1f, 0.06f);
            }
            yield return FadeTint(0f, fadeDuration);
            RestoreColors();
            if (_overlay != null) _overlay.gameObject.SetActive(false);
            _fade = null;
        }

        // ---------------- 실루엣 ----------------

        void CollectRenderers()
        {
            _renderers.Clear();
            _originalColors.Clear();

            // 관객 본체는 액터가 매 프레임 색을 쓰므로 정적 블렌드로 (ApplyTint 에서 올린다)
            AudienceMemberActor.SilhouetteColor = silhouetteColor;

            foreach (DecorativeCrowd crowd in FindObjectsByType<DecorativeCrowd>(FindObjectsSortMode.None))
                foreach (SpriteRenderer sr in crowd.GetComponentsInChildren<SpriteRenderer>())
                    Add(sr);

            StageBackgroundView background = FindFirstObjectByType<StageBackgroundView>();
            if (background != null && background.ActiveSet != null)
                foreach (SpriteRenderer prop in background.ActiveSet.Props) Add(prop);

            if (silhouetteSortingOrders != null && silhouetteSortingOrders.Length > 0)
            {
                foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                {
                    if (sr == null || sr.GetComponentInParent<Canvas>() != null) continue;
                    for (int i = 0; i < silhouetteSortingOrders.Length; i++)
                        if (sr.sortingOrder == silhouetteSortingOrders[i]) { Add(sr); break; }
                }
            }

            if (extraSilhouetteObjectNames != null)
            {
                for (int i = 0; i < extraSilhouetteObjectNames.Length; i++)
                {
                    GameObject go = GameObject.Find(extraSilhouetteObjectNames[i]);
                    if (go == null) continue;
                    foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>()) Add(sr);
                }
            }
        }

        void Add(SpriteRenderer renderer)
        {
            if (renderer == null || _renderers.Contains(renderer)) return;
            _renderers.Add(renderer);
            _originalColors.Add(renderer.color);
        }

        IEnumerator FadeTint(float target, float duration)
        {
            float from = _currentTint;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                ApplyTint(Mathf.Lerp(from, target, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            ApplyTint(target);
        }

        float _currentTint;

        void ApplyTint(float t)
        {
            _currentTint = t;
            AudienceMemberActor.SilhouetteBlend = t;
            for (int i = 0; i < _renderers.Count; i++)
            {
                SpriteRenderer sr = _renderers[i];
                if (sr == null) continue;
                Color original = _originalColors[i];
                Color target = new Color(silhouetteColor.r, silhouetteColor.g, silhouetteColor.b, original.a);
                sr.color = Color.Lerp(original, target, t);
            }
            if (_scanlines != null) _scanlines.color = new Color(1f, 1f, 1f, scanlineAlpha * t);
        }

        void RestoreColors()
        {
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].color = _originalColors[i];
            _renderers.Clear();
            _originalColors.Clear();
            _currentTint = 0f;
            AudienceMemberActor.SilhouetteBlend = 0f;
        }

        void StopRoutines()
        {
            if (_fade != null) StopCoroutine(_fade);
            if (_blink != null) StopCoroutine(_blink);
            _fade = null;
            _blink = null;
        }

        // ---------------- 오버레이 ----------------

        IEnumerator BlinkSignal()
        {
            while (true)
            {
                if (_signal != null) _signal.enabled = !_signal.enabled;
                yield return new WaitForSeconds(0.5f);
            }
        }

        void EnsureOverlay()
        {
            if (_overlay != null) return;

            var canvasObject = new GameObject("BlackoutOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _overlay = canvasObject.GetComponent<Canvas>();
            _overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlay.sortingOrder = overlaySortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var lineObject = new GameObject("Scanlines", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var lineRect = lineObject.GetComponent<RectTransform>();
            lineRect.SetParent(canvasObject.transform, false);
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = Vector2.one;
            lineRect.offsetMin = Vector2.zero;
            lineRect.offsetMax = Vector2.zero;
            _scanlines = lineObject.GetComponent<Image>();
            _scanlines.sprite = CreateScanlineSprite();
            _scanlines.type = Image.Type.Tiled;
            _scanlines.pixelsPerUnitMultiplier = 0.5f;
            _scanlines.color = new Color(1f, 1f, 1f, 0f);
            _scanlines.raycastTarget = false;

            TMP_FontAsset font = null;
            DialogueCatalog catalog = DialogueCatalog.LoadDefault();
            if (catalog != null && catalog.Style != null) font = catalog.Style.Font;

            var textObject = new GameObject("Signal", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(canvasObject.transform, false);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(28f, -24f);
            textRect.sizeDelta = new Vector2(420f, 44f);
            _signal = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null) _signal.font = font;
            _signal.fontSize = 28f;
            _signal.color = signalColor;
            _signal.text = signalText;
            _signal.alignment = TextAlignmentOptions.MidlineLeft;
            _signal.raycastTarget = false;
            _signal.outlineWidth = 0.2f;
            _signal.outlineColor = new Color32(0x16, 0x12, 0x1C, 0xFF);

            _overlay.gameObject.SetActive(false);
        }

        static Sprite CreateScanlineSprite()
        {
            var tex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(new[] { new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 0) });
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 4), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
