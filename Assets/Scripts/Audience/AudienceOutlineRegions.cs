using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>원본 스프라이트를 유지하면서 외곽선에 사용할 본체만 선택한다.</summary>
    public sealed class AudienceOutlineRegions : ScriptableObject
    {
        public const string ResourcesPath = "AudienceOutlineRegions";

        [Serializable]
        public struct Entry
        {
            public Sprite sprite;
            public Texture2D mask;
            public Rect sourcePixelRect;
            public Rect maskUvRect;
        }

        [Serializable]
        public struct Correction
        {
            public Sprite sprite;
            [Tooltip("슬라이스 왼쪽 아래 기준 픽셀 좌표. 떨어진 본체 부위를 포함한다.")]
            public Vector2Int[] includeSeeds;
            [Tooltip("본체에 붙은 다른 캐릭터 조각 등을 제외한다. 원본 이미지는 변경하지 않는다.")]
            public RectInt[] excludeRects;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private Correction[] corrections = Array.Empty<Correction>();
        Dictionary<Sprite, Entry> _lookup;

        public bool TryGetMask(Sprite sprite, out Entry entry)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<Sprite, Entry>(entries.Length);
                foreach (Entry item in entries)
                    if (item.sprite != null && item.mask != null) _lookup[item.sprite] = item;
            }
            return _lookup.TryGetValue(sprite, out entry);
        }

#if UNITY_EDITOR
        public Correction[] Corrections => corrections;
        public void SetCorrections(Correction[] value) => corrections = value;

        public void SetEntries(Entry[] value)
        {
            entries = value;
            _lookup = null;
        }
#endif
    }
}
