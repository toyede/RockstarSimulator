using System.Collections;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 팝업 뒤에 깔리는 블러 배경. 일시정지·클리어·게임오버 창용.
    ///
    /// <b>매 프레임 블러를 돌리지 않는다.</b> 이런 창은 뒷화면이 멈춰 있는 게 정상이라
    /// 열리는 순간 화면을 <b>한 번</b> 잡아 흐리게 만들고 그 결과를 계속 보여준다.
    /// 그래서 이후 프레임 비용이 0이고, URP Renderer Feature 도 _CameraOpaqueTexture 도 필요 없다.
    ///
    /// 동작 순서:
    /// <list type="number">
    /// <item>팝업이 열리며 이 오브젝트가 켜진다 (UIPopup.Open 이 루트를 SetActive)</item>
    /// <item>그 프레임 끝까지 기다렸다가 화면을 통째로 캡처한다 (HUD 포함)</item>
    /// <item>축소 → Kawase 블러 여러 패스 → RawImage 에 꽂고 페이드 인</item>
    /// </list>
    ///
    /// <b>캡처 순간에는 팝업 자신을 잠깐 투명하게 만든다.</b>
    /// 안 그러면 막 뜨기 시작한 팝업이 배경에 같이 찍혀 겹쳐 보인다.
    ///
    /// 축소 배율(downscale)을 키우면 픽셀이 뭉개지면서 도트 감성과도 맞는다.
    /// 어둡게 깔고 싶으면 RawImage 의 color 를 회색으로 낮추면 된다 (셰이더 수정 불필요).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage))]
    public sealed class UIBlurBackdrop : MonoBehaviour
    {
        /// <summary>캡처 결과의 상하 반전 처리. 그래픽 API 마다 화면 원점이 달라서 필요하다.</summary>
        public enum FlipMode
        {
            [InspectorName("자동 (권장)")] Auto,
            [InspectorName("뒤집기")] Flip,
            [InspectorName("그대로")] None,
        }

        [Header("연결")]
        [SerializeField, Tooltip("비워두면 이 오브젝트에서 찾는다")]
        RawImage backdropImage;

        [SerializeField, Tooltip(
            "블러 셰이더. 비워두면 Shader.Find 로 찾지만, " +
            "직접 연결해 두어야 빌드에서 스트리핑되지 않는다")]
        Shader blurShader;

        [SerializeField, Tooltip(
            "캡처 순간 잠깐 투명하게 만들 팝업의 CanvasGroup. " +
            "비워두면 부모에서 찾는다. 안 그러면 팝업이 배경에 같이 찍힌다")]
        CanvasGroup popupGroup;

        [Header("블러")]
        [SerializeField, Range(1, 16), Tooltip(
            "캡처 해상도를 몇 분의 1로 줄일지. 클수록 더 흐리고 더 싸다. " +
            "픽셀아트라면 크게 잡아도 잘 어울린다")]
        int downscale = 4;

        [SerializeField, Range(0, 8), Tooltip(
            "Kawase 블러 패스 수. 패스마다 오프셋이 커져 흐림이 빠르게 번진다")]
        int blurPasses = 4;

        [SerializeField, Min(0f), Tooltip("첫 패스의 샘플 오프셋(픽셀). 패스마다 커진다")]
        float baseOffset = 1f;

        [SerializeField, Tooltip(
            "축소 텍스처를 Point 로 샘플링한다. 켜면 픽셀이 각지게 남아 도트 느낌이 살고, " +
            "끄면 매끈하게 번진다")]
        bool pixelatedFilter = true;

        [Header("표시")]
        [SerializeField, Min(0f), Tooltip("배경이 나타나는 시간(초). 0이면 즉시")]
        float fadeInDuration = 0.12f;

        [SerializeField, Tooltip(
            "캡처 결과의 상하 반전. 자동은 그래픽 API 의 화면 원점을 보고 정한다. " +
            "화면이 뒤집혀 보이면 여기서 강제로 바꾼다")]
        FlipMode flip = FlipMode.Auto;

        // ---------------- 상태 ----------------

        Material _material;
        RenderTexture _capture;
        RenderTexture _blurA;
        RenderTexture _blurB;
        Coroutine _routine;
        Color _baseColor = Color.white;

        void Awake()
        {
            if (backdropImage == null) backdropImage = GetComponent<RawImage>();
            if (popupGroup == null) popupGroup = GetComponentInParent<CanvasGroup>();

            _baseColor = backdropImage.color;
            backdropImage.raycastTarget = false;
            backdropImage.enabled = false;
        }

        void OnEnable()
        {
            // 시작 시 UIPopup.CloseImmediate 로 잠깐 켜졌다 꺼지는 경우가 있어
            // 실제로 열린 팝업이 아니면 아무것도 하지 않는다
            if (!Application.isPlaying) return;

            UIPopup popup = GetComponentInParent<UIPopup>();
            if (popup != null && !popup.IsOpen) return;

            _routine = StartCoroutine(CaptureAndBlur());
        }

        void OnDisable()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            Hide();
            ReleaseTextures();
        }

        void OnDestroy()
        {
            ReleaseTextures();
            if (_material != null) Destroy(_material);
        }

        /// <summary>
        /// 캡처는 <b>이 프레임이 그려진 뒤</b>에 해야 하므로 WaitForEndOfFrame 을 한 번 기다린다.
        ///
        /// 팝업 알파를 미리 0으로 눌러 두는 이유: 이 코루틴은 UIPopup.Open 의
        /// SetActive 시점(=페이드가 시작되기 전)에 돌기 시작하므로, 여기서 0을 넣으면
        /// animDuration 이 0인 팝업이 배경에 통째로 찍히는 것을 막을 수 있다.
        /// 페이드가 있는 팝업은 같은 프레임에 UIPopup 이 알파를 한 스텝(기본 약 0.1)
        /// 올려 버리지만, 그 정도는 축소·블러를 거치면 보이지 않는다.
        /// </summary>
        IEnumerator CaptureAndBlur()
        {
            Hide();

            float restoreAlpha = -1f;
            if (popupGroup != null)
            {
                restoreAlpha = popupGroup.alpha;
                popupGroup.alpha = 0f;
            }

            yield return new WaitForEndOfFrame();

            bool captured = TryCapture();

            // UIPopup 의 페이드 코루틴이 다음 프레임에 다시 써 주지만,
            // animDuration 이 0이면 아무도 안 써 주므로 여기서 되돌려야 한다
            if (popupGroup != null && restoreAlpha >= 0f) popupGroup.alpha = restoreAlpha;

            if (!captured) yield break;

            Blur();
            yield return FadeIn();

            _routine = null;
        }

        bool TryCapture()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);

            EnsureCaptureTexture(width, height);
            if (_capture == null) return false;

            // 백버퍼를 그대로 가져오므로 HUD 를 포함한 "지금 보이는 화면" 이 잡힌다.
            // URP·2D 렌더러 설정과 무관하게 동작한다.
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_capture);
            return true;
        }

        void Blur()
        {
            if (!EnsureMaterial()) return;

            int width = Mathf.Max(1, _capture.width / Mathf.Max(1, downscale));
            int height = Mathf.Max(1, _capture.height / Mathf.Max(1, downscale));
            EnsureBlurTextures(width, height);

            // 축소하면서 이미 한 번 섞인다 (블러 패스를 크게 아낀다)
            Graphics.Blit(_capture, _blurA);

            RenderTexture source = _blurA;
            RenderTexture destination = _blurB;
            for (int i = 0; i < blurPasses; i++)
            {
                _material.SetFloat("_Offset", baseOffset + i); // 패스마다 넓게 번진다
                Graphics.Blit(source, destination, _material);

                RenderTexture swap = source;
                source = destination;
                destination = swap;
            }

            backdropImage.texture = source;
            backdropImage.uvRect = ShouldFlip()
                ? new Rect(0f, 1f, 1f, -1f)
                : new Rect(0f, 0f, 1f, 1f);
            backdropImage.enabled = true;
        }

        /// <summary>
        /// 백버퍼 캡처의 세로 방향은 그래픽 API 마다 다르다.
        /// D3D 처럼 화면 원점이 위인 API 에서는 뒤집어야 똑바로 보인다.
        /// </summary>
        bool ShouldFlip()
        {
            switch (flip)
            {
                case FlipMode.Flip: return true;
                case FlipMode.None: return false;
                default: return SystemInfo.graphicsUVStartsAtTop;
            }
        }

        IEnumerator FadeIn()
        {
            if (fadeInDuration <= 0f)
            {
                SetAlpha(1f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                // 일시정지(timeScale 0)에서도 나타나야 한다
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Clamp01(elapsed / fadeInDuration));
                yield return null;
            }

            SetAlpha(1f);
        }

        void SetAlpha(float alpha)
        {
            Color color = _baseColor;
            color.a = _baseColor.a * alpha;
            backdropImage.color = color;
        }

        void Hide()
        {
            if (backdropImage == null) return;

            backdropImage.enabled = false;
            SetAlpha(0f);
        }

        // ---------------- 리소스 ----------------

        bool EnsureMaterial()
        {
            if (_material != null) return true;

            if (blurShader == null) blurShader = Shader.Find("ContextStage/UI/Kawase Blur");
            if (blurShader == null)
            {
                Debug.LogError(
                    "[UIBlur] 블러 셰이더를 찾지 못했습니다. " +
                    "인스펙터의 blurShader 에 Assets/Shaders/UIKawaseBlur.shader 를 연결하세요.",
                    this);
                return false;
            }

            _material = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        void EnsureCaptureTexture(int width, int height)
        {
            if (_capture != null && _capture.width == width && _capture.height == height) return;

            ReleaseTexture(ref _capture);
            _capture = new RenderTexture(width, height, 0)
            {
                name = "UIBlur Capture",
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _capture.Create();
        }

        void EnsureBlurTextures(int width, int height)
        {
            if (_blurA != null && _blurA.width == width && _blurA.height == height) return;

            ReleaseTexture(ref _blurA);
            ReleaseTexture(ref _blurB);
            _blurA = CreateBlurTexture(width, height, "UIBlur A");
            _blurB = CreateBlurTexture(width, height, "UIBlur B");
        }

        RenderTexture CreateBlurTexture(int width, int height, string textureName)
        {
            var texture = new RenderTexture(width, height, 0)
            {
                name = textureName,
                // Point 로 두면 축소된 픽셀 격자가 그대로 남아 도트 느낌이 산다.
                // 단 블러 패스의 이중선형 이득이 사라지므로 패스를 조금 더 쓰게 된다.
                filterMode = pixelatedFilter ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.Create();
            return texture;
        }

        void ReleaseTextures()
        {
            if (backdropImage != null) backdropImage.texture = null;
            ReleaseTexture(ref _capture);
            ReleaseTexture(ref _blurA);
            ReleaseTexture(ref _blurB);
        }

        static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null) return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            downscale = Mathf.Clamp(downscale, 1, 16);
            blurPasses = Mathf.Clamp(blurPasses, 0, 8);
        }
#endif
    }
}
