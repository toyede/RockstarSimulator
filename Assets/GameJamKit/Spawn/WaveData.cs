using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>웨이브 안에서 "무엇을 몇 마리 어떤 간격으로" 스폰할지.</summary>
    [System.Serializable]
    public class SpawnEntry
    {
        public GameObject prefab;
        [Min(1)] public int count = 5;
        [Tooltip("한 마리씩 스폰되는 간격(초)")] public float interval = 0.5f;
        [Tooltip("웨이브 시작 후 이 그룹이 나오기까지의 지연(초)")] public float startDelay = 0f;
    }

    [System.Serializable]
    public class Wave
    {
        public string name = "Wave";
        public List<SpawnEntry> spawns = new List<SpawnEntry>();

        [Tooltip("true 면 모두 처치해야 다음 웨이브로 넘어간다. false 면 스폰이 끝나는 즉시 진행")]
        public bool waitUntilCleared = true;

        [Tooltip("웨이브 클리어 후 다음 웨이브까지의 대기(초)")]
        public float delayAfterClear = 2f;

        public int TotalCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < spawns.Count; i++) total += spawns[i] != null ? spawns[i].count : 0;
                return total;
            }
        }
    }

    /// <summary>
    /// 웨이브 정의 테이블. Create/GameJamKit/Wave Data
    /// 기획자가 인스펙터에서 난이도 곡선을 직접 만질 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveData", menuName = "GameJamKit/Wave Data")]
    public class WaveData : ScriptableObject
    {
        public List<Wave> waves = new List<Wave>();

        [Tooltip("마지막 웨이브 이후 처음으로 돌아가 무한 반복")]
        public bool loopLastWave = false;

        public int Count => waves.Count;
        public Wave Get(int index) => index >= 0 && index < waves.Count ? waves[index] : null;
    }
}
