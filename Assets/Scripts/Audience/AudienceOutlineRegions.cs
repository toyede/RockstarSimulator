using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>취향 간파에서 분리된 그림자·잔여 표시를 제외할 스프라이트 영역.</summary>
    public sealed class AudienceOutlineRegions : ScriptableObject
    {
        public const string ResourcesPath = "AudienceOutlineRegions";

        [Serializable]
        public struct Entry
        {
            public Sprite sprite;
            public float minimumPixelY;
            public float maximumPixelY;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        Dictionary<Sprite, Vector2> _verticalBounds;

        public Rect GetTextureRect(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            if (_verticalBounds == null)
            {
                _verticalBounds = new Dictionary<Sprite, Vector2>(entries.Length);
                foreach (Entry entry in entries)
                    if (entry.sprite != null)
                        _verticalBounds[entry.sprite] = new Vector2(entry.minimumPixelY, entry.maximumPixelY);
            }

            if (_verticalBounds.TryGetValue(sprite, out Vector2 bounds))
            {
                // 슬라이스 내부 좌표를 현재 텍스처의 좌표로 변환한다.
                float originY = rect.yMin - sprite.textureRectOffset.y;
                rect.yMin = Mathf.Max(rect.yMin, originY + bounds.x);
                rect.yMax = Mathf.Min(rect.yMax, originY + bounds.y);
            }
            return rect;
        }

#if UNITY_EDITOR
        public void SetEntries(Entry[] value)
        {
            entries = value;
            _verticalBounds = null;
        }
#endif
    }
}
