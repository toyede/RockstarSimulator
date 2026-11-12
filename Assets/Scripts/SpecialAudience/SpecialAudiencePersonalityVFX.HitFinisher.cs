using UnityEngine;

namespace ContextStage
{
    public sealed partial class SpecialAudiencePersonalityVFX
    {
        const int FinisherSlots = 5;
        const float FinisherSeconds = 0.46f;
        SpriteRenderer[] _finisherSprites;
        HeatStage _finisherStage;
        Vector3 _finisherFeet;
        Vector3 _finisherHead;
        float _finisherUnit;
        float _finisherAge;
        bool _finisherPlaying;

        static Sprite _rippleSprite;
        static Sprite _shockSprite;
        static readonly Color RippleInk = Hex(0x63E9EC);
        static readonly Color ChorusInk = Hex(0xCE96FF);
        static readonly Color ShockInk = Hex(0xFF503B);
        static readonly Color SparkInk = Hex(0xFFD395);

        // 기존 성향 표시와 별개로, 적중한 순간에만 한 번 재생한다.
        public void PlayHitFinisher(HeatStage stage, SpriteRenderer target)
        {
            if (!_active || target == null || target.sprite == null) return;
            if (stage != HeatStage.Chill && stage != HeatStage.Singalong && stage != HeatStage.Mosh) return;

            EnsureHitFinisher();
            Bounds bounds = target.bounds;
            _finisherFeet = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.10f, bounds.center.z);
            _finisherHead = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.12f, bounds.center.z);
            _finisherUnit = Mathf.Clamp(bounds.size.y, 0.6f, 1.85f);
            _finisherStage = stage;
            _finisherAge = 0f;
            _finisherPlaying = true;
            _nextAmbientAt = Mathf.Max(_nextAmbientAt, Time.time + FinisherSeconds);
            RenderHitFinisher(0f);
        }

        void TickHitFinisher()
        {
            if (!_finisherPlaying) return;
            _finisherAge += Time.deltaTime;
            if (_finisherAge >= FinisherSeconds)
            {
                HideHitFinisher();
                return;
            }
            RenderHitFinisher(_finisherAge / FinisherSeconds);
        }

        void HideHitFinisher()
        {
            _finisherPlaying = false;
            if (_finisherSprites == null) return;
            foreach (var sprite in _finisherSprites) sprite.enabled = false;
        }

        void RenderHitFinisher(float t)
        {
            for (int i = 0; i < FinisherSlots; i++)
            {
                switch (_finisherStage)
                {
                    case HeatStage.Chill: DrawRipple(i, t); break;
                    case HeatStage.Singalong: DrawChorus(i, t); break;
                    case HeatStage.Mosh: DrawShock(i, t); break;
                }
            }
        }

        void DrawRipple(int index, float t)
        {
            if (index < 2)
            {
                float wave = (t - index * 0.19f) / (1f - index * 0.19f);
                float size = Mathf.Lerp(0.53f, 1.65f, EaseOut(wave));
                Color tint = index == 0 ? RippleInk : SweatLight;
                DrawFinisher(index, _rippleSprite, _finisherFeet, new Vector2(size, size * 0.32f),
                    tint, wave < 0f ? 0f : (1f - wave) * 0.82f);
                return;
            }

            float lane = (index - 3) * 0.43f;
            Vector3 offset = new Vector3(lane * (0.45f + t), 0.10f + Mathf.Sin(t * Mathf.PI) * 0.19f);
            DrawFinisher(index, _sprites[(int)Shape.Pixel], _finisherFeet + offset * _finisherUnit,
                new Vector2(0.07f, 0.07f), SweatLight, (1f - t) * 0.86f);
        }

        void DrawChorus(int index, float t)
        {
            float delay = index * 0.075f;
            float flight = (t - delay) / (1f - delay);
            float side = index % 2 == 0 ? 1f : -1f;
            float spread = side * (0.10f + 0.055f * index + 0.33f * EaseOut(flight));
            Vector3 offset = new Vector3(spread, 0.04f + 0.77f * EaseOut(flight));
            float size = (index < 2 ? 0.31f : 0.24f) * (1f - 0.3f * Mathf.Clamp01(flight));
            DrawFinisher(index, _sprites[(int)Shape.Note], _finisherHead + offset * _finisherUnit,
                new Vector2(size, size), index % 2 == 0 ? ChorusInk : NoteYellow,
                flight < 0f ? 0f : Mathf.Clamp01((1f - flight) * 2.2f));
        }

        void DrawShock(int index, float t)
        {
            float spread = EaseOut(Mathf.Clamp01(t * 1.65f));
            if (index == 0)
            {
                DrawFinisher(index, _shockSprite, _finisherFeet,
                    new Vector2(0.55f + 1.37f * spread, 0.36f - 0.13f * spread), ShockInk,
                    Mathf.Clamp01(1f - t * 1.38f));
                return;
            }

            float side = index % 2 == 0 ? 1f : -1f;
            float stride = index < 3 ? 1f : 0.72f;
            Vector3 offset = new Vector3(side * (0.22f + 0.88f * spread) * stride,
                0.03f + (index < 3 ? 0.13f : 0.25f) * Mathf.Sin(t * Mathf.PI));
            DrawFinisher(index, _sprites[(int)Shape.Pixel], _finisherFeet + offset * _finisherUnit,
                new Vector2(0.17f - 0.08f * t, 0.075f), index < 3 ? SparkInk : AngerOrange, 1f - t);
        }

        void DrawFinisher(int index, Sprite sprite, Vector3 world, Vector2 size, Color tint, float opacity)
        {
            var piece = _finisherSprites[index];
            piece.enabled = opacity > 0.01f;
            if (!piece.enabled) return;
            piece.sprite = sprite;
            tint.a = opacity;
            piece.color = tint;
            piece.sortingLayerName = _sortingLayerName;
            piece.sortingOrder = _sortingOrder + 1;
            piece.transform.position = world;
            Vector3 parentScale = transform.lossyScale;
            piece.transform.localScale = new Vector3(
                size.x * _finisherUnit / NonZeroScale(parentScale.x),
                size.y * _finisherUnit / NonZeroScale(parentScale.y), 1f);
        }

        void EnsureHitFinisher()
        {
            if (_finisherSprites != null) return;
            if (_rippleSprite == null) _rippleSprite = CreateFinisherShape(false);
            if (_shockSprite == null) _shockSprite = CreateFinisherShape(true);

            _finisherSprites = new SpriteRenderer[FinisherSlots];
            for (int i = 0; i < FinisherSlots; i++)
            {
                var piece = new GameObject($"SpecialHitFinisher_{i}") { layer = gameObject.layer };
                piece.transform.SetParent(transform, false);
                var renderer = piece.AddComponent<SpriteRenderer>();
                if (_particleMaterial != null) renderer.sharedMaterial = _particleMaterial;
                renderer.enabled = false;
                _finisherSprites[i] = renderer;
            }
        }

        static Sprite CreateFinisherShape(bool shock)
        {
            const int cell = 32;
            var pixels = new Color32[cell * cell];
            for (int y = 0; y < cell; y++)
            for (int x = 0; x < cell; x++)
            {
                float nx = Mathf.Abs((x + 0.5f) / 16f - 1f);
                float ny = Mathf.Abs((y + 0.5f) / 16f - 1f);
                float radius = nx * nx + ny * ny;
                if (shock)
                {
                    // 닫힌 물결 대신 갈라진 톱니를 양쪽으로 뻗어 보낸다.
                    float edge = 0.87f - 0.49f * nx - 0.24f * ((x / 2) % 2);
                    if (nx < 0.10f || nx > 0.97f || ny > edge || ny < edge - 0.31f) continue;
                    pixels[y * cell + x] = ny > edge - 0.12f
                        ? new Color32(255, 229, 181, 255) : new Color32(255, 255, 255, 220);
                }
                else if (radius <= 0.96f && radius >= 0.68f)
                {
                    pixels[y * cell + x] = new Color32(255, 255, 255, (byte)(y < cell / 2 ? 255 : 176));
                }
            }

            var texture = new Texture2D(cell, cell, TextureFormat.RGBA32, false)
            {
                name = shock ? "SpecialHit_MoshShock" : "SpecialHit_ChillRipple",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, cell, cell), Vector2.one * 0.5f,
                cell, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static float EaseOut(float t) { float rest = 1f - Mathf.Clamp01(t); return 1f - rest * rest; }
        static float NonZeroScale(float x) => Mathf.Abs(x) > 0.0001f ? x : 1f;
    }
}
