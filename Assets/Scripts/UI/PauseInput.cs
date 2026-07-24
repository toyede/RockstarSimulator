using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// ESC 로 일시정지 패널을 열고 닫는 트리거. 열기/닫기를 이 스크립트가 전담한다.
    ///
    ///   ESC (Playing 중, 팝업 없음) : PausePopup 열기 → pauseGameWhileOpen 이 자동으로 Pause()
    ///   ESC (Paused 중)             : PausePopup 닫기 → pauseGameWhileOpen 이 자동으로 Resume()
    ///
    /// 주의: PausePopup 은 씬 인스펙터에서 closableByEscape 를 꺼둬야 한다.
    /// 켜져 있으면 킷의 UIManager.Update() 가 "같은 프레임"에 같은 ESC 입력으로 CloseTop() 을
    /// 먼저 실행해버릴 수 있고, 그 직후 이 스크립트가 (이미 Playing 으로 바뀐) State 를 보고
    /// 다시 Open() 을 호출해 즉시 재오픈되는 경합(같은 ESC 한 번에 닫혔다 열리는 버그)이 생긴다.
    /// 그래서 "닫기" 권한은 UIManager 의 자동 ESC 처리에 맡기지 않고 여기서만 갖는다.
    /// </summary>
    public class PauseInput : MonoBehaviour
    {
        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            bool escPressed;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            escPressed = kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (!escPressed) return;

            if (gm.State == GameState.Paused)
            {
                UIManager.Instance.Close<PausePopup>();
            }
            else if (gm.State == GameState.Playing && !UIManager.Instance.AnyPopupOpen)
            {
                UIManager.Instance.Open<PausePopup>();
            }
        }
    }
}
