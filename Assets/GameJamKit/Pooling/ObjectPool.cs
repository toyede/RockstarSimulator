using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 풀에서 꺼내지거나 반납될 때 알림을 받고 싶은 컴포넌트가 구현한다.
    /// (총알 속도 초기화, 파티클 재생 등)
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>단일 프리팹에 대한 인스턴스 풀. 보통 PoolManager 를 통해 간접적으로 사용한다.</summary>
    public class ObjectPool
    {
        readonly GameObject _prefab;
        readonly Transform _root;
        readonly Stack<GameObject> _idle = new Stack<GameObject>();
        readonly HashSet<GameObject> _active = new HashSet<GameObject>();
        readonly Dictionary<GameObject, IPoolable[]> _callbacks = new Dictionary<GameObject, IPoolable[]>();
        readonly int _maxSize;

        public GameObject Prefab => _prefab;
        public int IdleCount => _idle.Count;
        public int ActiveCount => _active.Count;
        public event System.Action<GameObject> InstanceDiscarded;

        public ObjectPool(GameObject prefab, Transform root, int prewarm = 0, int maxSize = 512)
        {
            _prefab = prefab;
            _root = root;
            _maxSize = maxSize;
            Prewarm(prewarm);
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = CreateInstance();
                go.SetActive(false);
                _idle.Push(go);
            }
        }

        GameObject CreateInstance()
        {
            var go = Object.Instantiate(_prefab, _root);
            go.name = _prefab.name;
            _callbacks[go] = go.GetComponentsInChildren<IPoolable>(true);
            return go;
        }

        public GameObject Spawn(Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject go = null;
            while (_idle.Count > 0 && go == null)
            {
                GameObject candidate = _idle.Pop();
                if (candidate == null)
                {
                    ForgetInstance(candidate);
                    continue;
                }

                go = candidate;
            }
            if (go == null) go = CreateInstance();

            var t = go.transform;
            t.SetParent(parent != null ? parent : _root, false);
            t.SetPositionAndRotation(position, rotation);
            t.localScale = _prefab.transform.localScale;

            go.SetActive(true);
            _active.Add(go);

            IPoolable[] poolables = GetCallbacks(go);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawned();

            return go;
        }

        public void Despawn(GameObject go)
        {
            if (ReferenceEquals(go, null)) return;
            if (go == null)
            {
                _active.Remove(go);
                ForgetInstance(go);
                return;
            }
            if (!_active.Remove(go)) return; // 이미 반납됨 = 중복 Despawn 무시

            IPoolable[] poolables = GetCallbacks(go);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnDespawned();

            go.SetActive(false);
            go.transform.SetParent(_root, false);

            if (_idle.Count >= _maxSize) DiscardInstance(go);
            else _idle.Push(go);
        }

        /// <summary>활성 인스턴스를 전부 반납한다.</summary>
        public void DespawnAll()
        {
            if (_active.Count == 0) return;
            var buffer = new List<GameObject>(_active);
            for (int i = 0; i < buffer.Count; i++) Despawn(buffer[i]);
        }

        public void Clear()
        {
            DespawnAll();
            while (_idle.Count > 0)
            {
                var go = _idle.Pop();
                if (go != null) DiscardInstance(go);
                else ForgetInstance(go);
            }
        }

        IPoolable[] GetCallbacks(GameObject go)
        {
            if (!_callbacks.TryGetValue(go, out IPoolable[] callbacks))
            {
                callbacks = go.GetComponentsInChildren<IPoolable>(true);
                _callbacks[go] = callbacks;
            }

            return callbacks;
        }

        void DiscardInstance(GameObject go)
        {
            ForgetInstance(go);
            if (go != null) Object.Destroy(go);
        }

        void ForgetInstance(GameObject go)
        {
            if (ReferenceEquals(go, null)) return;
            _callbacks.Remove(go);
            InstanceDiscarded?.Invoke(go);
        }
    }
}
