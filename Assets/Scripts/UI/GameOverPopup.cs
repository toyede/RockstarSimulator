using GameJamKit;

namespace ContextStage
{
    /// <summary>
    /// 게임오버(호응도 0) 시 화면 중앙에 뜨는 팝업. 킷 UIPopup 상속.
    ///
    /// 동작: GameStateChanged 이벤트를 구독하다가 GameOver 상태가 되면 스스로 Open().
    /// 재시작은 HypeDebugInput 의 R 키(GameManager.RestartScene) 가 처리하고,
    /// 버튼을 달고 싶으면 OnClickRestart 를 버튼 OnClick 에 연결하면 된다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// - 인스펙터에서 Closable By Escape 는 꺼둘 것 (게임오버를 ESC로 닫으면 안 됨)
    /// </summary>
    public class GameOverPopup : UIPopup
    {
        protected override void Awake()
        {
            base.Awake(); // 킷 규칙: UIPopup.Awake 를 반드시 호출해야 UIManager 에 등록된다

            // 주의: 닫힌 팝업은 SetActive(false) 상태라서 OnEnable/OnDisable 로 구독하면
            // 닫혀 있는 동안 이벤트를 놓친다. 그래서 Awake/OnDestroy 에서 구독한다.
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            base.OnDestroy(); // 킷 규칙: UIManager 등록 해제
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 호응도 0 → HypeSystem 이 GameManager.GameOver() 호출 → 여기로 통지가 온다
            if (e.Current == GameState.GameOver) Open();
        }

        /// <summary>[선택] 재시작 버튼을 만들면 OnClick 에 이 함수를 연결한다.</summary>
        public void OnClickRestart() => GameManager.Instance.RestartScene();
    }
}
