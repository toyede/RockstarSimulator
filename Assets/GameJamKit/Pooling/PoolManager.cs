using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 프리팹 단위 오브젝트 풀 관리자. Instantiate/Destroy 를 그대로 대체한다.
    ///
    /// var bullet = PoolManager.Spawn(bulletPrefab, muzzle.position, muzzle.rotation);
    /// PoolManager.Despawn(gameObject);        // Destroy 대신
    /// PoolManager.Despawn(gameObject, 2f);    // 2초 뒤 반납
    /// PoolManager.Prewarm(bulletPrefab, 50);  // 로딩 중 미리 생성
    /// </summary>
    public class PoolManager : MonoSingleton<PoolManager>
    {
        readonly Dictionary<GameObject, ObjectPool> _pools = new Dictionary<GameObject, ObjectPool>();
        readonly Dictionary<GameObject, ObjectPool> _owner = new Dictionary<GameObject, ObjectPool>();

        [SerializeField, Tooltip("풀 하나가 보관할 최대 유휴 인스턴스 수")]
        int maxIdlePerPool = 512;

        Transform _root;

        protected override void OnAwake()
        {
            _root = new GameObject("PooledObjects").transform;
            _root.SetParent(transform, false);
        }

        ObjectPool GetOrCreatePool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool(prefab, _root, 0, maxIdlePerPool);
                _pools.Add(prefab, pool);
            }
            return pool;
        }

        // ---------------- 정적 API ----------------

        public static GameObject Spawn(GameObject prefab) => Spawn(prefab, Vector3.zero, Quaternion.identity, null);
        public static GameObject Spawn(GameObject prefab, Vector3 position) => Spawn(prefab, position, Quaternion.identity, null);
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation) => Spawn(prefab, position, rotation, null);

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolManager] prefab 이 null 입니다.");
                return null;
            }

            var mgr = Instance;
            var pool = mgr.GetOrCreatePool(prefab);
            var instance = pool.Spawn(position, rotation, parent);
            mgr._owner[instance] = pool;
            return instance;
        }

        /// <summary>컴포넌트 프리팹용 제네릭 버전. var b = PoolManager.Spawn(bulletComponent, pos, rot);</summary>
        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefab == null) return null;
            var go = Spawn(prefab.gameObject, position, rotation, parent);
            return go == null ? null : go.GetComponent<T>();
        }

        /// <summary>풀 인스턴스가 아니면 그냥 Destroy 한다. 안전하게 Destroy 대체로 사용 가능.</summary>
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (HasInstance && Instance._owner.TryGetValue(instance, out var pool))
            {
                pool.Despawn(instance);
                return;
            }
            Destroy(instance);
        }

        public static void Despawn(GameObject instance, float delay)
        {
            if (instance == null) return;
            if (delay <= 0f) { Despawn(instance); return; }
            TimerUtils.Delay(delay, () => Despawn(instance));
        }

        public static void Despawn(Component component) => Despawn(component != null ? component.gameObject : null);

        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            Instance.GetOrCreatePool(prefab).Prewarm(count);
        }

        /// <summary>해당 프리팹의 활성 인스턴스를 전부 반납. (웨이브 리셋 등)</summary>
        public static void DespawnAll(GameObject prefab)
        {
            if (HasInstance && prefab != null && Instance._pools.TryGetValue(prefab, out var pool)) pool.DespawnAll();
        }

        /// <summary>모든 풀의 활성 인스턴스 반납. 씬 재시작 시 호출 권장.</summary>
        public static void DespawnAll()
        {
            if (!HasInstance) return;
            foreach (var pool in Instance._pools.Values) pool.DespawnAll();
        }

        /// <summary>풀 자체를 비우고 인스턴스를 파괴한다.</summary>
        public static void ClearAll()
        {
            if (!HasInstance) return;
            foreach (var pool in Instance._pools.Values) pool.Clear();
            Instance._pools.Clear();
            Instance._owner.Clear();
        }
    }
}
