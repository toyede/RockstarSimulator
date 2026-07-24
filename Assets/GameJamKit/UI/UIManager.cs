using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameJamKit
{
    /// <summary>
    /// 팝업 등록/스택 관리 + 씬 전환 진입점.
    ///
    /// UIManager.Instance.Open&lt;PausePopup&gt;();
    /// UIManager.Instance.CloseTop();          // ESC 로도 자동 호출
    /// UIManager.Instance.LoadScene("Stage2");
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        protected override bool Persistent => false; // UI 는 씬마다 새로 구성된다

        [SerializeField, Tooltip("ESC 키로 최상단 팝업 닫기")] bool escapeClosesTopPopup = true;

        readonly List<UIPopup> _registered = new List<UIPopup>();
        readonly List<UIPopup> _openStack = new List<UIPopup>();

        public bool AnyPopupOpen => _openStack.Count > 0;
        public UIPopup TopPopup => _openStack.Count > 0 ? _openStack[_openStack.Count - 1] : null;
        public IReadOnlyList<UIPopup> OpenPopups => _openStack;

        // ---------------- 등록 (UIPopup 이 자동 호출) ----------------

        public void Register(UIPopup popup)
        {
            if (popup != null && !_registered.Contains(popup)) _registered.Add(popup);
        }

        public void Unregister(UIPopup popup)
        {
            _registered.Remove(popup);
            _openStack.Remove(popup);
        }

        internal void PushOpen(UIPopup popup)
        {
            _openStack.Remove(popup);
            _openStack.Add(popup);
        }

        internal void PopOpen(UIPopup popup) => _openStack.Remove(popup);

        // ---------------- 팝업 제어 ----------------

        public T Get<T>() where T : UIPopup
        {
            for (int i = 0; i < _registered.Count; i++)
                if (_registered[i] is T typed) return typed;
            return null;
        }

        public T Open<T>() where T : UIPopup
        {
            var popup = Get<T>();
            if (popup == null)
            {
                Debug.LogWarning($"[UIManager] 씬에서 {typeof(T).Name} 을 찾지 못했습니다. " +
                                 "팝업 루트 오브젝트가 활성 상태인지 확인하세요.");
                return null;
            }
            popup.Open();
            return popup;
        }

        public void Close<T>() where T : UIPopup => Get<T>()?.Close();

        public void CloseTop()
        {
            var top = TopPopup;
            if (top != null && top.ClosableByEscape) top.Close();
        }

        public void CloseAll()
        {
            for (int i = _openStack.Count - 1; i >= 0; i--) _openStack[i].Close();
            _openStack.Clear();
        }

        // ---------------- 씬 ----------------

        public void LoadScene(string sceneName) => SceneLoader.Load(sceneName);
        public void ReloadScene() => SceneLoader.Reload();
        public void LoadNextScene() => SceneLoader.LoadNext();
        public void QuitGame() => SceneLoader.Quit();

        /// <summary>
        /// 타이틀의 "시작" 버튼 전용: GameManager 는 DontDestroyOnLoad 라 씬을 넘어가도
        /// 이전 GameOver/Paused 상태가 그대로 남는다. LoadNextScene 만 호출하면 상태가
        /// GameOver 에 갇힌 채로 다음 씬이 열려 카드 입력·ESC 일시정지가 전부 먹통이 되므로,
        /// 상태를 Ready 로 되돌린 뒤 다음 씬을 연다.
        /// </summary>
        public void StartGameNextScene()
        {
            if (GameManager.HasInstance) GameManager.Instance.ResetGame();
            SceneLoader.LoadNext();
        }

        // 버튼 OnClick 에 직접 연결하기 좋은 래퍼들
        public void StartGame() => GameManager.Instance.StartGame();
        public void TogglePause() => GameManager.Instance.TogglePause();
        public void RestartGame() => GameManager.Instance.RestartScene();

        void Update()
        {
            if (!escapeClosesTopPopup || !AnyPopupOpen) return;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseTop();
#else
            if (Input.GetKeyDown(KeyCode.Escape)) CloseTop();
#endif
        }
    }
}
