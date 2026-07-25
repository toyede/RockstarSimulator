using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    public enum GameState
    {
        Ready,      // 시작 대기 (타이틀/카운트다운)
        Playing,    // 진행 중
        Paused,     // 일시정지 (timeScale = 0)
        GameOver    // 종료
    }

    /// <summary>
    /// 게임 상태머신 + 점수. 상태 변경 시 C# 이벤트와 EventBus 양쪽으로 알린다.
    ///
    /// GameManager.Instance.StartGame();
    /// GameManager.Instance.TogglePause();
    /// GameManager.Instance.GameOver();
    /// GameManager.Instance.AddScore(100);
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        [Header("Score")]
        [SerializeField] string highScoreKey = "HighScore";

        [Header("Options")]
        [Tooltip("Ready 상태에서 게임 시작 전에도 시간이 흐를지")]
        [SerializeField] bool freezeTimeOnReady = false;

        readonly HashSet<object> _pauseOwners = new HashSet<object>();

        public GameState State { get; private set; } = GameState.Ready;
        public GameState PreviousState { get; private set; } = GameState.Ready;

        public int Score { get; private set; }
        public int HighScore => Save.GetInt(highScoreKey, 0);
        public bool IsPlaying => State == GameState.Playing;

        /// <summary>(이전 상태, 새 상태)</summary>
        public event Action<GameState, GameState> OnStateChanged;
        public event Action<int> OnScoreChanged;

        protected override void OnAwake()
        {
            ApplyTimeScale(State);
        }

        // ---------------- 상태 전이 ----------------

        public void SetState(GameState next)
        {
            if (State == next) return;
            if (!CanTransition(State, next))
            {
                Debug.LogWarning($"[GameManager] 허용되지 않는 전이: {State} → {next}");
                return;
            }

            PreviousState = State;
            State = next;
            ApplyTimeScale(next);

            OnStateChanged?.Invoke(PreviousState, State);
            EventBus.Raise(new GameStateChanged { Previous = PreviousState, Current = State });
        }

        static bool CanTransition(GameState from, GameState to)
        {
            if (to == GameState.Ready) return true; // 재시작은 언제나 허용
            switch (from)
            {
                case GameState.Ready:    return to == GameState.Playing;
                case GameState.Playing:  return to == GameState.Paused || to == GameState.GameOver;
                case GameState.Paused:   return to == GameState.Playing || to == GameState.GameOver;
                case GameState.GameOver: return to == GameState.Playing;
            }
            return false;
        }

        void ApplyTimeScale(GameState state)
        {
            switch (state)
            {
                case GameState.Paused:   Time.timeScale = 0f; break;
                case GameState.Ready:    Time.timeScale = freezeTimeOnReady ? 0f : 1f; break;
                default:                 Time.timeScale = 1f; break;
            }
        }

        // ---------------- 편의 API ----------------

        public void StartGame()
        {
            if (State == GameState.Paused)
            {
                Resume();
                return;
            }

            if (State == GameState.GameOver) ResetGame();
            SetState(GameState.Playing);
        }

        public void Pause() => AcquirePause(this);

        public void Resume()
        {
            _pauseOwners.Remove(this);
            ResumeWhenNoPauseOwners();
        }

        public void AcquirePause(object owner)
        {
            if (owner == null || !_pauseOwners.Add(owner)) return;
            if (State == GameState.Playing) SetState(GameState.Paused);
        }

        public void ReleasePause(object owner)
        {
            if (owner == null || !_pauseOwners.Remove(owner)) return;
            ResumeWhenNoPauseOwners();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }

        public void GameOver()
        {
            if (State == GameState.GameOver) return;
            if (State != GameState.Playing && State != GameState.Paused)
            {
                Debug.LogWarning($"[GameManager] {State} 상태에서는 GameOver로 전이할 수 없습니다.");
                return;
            }

            SubmitHighScore();
            SetState(GameState.GameOver);
        }

        /// <summary>점수 초기화 + Ready 상태로. 씬을 다시 로드하지는 않는다.</summary>
        public void ResetGame()
        {
            _pauseOwners.Clear();
            SetScore(0);
            SetState(GameState.Ready);
        }

        /// <summary>현재 씬을 페이드와 함께 재시작.</summary>
        public void RestartScene(float fadeDuration = 0.3f)
        {
            ResetGame();
            SceneLoader.Reload(fadeDuration);
        }

        // ---------------- 점수 ----------------

        public void AddScore(int delta)
        {
            if (delta == 0) return;
            Score += delta;
            OnScoreChanged?.Invoke(Score);
            EventBus.Raise(new ScoreChanged { Score = Score, Delta = delta });
        }

        public void SetScore(int value)
        {
            int delta = value - Score;
            Score = value;
            OnScoreChanged?.Invoke(Score);
            EventBus.Raise(new ScoreChanged { Score = Score, Delta = delta });
        }

        /// <summary>하이스코어 갱신 시 true.</summary>
        public bool SubmitHighScore()
        {
            if (Score <= HighScore) return false;
            Save.SetInt(highScoreKey, Score);
            return true;
        }

        void ResumeWhenNoPauseOwners()
        {
            if (_pauseOwners.Count == 0 && State == GameState.Paused)
                SetState(GameState.Playing);
        }
    }
}
