using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    [System.Serializable]
    public class SoundEntry
    {
        [Tooltip("코드에서 부를 이름. 예: \"hit\", \"jump\", \"bgm_main\"")]
        public string id;

        [Tooltip("여러 개를 넣으면 랜덤으로 하나 재생된다 (반복감 제거)")]
        public AudioClip[] clips;

        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 2f)] public float pitchMin = 1f;
        [Range(0f, 2f)] public float pitchMax = 1f;

        [Tooltip("BGM/앰비언트처럼 반복 재생할 사운드")]
        public bool loop = false;

        [Tooltip("같은 사운드가 이 간격 안에 다시 재생되지 않는다 (겹침 방지)")]
        public float minInterval = 0.02f;

        [System.NonSerialized] public float LastPlayTime = -999f;

        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            return clips[Random.Range(0, clips.Length)];
        }

        public float PickPitch() => Mathf.Approximately(pitchMin, pitchMax) ? pitchMin : Random.Range(pitchMin, pitchMax);
    }

    /// <summary>
    /// 사운드 ID → 클립 매핑 테이블.
    /// Project 창 우클릭 → Create/GameJamKit/Sound Library 로 생성하고,
    /// 반드시 Resources 폴더 안에 "SoundLibrary" 라는 이름으로 두면 자동 로드된다.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "GameJamKit/Sound Library")]
    public class SoundLibrary : ScriptableObject
    {
        [SerializeField] List<SoundEntry> sounds = new List<SoundEntry>();

        Dictionary<string, SoundEntry> _lookup;

        public IReadOnlyList<SoundEntry> Sounds => sounds;

        public SoundEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_lookup == null)
            {
                _lookup = new Dictionary<string, SoundEntry>(sounds.Count);
                for (int i = 0; i < sounds.Count; i++)
                {
                    var s = sounds[i];
                    if (s == null || string.IsNullOrEmpty(s.id)) continue;
                    _lookup[s.id] = s;
                }
            }

            return _lookup.TryGetValue(id, out var entry) ? entry : null;
        }

        public void InvalidateCache() => _lookup = null;

        void OnValidate() => _lookup = null;
    }
}
