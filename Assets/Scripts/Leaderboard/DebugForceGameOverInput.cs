using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// 정식 게임오버(공연 완료) 조건이 아직 확정되지 않은 동안,
    /// 이름/점수 저장 + 리더보드 UI 흐름을 테스트하기 위한 디버그 트리거.
    ///
    ///   End : Playing 상태에서 GameManager.Instance.GameOver() 강제 호출
    ///
    /// HypeDebugInput 의 Space/Q/W/E/R 과 겹치지 않는 키를 사용한다.
    /// 실제 게임오버 조건이 정해지면 이 스크립트는 제거하고 그 트리거로 교체하면 되며,
    /// GameOver() 이후의 팝업 흐름(ScoreEntryPopup → LeaderboardPopup)은 그대로 재사용된다.
    /// </summary>
    public class DebugForceGameOverInput : MonoBehaviour
    {
        [SerializeField, Tooltip("화면 좌상단에 조작법을 표시할지")]
        bool showOnScreenHelp = true;

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.endKey.wasPressedThisFrame) gm.GameOver();
#else
            if (Input.GetKeyDown(KeyCode.End)) gm.GameOver();
#endif
        }

        void OnGUI()
        {
            if (!showOnScreenHelp || !GameManager.HasInstance) return;
            if (GameManager.Instance.State != GameState.Playing) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            GUI.Label(new Rect(20f, 50f, 900f, 40f), "[디버그] End: 공연 강제 종료 (점수 저장 테스트)", style);
        }
    }
}
