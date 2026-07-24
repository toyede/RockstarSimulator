using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 킷이 기본으로 발행하는 이벤트들. 프로젝트 고유 이벤트는 이 파일에 계속 추가해서 쓰면 된다.
    /// struct 로 만드는 이유: GC 할당 없음.
    /// </summary>
    public struct GameStateChanged
    {
        public GameState Previous;
        public GameState Current;
    }

    public struct ScoreChanged
    {
        public int Score;
        public int Delta;
    }

    /// <summary>Health 컴포넌트가 사망 시 자동 발행.</summary>
    public struct EntityDied
    {
        public GameObject Entity;
        public GameObject Killer;
        public Vector3 Position;
    }

    /// <summary>Health 컴포넌트가 피격 시 자동 발행.</summary>
    public struct EntityDamaged
    {
        public GameObject Entity;
        public float Amount;
        public float RemainingNormalized;
    }

    public struct WaveStarted
    {
        public int Index;
        public string Name;
    }

    public struct WaveCleared
    {
        public int Index;
        public bool WasLast;
    }
}
