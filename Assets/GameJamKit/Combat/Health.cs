using System;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamKit
{
    public struct DamageInfo
    {
        public float Amount;
        public GameObject Source;      // 때린 주체 (없으면 null)
        public Vector2 Direction;      // 넉백/이펙트 방향
        public bool Critical;

        public static DamageInfo Simple(float amount, GameObject source = null)
            => new DamageInfo { Amount = amount, Source = source, Direction = Vector2.zero };
    }

    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(DamageInfo info);
    }

    /// <summary>
    /// 플레이어/적 공용 체력 컴포넌트.
    /// - 무적시간, 사망 처리, 이벤트(코드/인스펙터 양쪽) 제공
    /// - 사망 시 EventBus 로 EntityDied 발행
    /// - 풀링 오브젝트면 Despawn, 아니면 Destroy 로 정리 (선택)
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] float maxHealth = 100f;
        [SerializeField, Tooltip("피격 후 무적 시간(초). 0 이면 무적 없음")] float invincibleDuration = 0f;

        [Header("Death")]
        [SerializeField, Tooltip("사망 시 오브젝트를 자동 정리할지")] bool disableOnDeath = true;
        [SerializeField, Tooltip("사망 이벤트 후 정리까지의 지연(초)")] float deathDelay = 0f;
        [SerializeField, Tooltip("사망 시 스폰할 이펙트 프리팹 (풀링됨)")] GameObject deathEffect;
        [SerializeField, Tooltip("사망 시 재생할 사운드 ID")] string deathSoundId;

        [Header("Hit Feedback")]
        [SerializeField, Tooltip("피격 시 재생할 사운드 ID")] string hitSoundId;
        [SerializeField, Tooltip("피격 시 카메라 흔들림 세기 (0 이면 없음)")] float hitShake = 0f;

        [Header("Unity Events (인스펙터용)")]
        public UnityEvent<float> onHealthChanged01;   // 0~1 비율
        public UnityEvent onDamagedEvent;
        public UnityEvent onDeathEvent;

        float _current;
        float _invincibleUntil;

        /// <summary>코드에서 구독하는 이벤트.</summary>
        public event Action<DamageInfo> OnDamaged;
        public event Action<float> OnHealed;
        public event Action<DamageInfo> OnDeath;
        public event Action OnRevived;

        public float Max => maxHealth;
        public float Current => _current;
        public float Normalized => maxHealth <= 0f ? 0f : Mathf.Clamp01(_current / maxHealth);
        public bool IsDead { get; private set; }
        public bool IsInvincible => Time.time < _invincibleUntil;

        void Awake() => ResetHealth();

        void OnEnable()
        {
            // 풀에서 재사용될 때 초기화
            if (IsDead) ResetHealth();
        }

        public void ResetHealth()
        {
            _current = maxHealth;
            IsDead = false;
            _invincibleUntil = 0f;
            RaiseHealthChanged();
        }

        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = Mathf.Max(1f, value);
            if (refill) _current = maxHealth;
            else _current = Mathf.Min(_current, maxHealth);
            RaiseHealthChanged();
        }

        public void TakeDamage(float amount, GameObject source = null)
            => TakeDamage(new DamageInfo { Amount = amount, Source = source, Direction = Vector2.zero });

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || IsInvincible || info.Amount <= 0f) return;

            _current = Mathf.Max(0f, _current - info.Amount);
            if (invincibleDuration > 0f) _invincibleUntil = Time.time + invincibleDuration;

            if (!string.IsNullOrEmpty(hitSoundId)) Sound.Play(hitSoundId);
            if (hitShake > 0f) CameraShake.Shake(hitShake);

            RaiseHealthChanged();
            OnDamaged?.Invoke(info);
            onDamagedEvent?.Invoke();
            EventBus.Raise(new EntityDamaged
            {
                Entity = gameObject,
                Amount = info.Amount,
                RemainingNormalized = Normalized
            });

            if (_current <= 0f) Die(info);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            _current = Mathf.Min(maxHealth, _current + amount);
            RaiseHealthChanged();
            OnHealed?.Invoke(amount);
        }

        /// <summary>무적을 무시하고 즉사시킨다.</summary>
        public void Kill(GameObject source = null)
        {
            if (IsDead) return;
            _current = 0f;
            RaiseHealthChanged();
            Die(DamageInfo.Simple(0f, source));
        }

        public void Revive(float healthRatio = 1f)
        {
            IsDead = false;
            _current = Mathf.Clamp01(healthRatio) * maxHealth;
            _invincibleUntil = 0f;
            RaiseHealthChanged();
            OnRevived?.Invoke();
        }

        /// <summary>지정 시간 동안 무적 부여 (대시, 리스폰 등).</summary>
        public void GrantInvincibility(float duration) => _invincibleUntil = Mathf.Max(_invincibleUntil, Time.time + duration);

        void Die(DamageInfo info)
        {
            if (IsDead) return;
            IsDead = true;

            if (deathEffect != null) PoolManager.Spawn(deathEffect, transform.position, Quaternion.identity);
            if (!string.IsNullOrEmpty(deathSoundId)) Sound.Play(deathSoundId);

            OnDeath?.Invoke(info);
            onDeathEvent?.Invoke();
            EventBus.Raise(new EntityDied
            {
                Entity = gameObject,
                Killer = info.Source,
                Position = transform.position
            });

            if (disableOnDeath)
            {
                if (deathDelay > 0f) this.DelayCall(deathDelay, () => PoolManager.Despawn(gameObject));
                else PoolManager.Despawn(gameObject);
            }
        }

        void RaiseHealthChanged() => onHealthChanged01?.Invoke(Normalized);
    }
}
