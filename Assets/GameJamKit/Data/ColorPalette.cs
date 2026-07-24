using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 아트에게 받은 hex 코드를 한 곳에 모아두는 팔레트.
    /// 팀원은 색을 직접 찍지 않고 Palette.Get("primary") 또는 PaletteTint 컴포넌트를 쓴다.
    ///
    /// Project 우클릭 → Create/GameJamKit/Color Palette 로 생성 후
    /// Resources 폴더에 "ColorPalette" 이름으로 두면 자동 로드된다.
    /// </summary>
    [CreateAssetMenu(fileName = "ColorPalette", menuName = "GameJamKit/Color Palette")]
    public class ColorPalette : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("코드에서 부를 이름. 예: primary, enemy, ui_bg")]
            public string key;

            [Tooltip("#RRGGBB 또는 #RRGGBBAA. 여기에 붙여넣으면 아래 색이 자동 갱신된다")]
            public string hex = "#FFFFFF";

            public Color color = Color.white;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        Dictionary<string, Color> _lookup;

        public IReadOnlyList<Entry> Entries => entries;

        public Color Get(string key, Color fallback = default)
        {
            if (_lookup == null) BuildLookup();
            if (!string.IsNullOrEmpty(key) && _lookup.TryGetValue(key, out var c)) return c;

            Debug.LogWarning($"[ColorPalette] '{key}' 키가 없습니다.");
            return fallback == default ? Color.magenta : fallback;
        }

        public bool TryGet(string key, out Color color)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(key ?? string.Empty, out color);
        }

        public Color this[string key] => Get(key);

        void BuildLookup()
        {
            _lookup = new Dictionary<string, Color>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || string.IsNullOrEmpty(e.key)) continue;
                _lookup[e.key] = e.color;
            }
        }

        void OnValidate()
        {
            // hex 를 진실의 원천으로 삼아 color 를 동기화한다
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) continue;

                if (!string.IsNullOrEmpty(e.hex))
                {
                    var hex = e.hex.StartsWith("#") ? e.hex : "#" + e.hex;
                    if (ColorUtility.TryParseHtmlString(hex, out var parsed))
                    {
                        e.hex = hex;
                        e.color = parsed;
                        continue;
                    }
                }
                e.hex = "#" + ColorUtility.ToHtmlStringRGBA(e.color);
            }
            _lookup = null;
        }
    }

    /// <summary>Resources/ColorPalette.asset 에 대한 정적 접근자.</summary>
    public static class Palette
    {
        public const string ResourcesName = "ColorPalette";

        static ColorPalette s_asset;

        public static ColorPalette Asset
        {
            get
            {
                if (s_asset == null) s_asset = Resources.Load<ColorPalette>(ResourcesName);
                return s_asset;
            }
        }

        public static Color Get(string key)
        {
            var asset = Asset;
            if (asset == null)
            {
                Debug.LogWarning($"[Palette] Resources/{ResourcesName}.asset 이 없습니다.");
                return Color.magenta;
            }
            return asset.Get(key);
        }

        /// <summary>"#FF3366" → Color. 파싱 실패 시 magenta.</summary>
        public static Color FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static string ToHex(Color color) => "#" + ColorUtility.ToHtmlStringRGBA(color);
    }
}
