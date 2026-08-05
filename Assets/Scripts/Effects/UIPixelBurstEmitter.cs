using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// Screen Space UI에서 쓰는 가벼운 픽셀 파티클 풀.
    /// 점수·랭크·타이머가 같은 시각 언어를 공유하도록 작은 사각형만 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIPixelBurstEmitter : MonoBehaviour
    {
        const int MaximumParticles = 64;

        sealed class Pixel
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public float Remaining;
            public float Lifetime;
            public float RotationSpeed;
        }

        readonly List<Pixel> _pixels = new List<Pixel>(MaximumParticles);
        Canvas _canvas;
        RectTransform _canvasRect;

        public void EmitBurst(
            RectTransform source,
            Color color,
            int count,
            float minimumSpeed = 80f,
            float maximumSpeed = 180f)
        {
            if (source == null || count <= 0) return;
            EnsureCanvas();
            if (!TryResolvePosition(source, out Vector2 origin)) return;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(minimumSpeed, maximumSpeed);
                Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                ActivatePixel(origin, velocity, color, Random.Range(0.28f, 0.52f));
            }
        }

        public void EmitDust(RectTransform source, Color color, int count = 1)
        {
            if (source == null || count <= 0) return;
            EnsureCanvas();
            if (!TryResolvePosition(source, out Vector2 origin)) return;

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = new Vector2(Random.Range(-5f, 5f), Random.Range(-3f, 3f));
                Vector2 velocity = new Vector2(Random.Range(-35f, 10f), Random.Range(-75f, -28f));
                ActivatePixel(origin + offset, velocity, color, Random.Range(0.35f, 0.65f));
            }
        }

        void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            for (int i = 0; i < _pixels.Count; i++)
            {
                Pixel pixel = _pixels[i];
                if (pixel.Remaining <= 0f) continue;

                pixel.Remaining -= deltaTime;
                if (pixel.Remaining <= 0f)
                {
                    pixel.Rect.gameObject.SetActive(false);
                    continue;
                }

                pixel.Velocity += Vector2.down * (90f * deltaTime);
                pixel.Rect.anchoredPosition += pixel.Velocity * deltaTime;
                pixel.Rect.Rotate(0f, 0f, pixel.RotationSpeed * deltaTime);

                Color color = pixel.Image.color;
                color.a = Mathf.Clamp01(pixel.Remaining / pixel.Lifetime);
                pixel.Image.color = color;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < _pixels.Count; i++)
            {
                _pixels[i].Remaining = 0f;
                if (_pixels[i].Rect != null)
                    _pixels[i].Rect.gameObject.SetActive(false);
            }
        }

        void ActivatePixel(
            Vector2 position,
            Vector2 velocity,
            Color color,
            float lifetime)
        {
            Pixel pixel = GetPixel();
            float size = Random.Range(4f, 9f);
            pixel.Rect.gameObject.SetActive(true);
            pixel.Rect.anchoredPosition = position;
            pixel.Rect.sizeDelta = new Vector2(size, size);
            pixel.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            pixel.Image.color = color;
            pixel.Velocity = velocity;
            pixel.Lifetime = Mathf.Max(0.05f, lifetime);
            pixel.Remaining = pixel.Lifetime;
            pixel.RotationSpeed = Random.Range(-220f, 220f);
        }

        Pixel GetPixel()
        {
            for (int i = 0; i < _pixels.Count; i++)
                if (_pixels[i].Remaining <= 0f)
                    return _pixels[i];

            if (_pixels.Count < MaximumParticles)
            {
                Pixel created = CreatePixel();
                _pixels.Add(created);
                return created;
            }

            int oldestIndex = 0;
            float leastRemaining = _pixels[0].Remaining;
            for (int i = 1; i < _pixels.Count; i++)
            {
                if (_pixels[i].Remaining >= leastRemaining) continue;
                oldestIndex = i;
                leastRemaining = _pixels[i].Remaining;
            }
            return _pixels[oldestIndex];
        }

        Pixel CreatePixel()
        {
            var gameObject = new GameObject(
                "UIPixel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(_canvasRect, false);

            var image = gameObject.GetComponent<Image>();
            image.raycastTarget = false;
            gameObject.SetActive(false);

            return new Pixel
            {
                Rect = (RectTransform)gameObject.transform,
                Image = image,
            };
        }

        bool TryResolvePosition(RectTransform source, out Vector2 localPosition)
        {
            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
            Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
            Camera camera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? sourceCanvas.worldCamera
                : null;
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPosition,
                null,
                out localPosition);
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;

            var canvasObject = new GameObject(
                "UIPixelVFXCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 124;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRect = canvasObject.GetComponent<RectTransform>();
        }
    }
}
