using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 씬에 하나 두고 프리팹을 등록해 두면 시작 시 풀을 미리 채워준다.
    /// 게임 시작 직후 첫 발사에서 생기는 순간 렉을 없앤다.
    /// </summary>
    public class PoolPrewarmer : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public GameObject prefab;
            public int count;
        }

        [SerializeField] Entry[] entries;

        void Start()
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
                PoolManager.Prewarm(entries[i].prefab, Mathf.Max(0, entries[i].count));
        }
    }
}
