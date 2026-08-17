using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 게임오버 화면에서 타이틀로 돌아가는 단일 통로.
    ///
    /// 그냥 <c>LoadScene("Title")</c> 만 호출하면 씬이 언로드되는 동안
    /// 아직 살아 있는 시스템들이 이벤트에 반응해 <b>파괴 중인 오브젝트를 건드린다.</b>
    /// (빌드 로그의 "Cannot set the parent of the GameObject 'Card_XX' while
    ///  activating or deactivating the parent GameObject 'CardHand'" 경고가 그 증거다)
    ///
    /// 그래서 씬을 넘기기 전에 순서대로 정리한다:
    ///   1) 열린 팝업을 모두 닫아 파괴 중 팝업이 이벤트로 다시 열리는 것을 막는다
    ///   2) 특별 관객·튜토리얼처럼 타이머를 돌리는 시스템을 멈춘다
    ///   3) GameManager 를 Ready 로 되돌린다
    ///      (킷 매니저들이 [Managers] 자식으로 있어 DontDestroyOnLoad 가 걸리지 않으므로
    ///       상태가 GameOver 로 굳은 채 다음 씬으로 넘어가면 입력이 전부 먹통이 된다)
    ///   4) 그 다음에 씬 전환
    /// </summary>
    public static class TitleReturn
    {
        public const string TitleSceneName = "Title";

        /// <summary>정리 후 타이틀로. 이미 로딩 중이면 중복 호출을 무시한다.</summary>
        public static void Go()
        {
            if (SceneLoader.IsLoading) return; // 버튼 연타 방지

            CloseAllPopups();
            StopRunningSystems();
            ResetGameState();
            ResetTourRun();

            SceneLoader.Load(TitleSceneName);
        }

        static void CloseAllPopups()
        {
            if (UIManager.HasInstance) UIManager.Instance.CloseAll();
        }

        static void StopRunningSystems()
        {
            // 타이머를 돌리는 시스템은 씬이 사라지는 동안 이벤트를 더 쏘지 않게 미리 멈춘다
            if (SpecialAudienceManager.HasInstance)
                SpecialAudienceManager.Instance.StopSystem();

            if (HypeSystem.HasInstance)
                HypeSystem.Instance.SetDecayPaused(true);
        }

        static void ResetGameState()
        {
            if (!GameManager.HasInstance) return;

            // Ready 로 되돌려 타이틀에서 "시작" 을 눌렀을 때 정상 진행되게 한다.
            // (GameOver 상태가 남으면 카드 입력·ESC 일시정지가 전부 막힌다)
            GameManager.Instance.ResetGame();
        }

        static void ResetTourRun()
        {
            if (TourRunManager.HasInstance)
                TourRunManager.Instance.ResetRun();
        }
    }
}
