using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 개발 중 공연 시작/재시작·호응도 판정을 키보드로 제어하는 디버그 입력.
    ///
    ///   Space   : 공연 시작          (Ready 상태의 보조 입력)
    ///   숫자키  : 공연 시작 및 현재 손패 선택
    ///   Q/W/E/R : 호응도 판정 적용    (Playing 중 — Perfect/Good/Miss/RiskMiss)
    ///   R       : 재시작              (GameOver 상태에서)
    ///
    /// 카드 선택은 CardInput이 담당하므로 숫자키 판정을 여기서 중복 처리하지 않는다.
    /// 옛 1234 테스트 판정 입력은 숫자키가 카드 선택으로 바뀌면서 QWER 로 이전했다.
    /// R 키는 Playing 중엔 RiskMiss, GameOver 중엔 재시작으로 상태별로 분리된다.
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

            // 디버그: QWER 로 호응도 판정 직접 적용 (Playing 중에만).
            // 옛 1234 판정 입력을 그대로 이전한 것. 실제 증감량은 HypeConfig 가 정한다.
            if (gm.State == GameState.Playing)
            {
                if (kb.qKey.wasPressedThisFrame) Hype.Apply(HypeJudgement.Perfect);
                if (kb.wKey.wasPressedThisFrame) Hype.Apply(HypeJudgement.Good);
                if (kb.eKey.wasPressedThisFrame) Hype.Apply(HypeJudgement.Miss);
                if (kb.rKey.wasPressedThisFrame) Hype.Apply(HypeJudgement.RiskMiss);
            }

#else
            // (구) Input Manager 프로젝트 설정용 폴백. UIManager 와 같은 분기 방식.
            if (Input.GetKeyDown(KeyCode.Space) && gm.State == GameState.Ready) gm.StartGame();
            if (Input.GetKeyDown(KeyCode.R) && gm.State == GameState.GameOver) gm.RestartScene();

            // 디버그: QWER 로 호응도 판정 직접 적용 (Playing 중에만).
            if (gm.State == GameState.Playing)
            {
                if (Input.GetKeyDown(KeyCode.Q)) Hype.Apply(HypeJudgement.Perfect);
                if (Input.GetKeyDown(KeyCode.W)) Hype.Apply(HypeJudgement.Good);
                if (Input.GetKeyDown(KeyCode.E)) Hype.Apply(HypeJudgement.Miss);
                if (Input.GetKeyDown(KeyCode.R)) Hype.Apply(HypeJudgement.RiskMiss);
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
                    msg = "[디버그] 숫자키: 카드 선택 및 공연 시작  /  Space: 시작";
                    break;
                case GameState.Playing:
                    string paused = Hype.IsDecayPaused ? "  (감소 정지 중)" : "";
                    msg = $"[디버그] 숫자키: 하단 카드 선택  /  Q·W·E·R: 호응도 판정(Perfect·Good·Miss·RiskMiss){paused}";
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
