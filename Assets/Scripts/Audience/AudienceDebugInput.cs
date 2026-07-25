using GameJamKit;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class AudienceDebugInput : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] bool showOnScreenHelp = true;
#endif

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!GameManager.HasInstance || IsUiInputFocused()) return;
            GameManager gameManager = GameManager.Instance;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame &&
                gameManager.State == GameState.Ready)
                gameManager.StartGame();
            if (keyboard.rKey.wasPressedThisFrame &&
                gameManager.State == GameState.GameOver)
                gameManager.RestartScene();
#else
            if (Input.GetKeyDown(KeyCode.Space) &&
                gameManager.State == GameState.Ready)
                gameManager.StartGame();
            if (Input.GetKeyDown(KeyCode.R) &&
                gameManager.State == GameState.GameOver)
                gameManager.RestartScene();
#endif
#endif
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showOnScreenHelp || !GameManager.HasInstance) return;

            string message;
            switch (GameManager.Instance.State)
            {
                case GameState.Ready:
                    message = "[디버그] 숫자키: 카드 선택 및 공연 시작  /  Space: 시작";
                    break;
                case GameState.Playing:
                    message =
                        $"[디버그] 숫자키: 카드 선택  /  " +
                        $"관객 {AudienceRoster.Count}명";
                    break;
                case GameState.GameOver:
                    message = "[디버그] R: 다시 시작";
                    break;
                default:
                    return;
            }

            var style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            GUI.Label(new Rect(20f, 20f, 700f, 40f), message, style);
#endif
        }

        static bool IsUiInputFocused()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null &&
                   eventSystem.currentSelectedGameObject != null;
        }
    }
}
