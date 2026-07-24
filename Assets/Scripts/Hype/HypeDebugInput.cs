using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// [임시] 카드 시스템 완성 전, 키보드만으로 호응도 루프를 테스트하는 디버그 입력.
    ///
    ///   Space : 공연 시작            (Ready 상태에서)
    ///   1~4   : Perfect / Good / Miss / RiskMiss 판정 발생 (Playing 중)
    ///   R     : 재시작               (GameOver 상태에서)
    ///
    /// 카드 시스템이 ApplyJudgement() 를 직접 호출하게 되면 이 컴포넌트는
    /// 삭제하지 말고 비활성화만 해둔다. (밸런스 테스트에 계속 유용함)
    /// </summary>
    public class HypeDebugInput : MonoBehaviour
    {
        [SerializeField, Tooltip("화면 좌상단에 조작법/상태 안내를 표시할지")]
        bool showOnScreenHelp = true;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.spaceKey.wasPressedThisFrame && gm.State == GameState.Ready) gm.StartGame();
            if (kb.rKey.wasPressedThisFrame && gm.State == GameState.GameOver) gm.RestartScene();

            if (gm.IsPlaying)
            {
                // Hype 전역 접근자 사용 예시 — 카드 담당도 이렇게 호출하면 된다
                if (kb.digit1Key.wasPressedThisFrame) Hype.Apply(HypeJudgement.Perfect);
                if (kb.digit2Key.wasPressedThisFrame) Hype.Apply(HypeJudgement.Good);
                if (kb.digit3Key.wasPressedThisFrame) Hype.Apply(HypeJudgement.Miss);
                if (kb.digit4Key.wasPressedThisFrame) Hype.Apply(HypeJudgement.RiskMiss);
            }
#else
            // (구) Input Manager 프로젝트 설정용 폴백. UIManager 와 같은 분기 방식.
            if (Input.GetKeyDown(KeyCode.Space) && gm.State == GameState.Ready) gm.StartGame();
            if (Input.GetKeyDown(KeyCode.R) && gm.State == GameState.GameOver) gm.RestartScene();

            if (gm.IsPlaying)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) Hype.Apply(HypeJudgement.Perfect);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Hype.Apply(HypeJudgement.Good);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Hype.Apply(HypeJudgement.Miss);
                if (Input.GetKeyDown(KeyCode.Alpha4)) Hype.Apply(HypeJudgement.RiskMiss);
            }
#endif
        }

        // 그레이박스 단계 임시 안내. 정식 UI 가 생기면 showOnScreenHelp 를 끈다.
        void OnGUI()
        {
            if (!showOnScreenHelp || !GameManager.HasInstance) return;

            string msg;
            switch (GameManager.Instance.State)
            {
                case GameState.Ready:
                    msg = "[디버그] Space : 공연 시작";
                    break;
                case GameState.Playing:
                    string paused = Hype.IsDecayPaused ? "  (감소 정지 중)" : "";
                    msg = $"[디버그] 1:Perfect  2:Good  3:Miss  4:RiskMiss{paused}";
                    break;
                case GameState.GameOver:
                    // 게임오버 안내는 GameOverPopup(중앙 팝업)이 담당하므로 여기서는 표시하지 않는다
                    return;
                default:
                    return;
            }

            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            GUI.Label(new Rect(20f, 20f, 900f, 40f), msg, style);
        }
    }
}
