using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 특별 관객의 종/성향을 읽히게 하는 로컬 픽셀 파티클.
    /// Chill=땀, Singalong=음표, Mosh=붉은 분노 주름을 한 인스턴스에서 재사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialAudiencePersonalityVFX : MonoBehaviour
    {
        static readonly Color SweatLight = Hex(0xC7F5FF);
        static readonly Color SweatBlue = Hex(0x30A7E1);
        static readonly Color NotePurple = Hex(0xB967FF);
        static readonly Color NoteYellow = Hex(0xF9C22B);
        static readonly Color AngerRed = Hex(0xFF2B12);
        static readonly Color AngerOrange = Hex(0xFB6B1D);

        [SerializeField] Vector3 headLocalPosition = new Vector3(0f, 1.52f, -0.08f);
        [SerializeField, Min(0.1f)] float ambientIntervalMin = 0.48f;
        [SerializeField, Min(0.1f)] float ambientIntervalMax = 0.78f;
        [SerializeField, Min(0.05f)] float angerDuration = 0.38f;
        [SerializeField, Min(0.1f)] float angerCreaseScale = 1.35f;

        ParticleSystem _particles;
        readonly SpriteRenderer[] _angerCreases = new SpriteRenderer[4];
        HeatStage _stage;
        bool _active;
        float _nextAmbientAt;
        float _angerRemaining;
        int _sortingOrder;
        string _sortingLayerName = "Default";

        static Texture2D _atlas;
        static Texture2D _creaseTexture;
        static Sprite[] _sprites;
        static Sprite _creaseSprite;
        static Material _particleMaterial;

        enum Shape
        {
            Sweat,
            Note,
            Pixel
        }

        public HeatStage ActiveStage => _stage;
        public bool IsActive => _active;
        public int LiveParticleCount =>
            _particles != null ? _particles.particleCount : 0;

        void Awake()
        {
            EnsureVisuals();
            Hide();
        }

        void OnDisable() => Hide();

        void Update()
        {
            if (!_active) return;

            if (_angerRemaining > 0f)
            {
                _angerRemaining = Mathf.Max(
                    0f,
                    _angerRemaining - Time.deltaTime);
                float t = _angerRemaining / Mathf.Max(0.01f, angerDuration);
                float pulse =
                    1f + Mathf.Sin((1f - t) * Mathf.PI) * 0.2f;
                SetCreaseVisual(t, angerCreaseScale * pulse);
            }

            if (Time.time < _nextAmbientAt) return;
            PlayAccent(strong: false);
            ScheduleAmbient();
        }

        public void Show(
            HeatStage stage,
            string sortingLayerName,
            int sortingOrder)
        {
            EnsureVisuals();
            _stage = stage;
            _active = true;
            SetSorting(sortingLayerName, sortingOrder);
            ScheduleAmbient();
            PlayAccent(strong: false);
        }

        public void Hide()
        {
            _active = false;
            _angerRemaining = 0f;
            if (_particles != null)
            {
                _particles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            SetCreaseVisual(0f, 1f);
        }

        public void PlayAccent(bool strong)
        {
            if (!_active) return;

            switch (_stage)
            {
                case HeatStage.Chill:
                    EmitSweat(strong ? 4 : 2);
                    break;
                case HeatStage.Singalong:
                    EmitNotes(strong ? 7 : 3);
                    break;
                case HeatStage.Mosh:
                    ShowAngerCreases(strong);
                    EmitAngerPixels(strong ? 6 : 2);
                    break;
            }
        }

        public void SetSorting(string layerName, int order)
        {
            _sortingLayerName = string.IsNullOrWhiteSpace(layerName)
                ? "Default"
                : layerName;
            _sortingOrder = order;

            if (_particles != null)
            {
                var renderer =
                    _particles.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerName = _sortingLayerName;
                renderer.sortingOrder = _sortingOrder;
            }

            for (int i = 0; i < _angerCreases.Length; i++)
            {
                if (_angerCreases[i] == null) continue;
                _angerCreases[i].sortingLayerName = _sortingLayerName;
                _angerCreases[i].sortingOrder = _sortingOrder + 1;
            }
        }

        void EmitSweat(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Emit(
                    Shape.Sweat,
                    new Vector3(side * Random.Range(0.2f, 0.38f), 0.04f, 0f),
                    new Vector3(
                        side * Random.Range(0.08f, 0.2f),
                        Random.Range(-0.45f, -0.2f),
                        0f),
                    Random.Range(0.42f, 0.65f),
                    Random.Range(0.12f, 0.19f),
                    Color.Lerp(SweatLight, SweatBlue, Random.value));
            }
        }

        void EmitNotes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(65f, 115f) * Mathf.Deg2Rad;
                float speed = Random.Range(0.75f, 1.45f);
                Emit(
                    Shape.Note,
                    new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f),
                    new Vector3(
                        Mathf.Cos(angle) * speed,
                        Mathf.Sin(angle) * speed,
                        0f),
                    Random.Range(0.5f, 0.8f),
                    Random.Range(0.14f, 0.23f),
                    Color.Lerp(NotePurple, NoteYellow, Random.value));
            }
        }

        void EmitAngerPixels(int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(40f, 140f) * Mathf.Deg2Rad;
                float speed = Random.Range(0.45f, 1.1f);
                Emit(
                    Shape.Pixel,
                    new Vector3(Random.Range(-0.28f, 0.28f), 0f, 0f),
                    new Vector3(
                        Mathf.Cos(angle) * speed,
                        Mathf.Sin(angle) * speed,
                        0f),
                    Random.Range(0.24f, 0.42f),
                    Random.Range(0.1f, 0.18f),
                    Color.Lerp(AngerRed, AngerOrange, Random.value));
            }
        }

        void Emit(
            Shape shape,
            Vector3 localPosition,
            Vector3 localVelocity,
            float lifetime,
            float size,
            Color color)
        {
            var animation = _particles.textureSheetAnimation;
            int spriteCount = (int)Shape.Pixel + 1;
            float frame = ((int)shape + 0.01f) / spriteCount;
            animation.startFrame =
                new ParticleSystem.MinMaxCurve(frame, frame);
            var emit = new ParticleSystem.EmitParams
            {
                position = localPosition,
                velocity = localVelocity,
                startLifetime = lifetime,
                startSize = size,
                startColor = color,
                rotation = Random.Range(-15f, 15f) * Mathf.Deg2Rad
            };
            _particles.Emit(emit, 1);
        }

        void ShowAngerCreases(bool strong)
        {
            _angerRemaining = strong
                ? angerDuration * 1.35f
                : angerDuration;
            for (int i = 0; i < _angerCreases.Length; i++)
            {
                SpriteRenderer crease = _angerCreases[i];
                crease.color = i % 2 == 0 ? AngerRed : AngerOrange;
                crease.enabled = true;
            }
            SetCreaseVisual(
                1f,
                angerCreaseScale * (strong ? 1.22f : 1f));
        }

        void SetCreaseVisual(float alpha, float scale)
        {
            for (int i = 0; i < _angerCreases.Length; i++)
            {
                SpriteRenderer crease = _angerCreases[i];
                if (crease == null) continue;
                Color color = i % 2 == 0 ? AngerRed : AngerOrange;
                color.a = Mathf.Clamp01(alpha);
                crease.color = color;
                crease.transform.localScale =
                    new Vector3(
                        (i % 2 == 0 ? 1f : -1f) * scale,
                        (i >= 2 ? -1f : 1f) * scale,
                        1f);
                crease.enabled = color.a > 0f;
            }
        }

        void ScheduleAmbient()
        {
            _nextAmbientAt = Time.time + Random.Range(
                Mathf.Min(ambientIntervalMin, ambientIntervalMax),
                Mathf.Max(ambientIntervalMin, ambientIntervalMax));
        }

        void EnsureVisuals()
        {
            EnsureSharedResources();
            if (_particles == null)
                _particles = CreateParticleSystem();

            Vector3[] offsets =
            {
                new Vector3(-0.58f, 0.24f, 0f),
                new Vector3(0.58f, 0.24f, 0f),
                new Vector3(-0.48f, -0.18f, 0f),
                new Vector3(0.48f, -0.18f, 0f)
            };
            Vector3 resolvedHeadPosition = ResolveHeadLocalPosition();

            for (int i = 0; i < _angerCreases.Length; i++)
            {
                if (_angerCreases[i] != null) continue;
                var child = new GameObject($"MoshAngerCrease_{i + 1}");
                child.transform.SetParent(transform, false);
                child.transform.localPosition = resolvedHeadPosition + offsets[i];
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sprite = _creaseSprite;
                renderer.enabled = false;
                _angerCreases[i] = renderer;
            }
            SetSorting(_sortingLayerName, _sortingOrder);
        }

        ParticleSystem CreateParticleSystem()
        {
            var child = new GameObject("PersonalityPixelParticles");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = ResolveHeadLocalPosition();
            var system = child.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 24;
            main.startLifetime = 0.6f;
            main.startSpeed = 0f;
            main.startSize = 0.18f;
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeGradient());

            var sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            sheet.frameOverTime = 0f;
            for (int i = 0; i < _sprites.Length; i++)
                sheet.AddSprite(_sprites[i]);

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = _particleMaterial;
            return system;
        }

        Vector3 ResolveHeadLocalPosition()
        {
            SpriteRenderer character = GetComponent<SpriteRenderer>();
            if (character == null || character.sprite == null)
                return headLocalPosition;

            Bounds bounds = character.sprite.bounds;
            Vector3 resolved = headLocalPosition;
            if (bounds.max.y > 2f)
                resolved.y = Mathf.Max(
                    resolved.y,
                    Mathf.Lerp(bounds.center.y, bounds.max.y, 0.72f));
            return resolved;
        }

        static void EnsureSharedResources()
        {
            if (_sprites != null &&
                _creaseSprite != null &&
                _particleMaterial != null)
                return;

            const int cell = 8;
            _atlas = new Texture2D(
                cell * 3,
                cell,
                TextureFormat.RGBA32,
                false)
            {
                name = "SpecialAudiencePersonalityShapes",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _atlas.SetPixels(new Color[_atlas.width * _atlas.height]);
            string[][] patterns =
            {
                new[] { "...##...", "..####..", "..####..", ".######.", ".######.", "..####..", "...##...", "........" },
                new[] { "...##...", "...###..", "...#.##.", "...#..##", "...#....", ".###....", ".###....", "........" },
                new[] { "........", ".######.", ".######.", ".######.", ".######.", ".######.", ".######.", "........" }
            };

            _sprites = new Sprite[patterns.Length];
            for (int i = 0; i < patterns.Length; i++)
            {
                DrawPattern(_atlas, i * cell, 0, patterns[i]);
                _sprites[i] = Sprite.Create(
                    _atlas,
                    new Rect(i * cell, 0f, cell, cell),
                    new Vector2(0.5f, 0.5f),
                    cell);
                _sprites[i].hideFlags = HideFlags.HideAndDontSave;
            }
            _atlas.Apply(false, true);

            string[] creasePattern =
            {
                "............",
                ".###........",
                "...###......",
                ".....###....",
                ".......###..",
                ".......##...",
                "......##....",
                ".....##.....",
                "............",
                "............",
                "............",
                "............"
            };
            _creaseTexture = new Texture2D(
                12,
                12,
                TextureFormat.RGBA32,
                false)
            {
                name = "MoshPixelAngerCrease",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _creaseTexture.SetPixels(new Color[144]);
            DrawPattern(_creaseTexture, 0, 0, creasePattern);
            _creaseTexture.Apply(false, true);
            _creaseSprite = Sprite.Create(
                _creaseTexture,
                new Rect(0f, 0f, 12f, 12f),
                new Vector2(0.5f, 0.5f),
                12f);
            _creaseSprite.hideFlags = HideFlags.HideAndDontSave;

            Shader shader =
                Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError(
                    "[SpecialAudiencePersonalityVFX] Sprite shader를 찾을 수 없습니다.");
                return;
            }
            _particleMaterial = new Material(shader)
            {
                name = "SpecialAudiencePersonalityMaterial",
                mainTexture = _atlas,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        static void DrawPattern(
            Texture2D texture,
            int originX,
            int originY,
            string[] pattern)
        {
            for (int y = 0; y < pattern.Length; y++)
            {
                for (int x = 0; x < pattern[y].Length; x++)
                {
                    if (pattern[y][x] == '#')
                    {
                        texture.SetPixel(
                            originX + x,
                            originY + pattern.Length - 1 - y,
                            Color.white);
                    }
                }
            }
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

        static Color Hex(int rgb) =>
            new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
    }
}
