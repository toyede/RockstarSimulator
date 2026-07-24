using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamKit
{
    /// <summary>
    /// 페이드 + 비동기 로딩이 붙은 씬 전환 헬퍼.
    ///
    /// SceneLoader.Load("Stage1");
    /// SceneLoader.Reload();
    /// SceneLoader.LoadNext();
    /// SceneLoader.OnProgress += p => bar.fillAmount = p;   // 로딩바
    ///
    /// 주의: 로드할 씬은 Build Settings 에 등록되어 있어야 한다.
    /// </summary>
    public static class SceneLoader
    {
        public static bool IsLoading { get; private set; }

        /// <summary>0~1 로딩 진행률.</summary>
        public static event Action<float> OnProgress;
        public static event Action<string> OnSceneLoaded;

        public static void Load(string sceneName, float fadeDuration = 0.3f, Action onLoaded = null)
        {
            if (IsLoading)
            {
                Debug.LogWarning("[SceneLoader] 이미 로딩 중입니다.");
                return;
            }
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneLoader] 씬 이름이 비어 있습니다.");
                return;
            }

            IsLoading = true;
            TimerRunner.Instance.StartCoroutine(LoadRoutine(sceneName, fadeDuration, onLoaded));
        }

        public static void Reload(float fadeDuration = 0.3f, Action onLoaded = null)
            => Load(SceneManager.GetActiveScene().name, fadeDuration, onLoaded);

        /// <summary>Build Settings 상의 다음 씬. 마지막 씬이면 첫 씬으로 순환.</summary>
        public static void LoadNext(float fadeDuration = 0.3f, Action onLoaded = null)
        {
            int next = (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;
            var path = SceneUtility.GetScenePathByBuildIndex(next);
            Load(System.IO.Path.GetFileNameWithoutExtension(path), fadeDuration, onLoaded);
        }

        static IEnumerator LoadRoutine(string sceneName, float fadeDuration, Action onLoaded)
        {
            var fader = ScreenFader.Instance;

            bool faded = false;
            fader.FadeOut(fadeDuration, () => faded = true);
            yield return new WaitUntil(() => faded);

            Time.timeScale = 1f; // 일시정지 상태로 씬을 넘어가는 사고 방지

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] 씬 '{sceneName}' 로드 실패. Build Settings 등록 여부를 확인하세요.");
                IsLoading = false;
                fader.FadeIn(fadeDuration);
                yield break;
            }

            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                OnProgress?.Invoke(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
            OnProgress?.Invoke(1f);

            op.allowSceneActivation = true;
            yield return new WaitUntil(() => op.isDone);

            IsLoading = false;
            OnSceneLoaded?.Invoke(sceneName);
            onLoaded?.Invoke();

            fader.FadeIn(fadeDuration);
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
