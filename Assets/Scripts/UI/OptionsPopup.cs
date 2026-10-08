using GameJamKit;

namespace ContextStage
{
    /// <summary>
    /// 타이틀의 "옵션" 버튼으로 여는 설정 패널. 킷 UIPopup 상속.
    ///
    /// 볼륨 조절은 AudioVolumeSlider 가 슬라이더별로 알아서 처리하므로
    /// 이 클래스는 닫기 버튼 콜백만 있으면 된다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// </summary>
    public class OptionsPopup : UIPopup
    {
        PausePopup _coveredPause;

        protected override void Awake()
        {
            base.Awake();
            OnClosed += RestorePauseMenu;
        }

        protected override void OnOpen()
        {
            _coveredPause = UIManager.Instance.Get<PausePopup>();
            if (_coveredPause != null && _coveredPause.IsOpen) _coveredPause.SetMenuCovered(true);
            PausePopup.RaiseOverlay(this);
        }

        void OnDisable() => RestorePauseMenu();

        void RestorePauseMenu()
        {
            if (_coveredPause != null) _coveredPause.SetMenuCovered(false);
            _coveredPause = null;
        }
        public void OnClickClose() => Close();
    }
}
