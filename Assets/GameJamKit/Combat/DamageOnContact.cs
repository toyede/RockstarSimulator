using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 2D 트리거/충돌로 닿은 대상에게 데미지를 주는 컴포넌트.
    /// 총알, 적의 몸통, 가시 함정 등에 그대로 붙여 쓴다.
    /// Collider2D 가 필요하며 (isTrigger 권장), 총알이면 Rigidbody2D(Kinematic)도 함께 붙일 것.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DamageOnContact : MonoBehaviour
    {
        [SerializeField] float damage = 10f;
        [SerializeField] LayerMask targetLayers = ~0;

        [Header("Behaviour")]
        [SerializeField, Tooltip("한 번 맞히면 스스로 사라진다 (총알)")] bool despawnAfterHit = true;
        [SerializeField, Tooltip("같은 대상에게 다시 데미지를 주기까지의 간격(초). 0 이면 매 프레임")] float repeatInterval = 0.5f;
        [SerializeField, Tooltip("맞힐 때 스폰할 이펙트 (풀링됨)")] GameObject hitEffect;
        [SerializeField, Tooltip("맞힐 때 재생할 사운드 ID")] string hitSoundId;
        [SerializeField, Tooltip("맞힐 때 카메라 흔들림 세기")] float shake = 0f;
        [SerializeField, Tooltip("맞힐 때 히트스톱 시간(초). 0 이면 없음")] float hitStop = 0f;

        float _nextHitTime;

        public float Damage { get => damage; set => damage = value; }

        void OnTriggerEnter2D(Collider2D other) => TryHit(other, other.ClosestPoint(transform.position));
        void OnTriggerStay2D(Collider2D other)
        {
            if (repeatInterval > 0f) TryHit(other, other.ClosestPoint(transform.position));
        }
        void OnCollisionEnter2D(Collision2D collision)
        {
            Vector2 point = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2)transform.position;
            TryHit(collision.collider, point);
        }

        void TryHit(Collider2D other, Vector2 point)
        {
            if (other == null) return;
            if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
            if (Time.time < _nextHitTime) return;

            var target = other.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead) return;

            target.TakeDamage(new DamageInfo
            {
                Amount = damage,
                Source = gameObject,
                Direction = ((Vector2)(other.transform.position - transform.position)).normalized
            });

            _nextHitTime = Time.time + repeatInterval;

            if (hitEffect != null) PoolManager.Spawn(hitEffect, point, Quaternion.identity);
            if (!string.IsNullOrEmpty(hitSoundId)) Sound.Play(hitSoundId);
            if (shake > 0f) CameraShake.Shake(shake);
            if (hitStop > 0f) HitStop.Do(hitStop);

            if (despawnAfterHit) PoolManager.Despawn(gameObject);
        }
    }
}
