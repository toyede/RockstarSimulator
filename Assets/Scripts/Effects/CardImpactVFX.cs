using System.Collections;
using GameJamKit;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextStage
{
    /// <summary>
    /// 카드 한 장의 사용을 무대 중앙에 전달하는 재사용 임팩트.
    /// 흰색 플래시, 픽셀 방사, 픽셀 링과 기존 Bloom의 짧은 가산 펄스를 담당한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardImpactVFX : MonoBehaviour
    {
        [Header("Position")]
        [SerializeField] Transform impactAnchor;
        [SerializeField] Vector2 fallbackViewportPosition = new Vector2(0.5f, 0.55f);
        [SerializeField] int sortingOrder = 80;

        [Header("Timing")]
        [SerializeField, Range(0.05f, 0.1f)] float flashDuration = 0.075f;
        [SerializeField, Min(0.05f)] float ringDuration = 0.15f;

        [Header("Shape")]
        [SerializeField, Min(0.05f)] float flashWorldSize = 0.65f;
        [SerializeField, Min(0.05f)] float ringWorldSize = 1.25f;
        [SerializeField, Range(8, 12)] int minimumRadialPixels = 8;
        [SerializeField, Range(8, 12)] int maximumRadialPixels = 12;

        [Header("Bloom")]
        [SerializeField, Min(0f), Tooltip(
            "씬의 기존 Bloom 값을 보존하면서 더할 피크값. 0.3→0.65에 해당하는 +0.35 펄스")]
        float bloomIntensityBoost = 0.35f;
        [SerializeField, Min(0.05f)] float bloomDuration = 0.12f;

        ParticleSystem _radialSystem;
        SpriteRenderer _flashRenderer;
        SpriteRenderer _ringRenderer;
        Coroutine _routine;
        Bloom _bloom;
        float _bloomBaseline;
        bool _bloomDriven;

        static Texture2D _pixelTexture;
        static Texture2D _ringTexture;
        static Sprite _pixelSprite;
        static Sprite _ringSprite;
        static Material _particleMaterial;

        public Color LastImpactColor { get; private set; } = Color.white;
        public int LastBurstCount { get; private set; }

        void Awake()
        {
            EnsureVisuals();
            ResolveBloom();
            HideSprites();
        }

        void OnEnable() =>
            EventBus.Subscribe<CardPresentationStarted>(OnCardPresentationStarted);

        void OnDisable()
        {
            EventBus.Unsubscribe<CardPresentationStarted>(OnCardPresentationStarted);
            StopCurrent();
            HideSprites();
        }

        void OnCardPresentationStarted(CardPresentationStarted e)
        {
            Color familyColor = CardVFXPalette.ResolveFamily(e.Role, e.TargetStage);
            Play(familyColor);
        }

        public void Play(Color familyColor)
        {
            EnsureVisuals();
            StopCurrent();

            LastImpactColor = familyColor;
            Vector3 position = ResolveImpactPosition();
            _flashRenderer.transform.position = position;
            _ringRenderer.transform.position = position;
            EmitRadialPixels(position, familyColor);
            _routine = StartCoroutine(ImpactRoutine(familyColor));
        }

        IEnumerator ImpactRoutine(Color familyColor)
        {
            _flashRenderer.enabled = true;
            _flashRenderer.color = Hdr(Color.white, 2.2f);
            _flashRenderer.transform.localScale =
                Vector3.one * flashWorldSize;

            _ringRenderer.enabled = true;
            _ringRenderer.color = Hdr(familyColor, 1.7f);
            _ringRenderer.transform.localScale =
                Vector3.one * ringWorldSize * 0.3f;

            BeginBloom();
            float elapsed = 0f;
            float duration = Mathf.Max(
                flashDuration,
                Mathf.Max(ringDuration, bloomDuration));

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;

                float flashT = Mathf.Clamp01(elapsed / flashDuration);
                Color flashColor = Hdr(Color.white, 2.2f);
                flashColor.a = 1f - flashT;
                _flashRenderer.color = flashColor;
                _flashRenderer.enabled = flashT < 1f;

                float ringT = Mathf.Clamp01(elapsed / ringDuration);
                float ringScale = Mathf.Lerp(0.3f, 1.5f, EaseOutCubic(ringT));
                _ringRenderer.transform.localScale =
                    Vector3.one * ringWorldSize * ringScale;
                Color ringColor = Hdr(familyColor, 1.7f);
                ringColor.a = 1f - ringT;
                _ringRenderer.color = ringColor;
                _ringRenderer.enabled = ringT < 1f;

                UpdateBloom(elapsed);
                yield return null;
            }

            RestoreBloom();
            HideSprites();
            _routine = null;
        }

        void EmitRadialPixels(Vector3 position, Color familyColor)
        {
            LastBurstCount = Random.Range(
                Mathf.Min(minimumRadialPixels, maximumRadialPixels),
                Mathf.Max(minimumRadialPixels, maximumRadialPixels) + 1);
            float step = 360f / LastBurstCount;

            for (int i = 0; i < LastBurstCount; i++)
            {
                float angle = (i * step + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                float speed = Random.Range(2.4f, 4f);
                Color color = i % 3 == 0
                    ? Color.white
                    : Color.Lerp(familyColor, Color.white, Random.Range(0f, 0.2f));
                color = Hdr(color, 1.45f);

                _radialSystem.Emit(new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(
                        Mathf.Cos(angle) * speed,
                        Mathf.Sin(angle) * speed,
                        0f),
                    startLifetime = Random.Range(0.16f, 0.24f),
                    startSize = Random.Range(0.12f, 0.24f),
                    startColor = color,
                    rotation = Random.Range(0, 4) * 90f * Mathf.Deg2Rad
                }, 1);
            }
        }

        void EnsureVisuals()
        {
            EnsureSharedResources();
            if (_radialSystem == null)
                _radialSystem = CreateParticleSystem();
            if (_flashRenderer == null)
                _flashRenderer = CreateRenderer("CardImpactFlash", _pixelSprite, sortingOrder + 2);
            if (_ringRenderer == null)
                _ringRenderer = CreateRenderer("CardImpactRing", _ringSprite, sortingOrder + 1);
        }

        ParticleSystem CreateParticleSystem()
        {
            var child = new GameObject("CardImpactPixels");
            child.transform.SetParent(transform, false);
            var system = child.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.startLifetime = 0.2f;
            main.startSpeed = 0f;
            main.startSize = 0.18f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeGradient());

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = _particleMaterial;
            renderer.sortingOrder = sortingOrder;
            return system;
        }

        SpriteRenderer CreateRenderer(
            string objectName,
            Sprite sprite,
            int order)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        Vector3 ResolveImpactPosition()
        {
            if (impactAnchor != null) return impactAnchor.position;

            Camera camera = Camera.main;
            if (camera == null) return Vector3.zero;

            float distance = camera.orthographic
                ? Mathf.Abs(camera.transform.position.z)
                : Mathf.Max(0.01f, Mathf.Abs(camera.transform.position.z));
            Vector3 position = camera.ViewportToWorldPoint(
                new Vector3(
                    Mathf.Clamp01(fallbackViewportPosition.x),
                    Mathf.Clamp01(fallbackViewportPosition.y),
                    distance));
            position.z = 0f;
            return position;
        }

        void ResolveBloom()
        {
            Volume volume = FindFirstObjectByType<Volume>();
            if (volume == null || volume.profile == null) return;
            volume.profile.TryGet(out _bloom);
        }

        void BeginBloom()
        {
            if (_bloom == null) ResolveBloom();
            if (_bloom == null || bloomIntensityBoost <= 0f) return;

            _bloomBaseline = _bloom.intensity.value;
            _bloomDriven = true;
        }

        void UpdateBloom(float elapsed)
        {
            if (!_bloomDriven || _bloom == null) return;

            float t = Mathf.Clamp01(elapsed / bloomDuration);
            float envelope = t < 0.3f
                ? t / 0.3f
                : 1f - ((t - 0.3f) / 0.7f);
            _bloom.intensity.value =
                _bloomBaseline + bloomIntensityBoost * Mathf.Clamp01(envelope);
        }

        void RestoreBloom()
        {
            if (_bloomDriven && _bloom != null)
                _bloom.intensity.value = _bloomBaseline;
            _bloomDriven = false;
        }

        void StopCurrent()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            RestoreBloom();
        }

        void HideSprites()
        {
            if (_flashRenderer != null) _flashRenderer.enabled = false;
            if (_ringRenderer != null) _ringRenderer.enabled = false;
        }

        static void EnsureSharedResources()
        {
            if (_pixelSprite != null &&
                _ringSprite != null &&
                _particleMaterial != null)
                return;

            _pixelTexture = BuildTexture(
                "CardImpactPixel",
                new[]
                {
                    "........",
                    ".######.",
                    ".######.",
                    ".######.",
                    ".######.",
                    ".######.",
                    ".######.",
                    "........"
                });
            _ringTexture = BuildTexture(
                "CardImpactRing",
                new[]
                {
                    "................",
                    ".....######.....",
                    "...##......##...",
                    "..##........##..",
                    "..#..........#..",
                    ".##..........##.",
                    ".#............#.",
                    ".#............#.",
                    ".#............#.",
                    ".#............#.",
                    ".##..........##.",
                    "..#..........#..",
                    "..##........##..",
                    "...##......##...",
                    ".....######.....",
                    "................"
                });

            _pixelSprite = CreateSprite(_pixelTexture, 8f);
            _ringSprite = CreateSprite(_ringTexture, 16f);

            Shader shader =
                Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[CardImpactVFX] Sprite shader를 찾을 수 없습니다.");
                return;
            }

            _particleMaterial = new Material(shader)
            {
                name = "CardImpactPixelMaterial",
                mainTexture = _pixelTexture,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        static Texture2D BuildTexture(string textureName, string[] pattern)
        {
            int height = pattern.Length;
            int width = pattern[0].Length;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = textureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var clear = new Color[width * height];
            texture.SetPixels(clear);
            for (int y = 0; y < height; y++)
            {
                string row = pattern[y];
                for (int x = 0; x < width; x++)
                {
                    if (row[x] == '#')
                        texture.SetPixel(x, height - 1 - y, Color.white);
                }
            }
            texture.Apply(false, true);
            return texture;
        }

        static Sprite CreateSprite(Texture2D texture, float pixelsPerUnit)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Gradient FadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        static float EaseOutCubic(float t) =>
            1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        static Color Hdr(Color color, float intensity)
        {
            color.r *= intensity;
            color.g *= intensity;
            color.b *= intensity;
            return color;
        }
    }
}
