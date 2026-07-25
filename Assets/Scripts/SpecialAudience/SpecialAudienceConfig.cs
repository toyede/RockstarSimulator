using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "SpecialAudienceConfig",
        menuName = "Context Stage/Special Audience Config")]
    public sealed class SpecialAudienceConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] float firstSpawnDelay = 10f;
        [SerializeField, Min(0f)] float spawnInterval = 15f;
        [SerializeField, Min(0.01f)] float requestDuration = 7f;
        [SerializeField, Min(0f)] float specialHitHoldDuration = 0.7f;

        public float FirstSpawnDelay => firstSpawnDelay;
        public float SpawnInterval => spawnInterval;
        public float RequestDuration => requestDuration;
        public float SpecialHitHoldDuration => specialHitHoldDuration;

#if UNITY_EDITOR
        void OnValidate()
        {
            firstSpawnDelay = Mathf.Max(0f, firstSpawnDelay);
            spawnInterval = Mathf.Max(0f, spawnInterval);
            requestDuration = Mathf.Max(0.01f, requestDuration);
            specialHitHoldDuration = Mathf.Max(0f, specialHitHoldDuration);
        }
#endif
    }
}
