using GameJamKit;

namespace ContextStage
{
    /// <summary>
    /// ESC 로 열리는 일시정지 패널. 킷 UIPopup 상속.
    ///
    /// 동작: PauseInput 이 ESC 를 감지해 Open() 을 호출한다(닫기는 UIManager 가 ESC 로 자동 처리).
    /// pauseGameWhileOpen 이 켜져 있어 Open/Close 시점에 GameManager.Pause()/Resume() 이 자동 호출된다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// - 인스펙터에서 Pause Game While Open 은 켜둘 것, Closable By Escape 도 켜둘 것
    /// </summary>
    public class PausePopup : UIPopup
    {
        public void OnClickResume() => Close();
        public void OnClickRestart() => GameManager.Instance.RestartScene();
        public void OnClickQuit() => UIManager.Instance.QuitGame();
    }
}
