using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 화면을 한 번 캡처한 뒤, 지정한 UI 영역만 잘라서 블러 배경으로 표시한다.
    /// 전체 화면 캡처 이미지를 작은 팝업에 축소해 넣지 않으므로 화면 비율이 보존된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIRegionBlurBackdrop : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] RawImage blurImage;
        [SerializeField] RectTransform captureRegion;
        [SerializeField] CanvasGroup ownerGroup;
        [SerializeField] Shader blurShader;

        [Header("블러")]
        [SerializeField, Range(1, 16)] int downscale = 4;
        [SerializeField, Range(0, 8)] int blurPasses = 6;
        [SerializeField, Min(0f)] float baseOffset = 1.5f;
        [SerializeField] bool pixelatedFilter;

        Coroutine _captureRoutine;
        Material _blurMaterial;
        RenderTexture _capture;
        RenderTexture _blurA;
        RenderTexture _blurB;
        float _ownerBaseAlpha = 1f;
        readonly Vector3[] _worldCorners = new Vector3[4];

        public void Configure(
            RawImage target,
            RectTransform region,
            CanvasGroup group,
            Shader shader)
        {
            blurImage = target;
            captureRegion = region;
            ownerGroup = group;
            blurShader = shader;
        }

        void Awake()
        {
            if (blurImage == null) blurImage = GetComponent<RawImage>();
            if (captureRegion == null)
                captureRegion = transform.parent as RectTransform;
            if (ownerGroup == null)
                ownerGroup = GetComponentInParent<CanvasGroup>();

            if (ownerGroup != null)
                _ownerBaseAlpha = ownerGroup.alpha;

            if (blurImage != null)
            {
                blurImage.color = Color.white;
                blurImage.raycastTarget = false;
                blurImage.enabled = false;
            }
        }

        void OnEnable()
        {
            if (_captureRoutine != null)
                StopCoroutine(_captureRoutine);

            _captureRoutine = StartCoroutine(CaptureAndBlur());
        }

        void OnDisable()
        {
            if (_captureRoutine != null)
            {
                StopCoroutine(_captureRoutine);
                _captureRoutine = null;
            }

            if (ownerGroup != null)
                ownerGroup.alpha = _ownerBaseAlpha;

            HideAndReleaseTextures();
        }

        void OnDestroy()
        {
            ReleaseTexture(ref _capture);
            ReleaseTexture(ref _blurA);
            ReleaseTexture(ref _blurB);

            if (_blurMaterial != null)
                Destroy(_blurMaterial);
        }

        IEnumerator CaptureAndBlur()
        {
            if (blurImage == null || captureRegion == null)
                yield break;

            if (ownerGroup != null)
                ownerGroup.alpha = 0f;

            if (blurImage != null)
                blurImage.enabled = false;

            // 팝업 자체가 캡처 결과에 섞이지 않도록 팝업이 그려지지 않은 프레임을 기다린다.
            yield return new WaitForEndOfFrame();

            bool captured = false;
            try
            {
                captured = TryCaptureAndBlur();
            }
            finally
            {
                if (ownerGroup != null)
                    ownerGroup.alpha = _ownerBaseAlpha;
            }

            if (captured && blurImage != null)
                blurImage.enabled = true;

            _captureRoutine = null;
        }

        bool TryCaptureAndBlur()
        {
            Rect region = GetScreenRect();
            if (region.width <= 1f || region.height <= 1f)
                return false;

            int screenWidth = Mathf.Max(1, Screen.width);
            int screenHeight = Mathf.Max(1, Screen.height);
            EnsureCaptureTexture(screenWidth, screenHeight);
            if (_capture == null)
                return false;

            ScreenCapture.CaptureScreenshotIntoRenderTexture(_capture);

            int width = Mathf.Max(1, Mathf.RoundToInt(region.width / Mathf.Max(1, downscale)));
            int height = Mathf.Max(1, Mathf.RoundToInt(region.height / Mathf.Max(1, downscale)));
            EnsureBlurTextures(width, height);
            if (_blurA == null || _blurB == null || !EnsureMaterial())
                return false;

            BlitRegion(_capture, _blurA, region, screenWidth, screenHeight);

            RenderTexture source = _blurA;
            RenderTexture destination = _blurB;
            for (int i = 0; i < blurPasses; i++)
            {
                _blurMaterial.SetFloat("_Offset", baseOffset + i);
                Graphics.Blit(source, destination, _blurMaterial);

                RenderTexture swap = source;
                source = destination;
                destination = swap;
            }

            blurImage.texture = source;
            blurImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            return true;
        }

        Rect GetScreenRect()
        {
            captureRegion.GetWorldCorners(_worldCorners);
            Canvas canvas = captureRegion.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[2]);
            return Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y));
        }

        void BlitRegion(
            Texture source,
            RenderTexture destination,
            Rect region,
            int screenWidth,
            int screenHeight)
        {
            float u = Mathf.Clamp01(region.xMin / screenWidth);
            float v = Mathf.Clamp01(region.yMin / screenHeight);
            float uSize = Mathf.Clamp01(region.width / screenWidth);
            float vSize = Mathf.Clamp01(region.height / screenHeight);

            if (SystemInfo.graphicsUVStartsAtTop)
            {
                // ScreenCapture 텍스처의 상하 방향을 교정하면서 영역만 잘라낸다.
                Graphics.Blit(
                    source,
                    destination,
                    new Vector2(uSize, -vSize),
                    new Vector2(u, 1f - v));
            }
            else
            {
                Graphics.Blit(
                    source,
                    destination,
                    new Vector2(uSize, vSize),
                    new Vector2(u, v));
            }
        }

        bool EnsureMaterial()
        {
            if (_blurMaterial != null)
                return true;

            if (blurShader == null)
                blurShader = Shader.Find("ContextStage/UI/Kawase Blur");
            if (blurShader == null)
            {
                Debug.LogError(
                    "[UIRegionBlur] 블러 셰이더를 찾지 못했습니다. " +
                    "Assets/Shaders/UIKawaseBlur.shader 를 연결하세요.",
                    this);
                return false;
            }

            _blurMaterial = new Material(blurShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return true;
        }

        void EnsureCaptureTexture(int width, int height)
        {
            if (_capture != null && _capture.width == width && _capture.height == height)
                return;

            ReleaseTexture(ref _capture);
            _capture = new RenderTexture(width, height, 0)
            {
                name = "UIRegionBlur Capture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _capture.Create();
        }

        void EnsureBlurTextures(int width, int height)
        {
            if (_blurA != null && _blurA.width == width && _blurA.height == height)
                return;

            ReleaseTexture(ref _blurA);
            ReleaseTexture(ref _blurB);
            _blurA = CreateBlurTexture(width, height, "UIRegionBlur A");
            _blurB = CreateBlurTexture(width, height, "UIRegionBlur B");
        }

        RenderTexture CreateBlurTexture(int width, int height, string textureName)
        {
            var texture = new RenderTexture(width, height, 0)
            {
                name = textureName,
                filterMode = pixelatedFilter ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        void HideAndReleaseTextures()
        {
            if (blurImage != null)
            {
                blurImage.enabled = false;
                blurImage.texture = null;
            }

            ReleaseTexture(ref _capture);
            ReleaseTexture(ref _blurA);
            ReleaseTexture(ref _blurB);
        }

        static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            downscale = Mathf.Clamp(downscale, 1, 16);
            blurPasses = Mathf.Clamp(blurPasses, 0, 8);
            baseOffset = Mathf.Max(0f, baseOffset);
        }
#endif
    }
}
