using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>관객 전체의 정렬을 이동 후 한 번 확정한다. 스프라이트 프레임/점프는 깊이에 넣지 않는다.</summary>
    [DefaultExecutionOrder(20000)]
    public sealed class AudienceDepthSorting : MonoBehaviour
    {
        sealed class Entry
        {
            public object Owner;
            public SpriteRenderer Renderer;
            public long Key;
            public Func<float> GroundY;
            public Action<int> Apply;
            public float Y;
        }
        static AudienceDepthSorting _instance;
        readonly List<Entry> _entries = new List<Entry>(40);
        readonly List<Entry> _pending = new List<Entry>(8);
        [SerializeField, Min(0f)] float crossingMargin = .04f;
        const int FirstOrder = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => _instance = null;

        public static void Register(object owner, SpriteRenderer renderer, long stableKey,
            Func<float> groundY, Action<int> apply)
        {
            if (!Application.isPlaying || renderer == null) return;
            if (_instance == null) _instance = new GameObject("[AudienceDepthSorting]").AddComponent<AudienceDepthSorting>();
            Unregister(owner);
            _instance._pending.Add(new Entry { Owner=owner, Renderer=renderer, Key=stableKey, GroundY=groundY, Apply=apply });
        }
        public static void Unregister(object owner)
        {
            if (_instance == null) return;
            for (int i = _instance._entries.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_instance._entries[i].Owner, owner)) _instance._entries.RemoveAt(i);
            for (int i = _instance._pending.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_instance._pending[i].Owner, owner)) _instance._pending.RemoveAt(i);
        }
        static bool Alive(Entry e) => e.Renderer != null && e.Renderer.gameObject.activeInHierarchy;
        void LateUpdate()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (!Alive(_entries[i])) { _entries.RemoveAt(i); continue; }
                _entries[i].Y = _entries[i].GroundY();
            }
            foreach (var entry in _pending)
            {
                if (!Alive(entry)) continue;
                entry.Y = entry.GroundY();
                int index = _entries.Count;
                // 새 관객만 ID로 동률을 결정한다. 기존 관객의 거의 같은 Y 순서는 보존한다.
                while (index > 0)
                {
                    var previous = _entries[index - 1];
                    float difference = entry.Y - previous.Y;
                    if (difference > crossingMargin ||
                        (Mathf.Abs(difference) <= crossingMargin && entry.Key < previous.Key)) index--;
                    else break;
                }
                _entries.Insert(index, entry);
            }
            _pending.Clear();
            // epsilon 비교자를 List.Sort에 넣지 않는다(비추이적 비교가 된다).
            // 충분히 실제로 교차한 쌍만 이동시켜 같은 높이에서 왕복하는 떨림을 막는다.
            for (int i = 1; i < _entries.Count; i++)
            {
                var current = _entries[i]; int index = i;
                while (index > 0 && current.Y > _entries[index - 1].Y + crossingMargin)
                { _entries[index] = _entries[index - 1]; index--; }
                _entries[index] = current;
            }
            for (int i = 0; i < _entries.Count; i++) _entries[i].Apply(FirstOrder + i);
        }
        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            _entries.Clear(); _pending.Clear();
        }
    }
}
