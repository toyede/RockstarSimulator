using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 스폰된 뒤 일정 시간이 지나면 자동으로 풀에 반납한다.
    /// 총알/이펙트 프리팹에 붙여두면 수명 관리 코드를 따로 짤 필요가 없다.
    /// </summary>
    public class DespawnAfter : MonoBehaviour, IPoolable
    {
        [SerializeField] float lifetime = 2f;
        [SerializeField, Tooltip("ParticleSystem 이 있으면 재생 길이를 수명으로 사용")]
        bool useParticleDuration = false;

        float _timer;

        void OnEnable() => ResetTimer();

        public void OnSpawned() => ResetTimer();
        public void OnDespawned() { }

        void ResetTimer()
        {
            _timer = lifetime;
            if (useParticleDuration)
            {
                var ps = GetComponentInChildren<ParticleSystem>();
                if (ps != null) _timer = ps.main.duration + ps.main.startLifetime.constantMax;
            }
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f) PoolManager.Despawn(gameObject);
        }
    }
}
