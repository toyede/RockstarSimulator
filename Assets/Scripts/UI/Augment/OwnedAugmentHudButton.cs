using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 공연 화면의 보유 증강 버튼. 누르면 지도와 같은 보유 증강 창을 띄운다.
    /// 창이 열려 있어도 게임은 일시정지하지 않는다. ESC 는 창만 닫는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OwnedAugmentHudButton : MonoBehaviour
    {
        [SerializeField, Tooltip("보유 증강 창을 여는 버튼. 위치는 이 오브젝트에서 조정한다")]
        Button openButton;

        static OwnedAugmentHudButton s_active;
        AugmentSelectionPopup _popup;
        AugmentCatalog _catalog;

        /// <summary>보유 증강 창이 열려 있는지. 열려 있는 동안 ESC 로 일시정지 팝업을 열지 않는다.</summary>
        public static bool IsModalOpen =>
            s_active != null && s_active._popup != null && s_active._popup.IsOwnedModalOpen;

        void Awake()
        {
            // 지도와 같은 프리팹을 숨긴 채로 만들고, 보유 증강 창만 이 캔버스에 올려 쓴다.
            _popup = PrototypeAugmentSelectionUIFactory.Create(transform);
            if (_popup != null) _popup.SetOwnedModalHost(transform);
            if (openButton != null) UIInteractionSfx.Ensure(openButton);
        }

        void OnEnable()
        {
            s_active = this;
            if (openButton != null) openButton.onClick.AddListener(Open);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            Close();
            if (s_active == this) s_active = null;
        }

        void Open()
        {
            if (_popup == null) return;
            if (_catalog == null) _catalog = AugmentCatalog.LoadDefault();
            // 투어 밖(Main 직접 실행)에서는 런이 없어 빈 목록이 표시된다.
            TourRunState run = TourRunManager.HasInstance ? TourRunManager.Instance.CurrentRun : null;
            _popup.ShowOwnedModal(AugmentOwnedViewModelBuilder.Build(run, _catalog));
        }

        void Close()
        {
            if (_popup != null && _popup.IsOwnedModalOpen) _popup.HideOwnedModal();
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 공연이 끝나거나 다시 시작되면 결과 화면 위에 창이 남지 않게 닫는다.
            if (e.Current == GameState.GameOver || e.Current == GameState.Ready) Close();
        }

        // PauseInput.Update 가 끝난 뒤에 닫아야 같은 ESC 로 일시정지 팝업이 열리지 않는다.
        void LateUpdate()
        {
            if (!IsModalOpen || !EscapePressed()) return;
            Close();
        }

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
