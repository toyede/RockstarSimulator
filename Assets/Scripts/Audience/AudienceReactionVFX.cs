using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객 한 명이 소유하고 반복 재생하는 반응 VFX.
    /// 반응마다 오브젝트를 생성하지 않고, 준비된 파티클과 보조 스프라이트를 재사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudienceReactionVFX : MonoBehaviour
    {
        const float DepartureEmissionInterval = 0.1f;

        static readonly Color LoveWhite = Hex(0xFFFFFF);
        static readonly Color LoveGold = Hex(0xF9C22B);
        static readonly Color LoveOrange = Hex(0xFB6B1D);
        static readonly Color InterestedLight = Hex(0xC7DCD0);
        static readonly Color InterestedMint = Hex(0x30E1B9);
        static readonly Color BoredLight = Hex(0x7F708A);
        static readonly Color BoredDark = Hex(0x3E3546);
        static readonly Color DepartureDust = Hex(0x625565);
        static readonly Color FeverWhite = Hex(0xFFF7D6);
        static readonly Color FeverGold = Hex(0xF9C22B);
        static readonly Color FeverOrange = Hex(0xFB8B1D);

        [Header("Anchors")]
        [SerializeField] Vector3 headLocalPosition = new Vector3(0f, 1.55f, -0.05f);
        [SerializeField] Vector3 ellipsisLocalPosition = new Vector3(0f, 1.82f, -0.05f);
        [SerializeField] Vector3 frustrationLocalPosition = new Vector3(0f, 1.56f, -0.05f);
        [SerializeField] Vector3 arrowLocalPosition = new Vector3(0f, 1.72f, -0.05f);
        [SerializeField] Vector3 feetLocalPosition = new Vector3(0f, 0.12f, -0.05f);

        [Header("Auxiliary")]
        [SerializeField, Min(0.01f)] float flashSize = 0.44f;
        [SerializeField, Min(0.01f)] float ellipsisDuration = 0.58f;
        [SerializeField, Min(0.01f)] float frustrationDuration = 0.9f;
        [SerializeField, Min(0.01f)] float arrowDuration = 0.5f;

        ParticleSystem _loveSystem;
        ParticleSystem _interestedSystem;
        ParticleSystem _boredSystem;
        ParticleSystem _feverSystem;
        ParticleSystem _departureSystem;
        SpriteRenderer _flashRenderer;
        SpriteRenderer _ellipsisRenderer;
        SpriteRenderer _frustrationRenderer;
        SpriteRenderer _arrowRenderer;

        float _ellipsisRemaining;
        float _frustrationRemaining;
        float _arrowRemaining;
        float _departureRemaining;
        float _departureEmissionRemaining;
        Vector2 _departureDirection = Vector2.right;
        int _flashHideFrame = -1;
        int _sortingOrder;
        string _sortingLayerName = "Default";

        static Texture2D _shapeAtlas;
        static Material _particleMaterial;
        static Sprite[] _shapeSprites;

        enum ShapeSprite
        {
            Star,
            Bolt,
            Pixel,
            Note,
            Plus,
            Dot,
            Smoke,
            BrokenNote,
            Ellipsis,
            Dust,
            Footprint,
            Arrow,
            FrustrationLines,
            GoldDust,
            GoldVoxel,
            GoldSparkle
        }

        public bool HasLiveParticles =>
            IsAlive(_loveSystem) ||
            IsAlive(_interestedSystem) ||
            IsAlive(_boredSystem) ||
            IsAlive(_feverSystem) ||
            IsAlive(_departureSystem) ||
            _ellipsisRemaining > 0f ||
            _frustrationRemaining > 0f ||
            _arrowRemaining > 0f ||
            _flashHideFrame >= 0;

        void Awake()
        {
            EnsureVisuals();
            ResetVisual();
        }

        void OnDisable() => ResetVisual();

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (_flashHideFrame >= 0 && Time.frameCount >= _flashHideFrame)
            {
                _flashRenderer.enabled = false;
                _flashHideFrame = -1;
            }

            if (_ellipsisRemaining > 0f)
            {
                _ellipsisRemaining = Mathf.Max(0f, _ellipsisRemaining - deltaTime);
                SetAuxiliaryAlpha(
                    _ellipsisRenderer,
                    BoredLight,
                    _ellipsisRemaining / Mathf.Max(0.01f, ellipsisDuration));
            }

            if (_frustrationRemaining > 0f)
            {
                _frustrationRemaining = Mathf.Max(
                    0f,
                    _frustrationRemaining - deltaTime);
                SetAuxiliaryAlpha(
                    _frustrationRenderer,
                    BoredDark,
                    _frustrationRemaining /
                    Mathf.Max(0.01f, frustrationDuration));
            }

            if (_arrowRemaining > 0f)
            {
                _arrowRemaining = Mathf.Max(0f, _arrowRemaining - deltaTime);
                SetAuxiliaryAlpha(
                    _arrowRenderer,
                    DepartureDust,
                    _arrowRemaining / Mathf.Max(0.01f, arrowDuration));
            }

            if (_departureRemaining <= 0f) return;

            _departureRemaining = Mathf.Max(0f, _departureRemaining - deltaTime);
            _departureEmissionRemaining -= deltaTime;
            while (_departureEmissionRemaining <= 0f &&
                   _departureRemaining > 0f)
            {
                EmitDepartureDust();
                _departureEmissionRemaining += DepartureEmissionInterval;
            }
        }

        public void SetSorting(string sortingLayerName, int sortingOrder)
        {
            EnsureVisuals();
            _sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName)
                ? "Default"
                : sortingLayerName;
            _sortingOrder = sortingOrder;
            ApplySorting();
        }

        void ApplySorting()
        {
            SetParticleSorting(_loveSystem, _sortingLayerName, _sortingOrder);
            SetParticleSorting(_interestedSystem, _sortingLayerName, _sortingOrder);
            SetParticleSorting(_boredSystem, _sortingLayerName, _sortingOrder);
            SetParticleSorting(_feverSystem, _sortingLayerName, _sortingOrder + 1);
            SetParticleSorting(_departureSystem, _sortingLayerName, _sortingOrder);
            SetSpriteSorting(_flashRenderer, _sortingLayerName, _sortingOrder + 2);
            SetSpriteSorting(_ellipsisRenderer, _sortingLayerName, _sortingOrder + 2);
            SetSpriteSorting(_frustrationRenderer, _sortingLayerName, _sortingOrder + 1);
            SetSpriteSorting(_arrowRenderer, _sortingLayerName, _sortingOrder + 2);
        }

        public void PlayLoveIt()
        {
            EnsureVisuals();
            EmitBurst(
                _loveSystem,
                Random.Range(10, 15),
                0.45f,
                0.75f,
                1.8f,
                3f,
                60f,
                120f,
                0.16f,
                0.32f);

            _flashRenderer.transform.localScale = Vector3.one * flashSize;
            _flashRenderer.color = Color.white;
            _flashRenderer.enabled = true;
            _flashHideFrame = Time.frameCount + 1;
        }

        public void PlayInterested()
        {
            EnsureVisuals();
            EmitBurst(
                _interestedSystem,
                Random.Range(4, 7),
                0.35f,
                0.55f,
                0.8f,
                1.5f,
                72f,
                108f,
                0.1f,
                0.2f);
        }

        public void PlayBored()
        {
            EnsureVisuals();
            EmitBurst(
                _boredSystem,
                Random.Range(5, 9),
                0.8f,
                1.2f,
                0.02f,
                0.14f,
                65f,
                115f,
                0.12f,
                0.25f);

            _ellipsisRemaining = ellipsisDuration;
            _ellipsisRenderer.color = BoredLight;
            _ellipsisRenderer.enabled = true;
            _frustrationRemaining = frustrationDuration;
            _frustrationRenderer.transform.localScale =
                Vector3.one * Random.Range(0.62f, 0.78f);
            _frustrationRenderer.color = BoredDark;
            _frustrationRenderer.enabled = true;
        }

        public void PlayFeverBurst()
        {
            EnsureVisuals();

            // 큰 복셀 조각이 먼저 시선을 잡고 작은 금가루가 사이를 채운다.
            EmitBurst(
                _feverSystem,
                Random.Range(14, 19),
                0.5f,
                0.82f,
                1.8f,
                3.5f,
                35f,
                145f,
                0.13f,
                0.28f);
            EmitBurst(
                _feverSystem,
                Random.Range(10, 15),
                0.55f,
                0.95f,
                0.75f,
                2.2f,
                25f,
                155f,
                0.06f,
                0.13f);

            _flashRenderer.transform.localScale =
                Vector3.one * flashSize * 1.35f;
            _flashRenderer.color = FeverWhite;
            _flashRenderer.enabled = true;
            _flashHideFrame = Time.frameCount + 1;
        }

        public void PlayDeparture(
            Vector2 worldDirection,
            float moveDuration)
        {
            EnsureVisuals();
            _departureDirection =
                worldDirection.sqrMagnitude > 0.0001f
                    ? worldDirection.normalized
                    : Vector2.right;
            _departureRemaining = Mathf.Max(0.01f, moveDuration);
            _departureEmissionRemaining = DepartureEmissionInterval;
            EmitDepartureDust();

            float angle = Mathf.Atan2(
                _departureDirection.y,
                _departureDirection.x) * Mathf.Rad2Deg;
            _arrowRenderer.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _arrowRenderer.color = DepartureDust;
            _arrowRenderer.enabled = true;
            _arrowRemaining = arrowDuration;
        }

        public void ResetVisual()
        {
            StopAndClear(_loveSystem);
            StopAndClear(_interestedSystem);
            StopAndClear(_boredSystem);
            StopAndClear(_feverSystem);
            StopAndClear(_departureSystem);

            _ellipsisRemaining = 0f;
            _frustrationRemaining = 0f;
            _arrowRemaining = 0f;
            _departureRemaining = 0f;
            _departureEmissionRemaining = 0f;
            _flashHideFrame = -1;

            if (_flashRenderer != null) _flashRenderer.enabled = false;
            if (_ellipsisRenderer != null) _ellipsisRenderer.enabled = false;
            if (_frustrationRenderer != null) _frustrationRenderer.enabled = false;
            if (_arrowRenderer != null) _arrowRenderer.enabled = false;
        }

        void EnsureVisuals()
        {
            EnsureSharedResources();

            if (_loveSystem == null)
            {
                _loveSystem = CreateParticleSystem(
                    "LoveItParticles",
                    headLocalPosition,
                    new[]
                    {
                        ShapeSprite.Star,
                        ShapeSprite.Bolt,
                        ShapeSprite.Pixel
                    },
                    CreateGradient(LoveWhite, LoveGold, LoveOrange),
                    -0.1f,
                    18);
            }

            if (_interestedSystem == null)
            {
                _interestedSystem = CreateParticleSystem(
                    "InterestedParticles",
                    headLocalPosition,
                    new[]
                    {
                        ShapeSprite.Note,
                        ShapeSprite.Plus,
                        ShapeSprite.Dot
                    },
                    CreateGradient(InterestedLight, InterestedMint),
                    0f,
                    10);
            }

            if (_boredSystem == null)
            {
                _boredSystem = CreateParticleSystem(
                    "BoredParticles",
                    headLocalPosition,
                    new[]
                    {
                        ShapeSprite.Smoke,
                        ShapeSprite.BrokenNote,
                        ShapeSprite.FrustrationLines
                    },
                    CreateGradient(BoredLight, BoredDark),
                    0f,
                    12,
                    ParticleSystemSimulationSpace.Local);
            }

            if (_feverSystem == null)
            {
                _feverSystem = CreateParticleSystem(
                    "FeverGoldParticles",
                    headLocalPosition,
                    new[]
                    {
                        ShapeSprite.GoldDust,
                        ShapeSprite.GoldVoxel,
                        ShapeSprite.GoldSparkle,
                        ShapeSprite.Star
                    },
                    CreateGradient(FeverWhite, FeverGold, FeverOrange),
                    0.18f,
                    48);
            }

            if (_departureSystem == null)
            {
                _departureSystem = CreateParticleSystem(
                    "DepartureParticles",
                    feetLocalPosition,
                    new[]
                    {
                        ShapeSprite.Dust,
                        ShapeSprite.Footprint
                    },
                    CreateGradient(DepartureDust, DepartureDust),
                    0f,
                    14);
            }

            if (_flashRenderer == null)
                _flashRenderer = CreateAuxiliaryRenderer(
                    "LoveItFlash",
                    headLocalPosition,
                    ShapeSprite.Pixel);
            if (_ellipsisRenderer == null)
                _ellipsisRenderer = CreateAuxiliaryRenderer(
                    "BoredEllipsis",
                    ellipsisLocalPosition,
                    ShapeSprite.Ellipsis);
            if (_frustrationRenderer == null)
                _frustrationRenderer = CreateAuxiliaryRenderer(
                    "BoredFrustrationLines",
                    frustrationLocalPosition,
                    ShapeSprite.FrustrationLines);
            if (_arrowRenderer == null)
                _arrowRenderer = CreateAuxiliaryRenderer(
                    "DepartureArrow",
                    arrowLocalPosition,
                    ShapeSprite.Arrow);

            ApplySorting();
        }

        ParticleSystem CreateParticleSystem(
            string objectName,
            Vector3 localPosition,
            ShapeSprite[] shapes,
            Gradient gradient,
            float gravityModifier,
            int maxParticles,
            ParticleSystemSimulationSpace simulationSpace =
                ParticleSystemSimulationSpace.World)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = simulationSpace;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 0.2f;
            main.startColor = Color.white;
            main.gravityModifier = gravityModifier;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color =
                new ParticleSystem.MinMaxGradient(gradient);

            var textureSheet = system.textureSheetAnimation;
            textureSheet.enabled = true;
            textureSheet.mode = ParticleSystemAnimationMode.Sprites;
            textureSheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            textureSheet.startFrame =
                new ParticleSystem.MinMaxCurve(0f, 0.999f);
            textureSheet.frameOverTime = 0f;
            textureSheet.cycleCount = 1;
            for (int i = 0; i < shapes.Length; i++)
                textureSheet.AddSprite(_shapeSprites[(int)shapes[i]]);

            ParticleSystemRenderer renderer =
                child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = _particleMaterial;
            renderer.sortingLayerName = _sortingLayerName;
            renderer.sortingOrder = _sortingOrder;
            return system;
        }

        SpriteRenderer CreateAuxiliaryRenderer(
            string objectName,
            Vector3 localPosition,
            ShapeSprite shape)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = _shapeSprites[(int)shape];
            renderer.sortingLayerName = _sortingLayerName;
            renderer.sortingOrder = _sortingOrder + 2;
            renderer.enabled = false;
            return renderer;
        }

        static void EmitBurst(
            ParticleSystem system,
            int count,
            float lifetimeMin,
            float lifetimeMax,
            float speedMin,
            float speedMax,
            float angleMin,
            float angleMax,
            float sizeMin,
            float sizeMax)
        {
            if (system == null) return;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(angleMin, angleMax) * Mathf.Deg2Rad;
                float speed = Random.Range(speedMin, speedMax);
                bool localSimulation =
                    system.main.simulationSpace ==
                    ParticleSystemSimulationSpace.Local;
                var emit = new ParticleSystem.EmitParams
                {
                    position = localSimulation
                        ? Vector3.zero
                        : system.transform.position,
                    velocity = new Vector3(
                        Mathf.Cos(angle) * speed,
                        Mathf.Sin(angle) * speed,
                        0f),
                    startLifetime = Random.Range(lifetimeMin, lifetimeMax),
                    startSize = Random.Range(sizeMin, sizeMax),
                    startColor = Color.white,
                    rotation = Random.Range(0, 4) * 90f * Mathf.Deg2Rad
                };
                system.Emit(emit, 1);
            }
        }

        void EmitDepartureDust()
        {
            Vector2 backwards = -_departureDirection;
            float angle =
                Mathf.Atan2(backwards.y, backwards.x) +
                Random.Range(-18f, 18f) * Mathf.Deg2Rad;
            float speed = Random.Range(0.25f, 0.6f);
            var emit = new ParticleSystem.EmitParams
            {
                position = _departureSystem.transform.position,
                velocity = new Vector3(
                    Mathf.Cos(angle) * speed,
                    Mathf.Sin(angle) * speed,
                    0f),
                startLifetime = Random.Range(0.35f, 0.6f),
                startSize = Random.Range(0.11f, 0.2f),
                startColor = Color.white,
                rotation = Random.Range(-25f, 25f) * Mathf.Deg2Rad
            };
            _departureSystem.Emit(emit, 1);
        }

        static void EnsureSharedResources()
        {
            if (_shapeSprites != null &&
                _particleMaterial != null &&
                _shapeAtlas != null)
                return;

            const int cellSize = 8;
            const int columns = 4;
            const int rows = 4;
            _shapeAtlas = new Texture2D(
                cellSize * columns,
                cellSize * rows,
                TextureFormat.RGBA32,
                false)
            {
                name = "AudienceReactionPixelShapes",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] clearPixels =
                new Color[_shapeAtlas.width * _shapeAtlas.height];
            _shapeAtlas.SetPixels(clearPixels);

            string[][] patterns =
            {
                new[] { "...##...", "...##...", "########", ".######.", "..####..", ".##..##.", "##....##", "........" },
                new[] { "....##..", "...##...", "..####..", "....##..", "...##...", "..##....", ".##.....", "........" },
                new[] { ".######.", ".######.", ".######.", ".######.", ".######.", ".######.", "........", "........" },
                new[] { "...##...", "...###..", "...#.##.", "...#..##", "...#....", ".###....", ".###....", "........" },
                new[] { "...##...", "...##...", ".######.", ".######.", "...##...", "...##...", "........", "........" },
                new[] { "........", "...##...", "..####..", "..####..", "...##...", "........", "........", "........" },
                new[] { "..####..", ".######.", "########", "########", ".######.", "..####..", ".##..##.", "........" },
                new[] { "...##...", "...###..", "...#.##.", "......##", "...#....", ".###....", ".#......", "........" },
                new[] { "........", "........", "........", ".##.##.#", ".##.##.#", "........", "........", "........" },
                new[] { "........", "...##...", "..####..", ".######.", "..####..", "...##...", "........", "........" },
                new[] { ".###....", ".###....", "..##....", "....###.", "....###.", ".....##.", "........", "........" },
                new[] { "........", "...##...", "...###..", "#######.", "#######.", "...###..", "...##...", "........" },
                new[] { ".##.##..", ".##.##..", ".##.##..", ".##.##..", ".##.##..", ".##.##..", "........", "........" },
                new[] { "........", "........", "...##...", "...##...", "........", "........", "........", "........" },
                new[] { "...##...", "..####..", ".######.", "########", ".######.", "..####..", "...##...", "........" },
                new[] { "...##...", "...##...", ".######.", "...##...", "...##...", "........", "........", "........" }
            };

            _shapeSprites = new Sprite[patterns.Length];
            for (int index = 0; index < patterns.Length; index++)
            {
                int column = index % columns;
                int row = index / columns;
                DrawPattern(
                    _shapeAtlas,
                    column * cellSize,
                    row * cellSize,
                    patterns[index]);
                _shapeSprites[index] = Sprite.Create(
                    _shapeAtlas,
                    new Rect(
                        column * cellSize,
                        row * cellSize,
                        cellSize,
                        cellSize),
                    new Vector2(0.5f, 0.5f),
                    cellSize);
                _shapeSprites[index].name =
                    ((ShapeSprite)index).ToString();
                _shapeSprites[index].hideFlags =
                    HideFlags.HideAndDontSave;
            }
            _shapeAtlas.Apply(false, true);

            Shader shader =
                Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError(
                    "[AudienceReactionVFX] A sprite-compatible shader is required.");
                return;
            }

            _particleMaterial = new Material(shader)
            {
                name = "AudienceReactionPixelParticleMaterial",
                mainTexture = _shapeAtlas,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        static void DrawPattern(
            Texture2D texture,
            int originX,
            int originY,
            string[] pattern)
        {
            for (int sourceY = 0; sourceY < pattern.Length; sourceY++)
            {
                string line = pattern[sourceY];
                int targetY = originY + pattern.Length - 1 - sourceY;
                for (int x = 0; x < line.Length; x++)
                {
                    if (line[x] == '#')
                        texture.SetPixel(originX + x, targetY, Color.white);
                }
            }
        }

        static Gradient CreateGradient(Color start, Color end)
            => CreateGradient(start, Color.Lerp(start, end, 0.5f), end);

        static Gradient CreateGradient(Color start, Color middle, Color end)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(middle, 0.42f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.58f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        static Color Hex(int rgb) =>
            new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);

        static void SetAuxiliaryAlpha(
            SpriteRenderer renderer,
            Color baseColor,
            float alpha)
        {
            if (renderer == null) return;
            Color color = baseColor;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
            renderer.enabled = color.a > 0f;
        }

        static void SetParticleSorting(
            ParticleSystem system,
            string sortingLayerName,
            int sortingOrder)
        {
            if (system == null) return;
            ParticleSystemRenderer renderer =
                system.GetComponent<ParticleSystemRenderer>();
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
        }

        static void SetSpriteSorting(
            SpriteRenderer renderer,
            string sortingLayerName,
            int sortingOrder)
        {
            if (renderer == null) return;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
        }

        static bool IsAlive(ParticleSystem system)
            => system != null && system.IsAlive(true);

        static void StopAndClear(ParticleSystem system)
        {
            if (system == null) return;
            system.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
