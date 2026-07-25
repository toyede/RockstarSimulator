using System;
using System.Collections;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 팝업 베이스 클래스. 상속해서 버튼 콜백만 채우면 된다.
    ///
    /// public class PausePopup : UIPopup { public void OnResume() => Close(); }
    ///
    /// 씬 배치 규칙: 팝업 루트 오브젝트는 **활성 상태로** 두어야 자동 등록된다.
    /// (startHidden 이 true 면 Start 에서 알아서 숨긴다)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPopup : MonoBehaviour
    {
        [Header("Popup")]
        [SerializeField, Tooltip("시작할 때 자동으로 닫힌 상태로 만든다")] bool startHidden = true;
        [SerializeField, Tooltip("열려 있는 동안 게임을 일시정지")] bool pauseGameWhileOpen = false;
        [SerializeField, Tooltip("ESC 로 닫을 수 있는 팝업인지")] bool closableByEscape = true;
        [SerializeField] float animDuration = 0.15f;
        [SerializeField, Tooltip("열릴 때 살짝 커지는 연출")] bool scaleAnimation = true;
        [SerializeField] string openSoundId = "";
        [SerializeField] string closeSoundId = "";

        CanvasGroup _group;
        Transform _content;
        Coroutine _routine;
        bool _ownsPause;

        public bool IsOpen { get; private set; }
        public bool ClosableByEscape => closableByEscape;

        public event Action OnOpened;
        public event Action OnClosed;

        protected virtual void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _content = transform.childCount > 0 ? transform.GetChild(0) : transform;
            UIManager.Instance.Register(this);
        }

        protected virtual void Start()
        {
            if (startHidden) CloseImmediate();
        }

        protected virtual void OnDestroy()
        {
            ReleasePauseOwnership();
            if (UIManager.HasInstance) UIManager.Instance.Unregister(this);
        }

        // ---------------- 열기/닫기 ----------------

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(openSoundId)) Sound.Play(openSoundId);
            if (pauseGameWhileOpen && GameManager.HasInstance)
            {
                GameManager.Instance.AcquirePause(this);
                _ownsPause = true;
            }

            UIManager.Instance.PushOpen(this);
            OnOpen();

            Animate(0f, 1f, () => OnOpened?.Invoke());
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            if (!string.IsNullOrEmpty(closeSoundId)) Sound.Play(closeSoundId);
            ReleasePauseOwnership();

            UIManager.Instance.PopOpen(this);
            OnClose();

            Animate(1f, 0f, () =>
            {
                gameObject.SetActive(false);
                OnClosed?.Invoke();
            });
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void CloseImmediate()
        {
            bool wasOpen = IsOpen;
            IsOpen = false;
            if (wasOpen) ReleasePauseOwnership();
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            ApplyAlpha(0f);
            if (UIManager.HasInstance) UIManager.Instance.PopOpen(this);
            gameObject.SetActive(false);
        }

        /// <summary>파생 클래스에서 열릴 때 데이터 갱신 등을 처리.</summary>
        protected virtual void OnOpen() { }
        protected virtual void OnClose() { }

        void ReleasePauseOwnership()
        {
            if (!_ownsPause) return;
            _ownsPause = false;
            if (GameManager.HasInstance) GameManager.Instance.ReleasePause(this);
        }

        // ---------------- 애니메이션 ----------------

        void Animate(float from, float to, Action onComplete)
        {
            if (_routine != null) StopCoroutine(_routine);

            if (animDuration <= 0f)
            {
                ApplyAlpha(to);
                onComplete?.Invoke();
                return;
            }
            _routine = StartCoroutine(AnimRoutine(from, to, onComplete));
        }

        IEnumerator AnimRoutine(float from, float to, Action onComplete)
        {
            float t = 0f;
            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime; // 일시정지 중에도 동작
                float k = Mathf.Clamp01(t / animDuration);
                ApplyAlpha(Mathf.Lerp(from, to, k));
                yield return null;
            }
            ApplyAlpha(to);
            _routine = null;
            onComplete?.Invoke();
        }

        void ApplyAlpha(float alpha)
        {
            _group.alpha = alpha;
            _group.interactable = alpha > 0.99f;
            _group.blocksRaycasts = alpha > 0.01f;

            if (scaleAnimation && _content != null)
                _content.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, alpha);
        }
    }
}
