using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// ESC 로 열리는 일시정지 패널. 킷 UIPopup 상속.
    ///
    /// 동작: PauseInput 이 ESC 열기/닫기를 전담한다.
    /// pauseGameWhileOpen 이 켜져 있어 Open/Close 시점에 GameManager.Pause()/Resume() 이 자동 호출된다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// - Pause Game While Open 은 켜고, Closable By Escape 는 끈다 (이중 처리 방지).
    /// </summary>
    public class PausePopup : UIPopup
    {
        CanvasGroup _menuGroup;

        protected override void OnOpen()
        {
            // 새 일시정지 메뉴 뒤에 이전 옵션창이 남지 않게 한다.
            UIManager.Instance.Get<OptionsPopup>()?.CloseImmediate();
            SetMenuCovered(false);
            RaiseOverlay(this);
        }

        internal void SetMenuCovered(bool covered)
        {
            if (_menuGroup == null)
            {
                Transform content = transform.Find("Buttons") ?? transform.Find("Content");
                if (content == null) return;
                _menuGroup = content.GetComponent<CanvasGroup>();
                if (_menuGroup == null) _menuGroup = content.gameObject.AddComponent<CanvasGroup>();
            }
            // 루트는 유지: 일시정지 소유권과 블러는 계속 살아 있어야 한다.
            _menuGroup.alpha = covered ? 0f : 1f;
            _menuGroup.interactable = !covered;
            _menuGroup.blocksRaycasts = !covered;
        }

        // 활성 HUD보다 위에 배치한다. 매번 다른 캔버스만 비교해 반복 Open의 누적을 막는다.
        internal static void RaiseOverlay(UIPopup popup)
        {
            Canvas canvas = popup.GetComponent<Canvas>();
            if (canvas == null) canvas = popup.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            int order = 0;
            foreach (Canvas other in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (other == canvas || other.transform.IsChildOf(popup.transform)) continue;
                if (other.isActiveAndEnabled) order = Mathf.Max(order, other.sortingOrder);
            }
            canvas.sortingOrder = Mathf.Min(30000, order + 10);
            if (popup.GetComponent<GraphicRaycaster>() == null)
                popup.gameObject.AddComponent<GraphicRaycaster>();
            Image blocker = popup.GetComponent<Image>();
            if (blocker == null)
            {
                blocker = popup.gameObject.AddComponent<Image>();
                blocker.color = Color.clear;
            }
            blocker.raycastTarget = true;
            foreach (UIBlurBackdrop blur in popup.GetComponentsInChildren<UIBlurBackdrop>(true))
                blur.RefreshSorting();
        }

        public void OnClickResume() => Close();
        public void OnClickRestart() => GameManager.Instance.RestartScene();
        public void OnClickQuit() => UIManager.Instance.QuitGame();
    }
}
