using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// WaveData 를 읽어 적을 스폰하고, 전멸 여부를 감시해 다음 웨이브로 넘긴다.
    /// 스폰은 전부 PoolManager 를 통하므로 프레임 드랍이 없다.
    ///
    /// 씬에 빈 오브젝트를 만들어 붙이고 waveData + spawnPoints 만 채우면 끝.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] WaveData waveData;

        [Header("Spawn Position")]
        [SerializeField, Tooltip("비워두면 이 오브젝트 주변 원형 범위에 스폰한다")] Transform[] spawnPoints;
        [SerializeField] float spawnRadius = 8f;

        [Header("Flow")]
        [SerializeField, Tooltip("GameManager 가 Playing 이 되면 자동 시작")] bool autoStartOnPlaying = true;
        [SerializeField] float firstWaveDelay = 1f;
        [SerializeField, Tooltip("생존 판정 갱신 주기(초)")] float aliveCheckInterval = 0.25f;

        readonly List<GameObject> _alive = new List<GameObject>();
        Coroutine _routine;
        int _runGeneration;

        public int CurrentWaveIndex { get; private set; } = -1;
        public int AliveCount => _alive.Count;
        public bool IsRunning => _routine != null;
        public WaveData Data { get => waveData; set => waveData = value; }

        public event Action<int> OnWaveStarted;
        public event Action<int> OnWaveCleared;
        public event Action OnAllWavesCleared;

        void OnEnable()
        {
            if (autoStartOnPlaying) EventBus.Subscribe<GameStateChanged>(HandleStateChanged);
        }

        void OnDisable()
        {
            if (autoStartOnPlaying) EventBus.Unsubscribe<GameStateChanged>(HandleStateChanged);
            StopWaves();
        }

        void HandleStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Playing && !IsRunning) StartWaves();
            else if (e.Current == GameState.GameOver || e.Current == GameState.Ready) StopWaves();
        }

        // ---------------- 제어 ----------------

        public void StartWaves()
        {
            if (waveData == null || waveData.Count == 0)
            {
                Debug.LogWarning("[WaveManager] WaveData 가 비어 있습니다.");
                return;
            }
            StopWaves();
            int generation = _runGeneration;
            _routine = StartCoroutine(RunAllWaves(generation));
        }

        public void StopWaves()
        {
            _runGeneration++;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
        }

        /// <summary>진행 중인 웨이브의 적을 전부 회수하고 카운터를 초기화한다.</summary>
        public void ResetWaves()
        {
            StopWaves();
            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] != null) PoolManager.Despawn(_alive[i]);
            _alive.Clear();
            CurrentWaveIndex = -1;
        }

        // ---------------- 진행 ----------------

        IEnumerator RunAllWaves(int generation)
        {
            yield return new WaitForSeconds(firstWaveDelay);
            if (generation != _runGeneration) yield break;

            int index = 0;
            while (true)
            {
                if (generation != _runGeneration) yield break;
                var wave = waveData.Get(index);
                if (wave == null) break;

                CurrentWaveIndex = index;
                OnWaveStarted?.Invoke(index);
                EventBus.Raise(new WaveStarted { Index = index, Name = wave.name });

                yield return StartCoroutine(RunWave(wave, generation));
                if (generation != _runGeneration) yield break;

                if (wave.waitUntilCleared)
                    yield return new WaitUntil(() => { PruneDead(); return _alive.Count == 0; });

                bool isLast = index >= waveData.Count - 1;
                OnWaveCleared?.Invoke(index);
                EventBus.Raise(new WaveCleared { Index = index, WasLast = isLast && !waveData.loopLastWave });

                if (wave.delayAfterClear > 0f) yield return new WaitForSeconds(wave.delayAfterClear);

                if (isLast)
                {
                    if (!waveData.loopLastWave) break;
                    index = 0; // 반복
                }
                else index++;
            }

            _routine = null;
            OnAllWavesCleared?.Invoke();
        }

        IEnumerator RunWave(Wave wave, int generation)
        {
            int running = 0;

            for (int i = 0; i < wave.spawns.Count; i++)
            {
                var entry = wave.spawns[i];
                if (entry == null || entry.prefab == null) continue;

                running++;
                StartCoroutine(SpawnGroup(entry, generation, () => running--));
            }

            yield return new WaitUntil(() => generation != _runGeneration || running <= 0);
        }

        IEnumerator SpawnGroup(SpawnEntry entry, int generation, Action onComplete)
        {
            if (entry.startDelay > 0f) yield return new WaitForSeconds(entry.startDelay);
            if (generation != _runGeneration) yield break;

            for (int i = 0; i < entry.count; i++)
            {
                if (generation != _runGeneration) yield break;
                SpawnOne(entry.prefab);
                if (entry.interval > 0f && i < entry.count - 1) yield return new WaitForSeconds(entry.interval);
            }
            onComplete?.Invoke();
        }

        public GameObject SpawnOne(GameObject prefab)
        {
            var instance = PoolManager.Spawn(prefab, GetSpawnPosition(), Quaternion.identity);
            if (instance != null) _alive.Add(instance);
            return instance;
        }

        public Vector3 GetSpawnPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                if (point != null) return point.position;
            }

            var dir = UnityEngine.Random.insideUnitCircle.normalized;
            return transform.position + new Vector3(dir.x, dir.y, 0f) * spawnRadius;
        }

        void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                var go = _alive[i];
                if (go == null || !go.activeInHierarchy) { _alive.RemoveAt(i); continue; }

                // 풀링된 적은 파괴되지 않으므로 Health 로도 확인한다
                var health = go.GetComponent<Health>();
                if (health != null && health.IsDead) _alive.RemoveAt(i);
            }
        }

        float _nextPrune;

        void Update()
        {
            if (Time.time < _nextPrune) return;
            _nextPrune = Time.time + aliveCheckInterval;
            PruneDead();
        }

        void OnDrawGizmosSelected()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Gizmos.color = Color.yellow;
                foreach (var p in spawnPoints)
                    if (p != null) Gizmos.DrawWireSphere(p.position, 0.4f);
            }
            else
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, spawnRadius);
            }
        }
    }
}
