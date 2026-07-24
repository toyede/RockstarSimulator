using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Runtime fallback used until the generated preference prefabs are assigned.
    /// Final art should replace the prefab SpriteRenderer, not depend on this class.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrowdPreferencePlaceholderView : MonoBehaviour
    {
        static readonly Dictionary<CrowdPreference, Sprite> s_sprites =
            new Dictionary<CrowdPreference, Sprite>();

        public void Configure(CrowdPreference preference)
        {
            var renderer = GetComponent<SpriteRenderer>();
            renderer.sprite = GetOrCreateSprite(preference);
            renderer.color = ColorFor(preference);

            Transform existingLabel = transform.Find("PreferenceLabel");
            TextMesh text;
            if (existingLabel == null)
            {
                var label = new GameObject("PreferenceLabel");
                label.transform.SetParent(transform, false);
                label.transform.localPosition = new Vector3(0f, 0f, -0.05f);
                text = label.AddComponent<TextMesh>();
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 32;
                text.characterSize = 0.09f;
                text.color = Color.white;

                var labelRenderer = label.GetComponent<MeshRenderer>();
                labelRenderer.sortingLayerName = renderer.sortingLayerName;
                labelRenderer.sortingOrder = renderer.sortingOrder + 10;
            }
            else
            {
                text = existingLabel.GetComponent<TextMesh>();
            }

            if (text != null) text.text = ShortLabel(preference);
        }

        static Sprite GetOrCreateSprite(CrowdPreference preference)
        {
            if (s_sprites.TryGetValue(preference, out Sprite sprite) && sprite != null)
                return sprite;

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"CrowdPlaceholder_{preference}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);
                    bool inside;

                    switch (preference)
                    {
                        case CrowdPreference.Chill:
                            inside = dx * dx + dy * dy <= radius * radius;
                            break;
                        case CrowdPreference.Mosh:
                            inside = dx + dy <= radius;
                            break;
                        default:
                            inside = dx <= radius && dy <= radius;
                            break;
                    }

                    pixels[y * size + x] = inside ? white : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            sprite.name = $"CrowdPlaceholder_{preference}";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            s_sprites[preference] = sprite;
            return sprite;
        }

        static Color ColorFor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return new Color32(49, 223, 234, 255);
                case CrowdPreference.Singalong: return new Color32(100, 49, 234, 255);
                case CrowdPreference.Mosh: return new Color32(240, 31, 31, 255);
                default: return Color.white;
            }
        }

        static string ShortLabel(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return "CHILL";
                case CrowdPreference.Singalong: return "SING";
                case CrowdPreference.Mosh: return "MOSH";
                default: return "?";
            }
        }
    }
}
