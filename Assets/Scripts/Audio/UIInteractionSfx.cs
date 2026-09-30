using GameJamKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>씬 로드와 UI 생성 시 한 번 연결한다. 매 프레임 UI를 검색하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIInteractionSfx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] string clickSoundId = "ui_click";
        [SerializeField] string hoverSoundId = "ui_hover";
        Selectable _selectable;
        Button _button;
        bool _hovered;
        bool _ownsClick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistration()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                    Ensure(selectable);
        }

        public static UIInteractionSfx Ensure(Selectable selectable, bool hover = true, bool click = true)
        {
            if (selectable == null || !Application.isPlaying) return null;
            var sound = selectable.GetComponent<UIInteractionSfx>();
            if (sound == null) sound = selectable.gameObject.AddComponent<UIInteractionSfx>();
            if (!hover) sound.hoverSoundId = string.Empty;
            if (!click) sound.clickSoundId = string.Empty;
            return sound;
        }

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _button = _selectable as Button;
            _ownsClick = _button != null && !HasExistingClickSound(_button);
        }

        void OnEnable()
        {
            if (_ownsClick) _button.onClick.AddListener(PlayClick);
        }

        void OnDisable()
        {
            if (_ownsClick) _button.onClick.RemoveListener(PlayClick);
            _hovered = false;
        }

        void PlayClick()
        {
            // Button이 입력 유효성을 검사한다. 앞선 콜백이 팝업을 닫아도 소리는 재생한다.
            if (!string.IsNullOrEmpty(clickSoundId)) Sound.Play(clickSoundId);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_hovered || _selectable == null || !_selectable.IsActive() || !_selectable.IsInteractable()) return;
            _hovered = true;
            if (!string.IsNullOrEmpty(hoverSoundId)) Sound.Play(hoverSoundId);
        }

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        static bool HasExistingClickSound(Button button)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentListenerState(i) == UnityEngine.Events.UnityEventCallState.Off) continue;
                Object target = button.onClick.GetPersistentTarget(i);
                string method = button.onClick.GetPersistentMethodName(i);
                if (target is AudioManager && method == nameof(AudioManager.PlaySfx)) return true;
                if (target is TitleLeaderboardLauncher && method == nameof(TitleLeaderboardLauncher.OpenLeaderboard)) return true;
                if (target is TitleLeaderboardPopup && method == nameof(TitleLeaderboardPopup.OnClickClose)) return true;
            }
            return false;
        }
    }
}
