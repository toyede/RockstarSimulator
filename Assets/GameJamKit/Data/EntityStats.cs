using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 적/플레이어 스탯을 코드에서 분리하는 데이터 템플릿.
    /// 밸런싱을 프로그래머 없이 인스펙터에서 할 수 있게 된다.
    /// 필요한 필드는 팀 상황에 맞게 자유롭게 추가/삭제할 것.
    ///
    /// Create/GameJamKit/Entity Stats
    /// </summary>
    [CreateAssetMenu(fileName = "NewEntityStats", menuName = "GameJamKit/Entity Stats")]
    public class EntityStats : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Enemy";
        public Sprite icon;

        [Header("Combat")]
        public float maxHealth = 100f;
        public float damage = 10f;
        public float attackInterval = 1f;
        public float attackRange = 1.5f;

        [Header("Movement")]
        public float moveSpeed = 3f;
        public float acceleration = 20f;

        [Header("Reward")]
        public int scoreValue = 10;
        [Range(0f, 1f)] public float dropChance = 0.1f;
        public GameObject dropPrefab;

        [Header("Visual")]
        public GameObject prefab;
        [Tooltip("ColorPalette 의 키. 비우면 사용하지 않음")] public string paletteKey;

        /// <summary>Health 컴포넌트에 이 스탯을 적용.</summary>
        public void ApplyTo(Health health, bool refill = true)
        {
            if (health != null) health.SetMaxHealth(maxHealth, refill);
        }
    }
}
