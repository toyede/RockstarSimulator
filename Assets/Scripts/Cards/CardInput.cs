using GameJamKit;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>현재 손패를 숫자키 1~9로 선택한다.</summary>
    public sealed class CardInput : MonoBehaviour
    {
        void Update() => ProcessInput();

        /// <summary>현재 프레임의 숫자키 입력을 처리한다. 자동화 검증에서도 동일 경로를 호출한다.</summary>
        public void ProcessInput()
        {
            int index = GetPressedIndex();
            if (index >= 0) SelectNumber(index + 1);
        }

        /// <summary>화면에 표시된 1부터 시작하는 카드 번호를 선택한다.</summary>
        public bool SelectNumber(int number)
        {
            if (!CardSystem.HasInstance) return false;
            if (!GameManager.HasInstance) return false;
            if (UIManager.HasInstance && UIManager.Instance.AnyPopupOpen) return false;

            int index = number - 1;
            if (index < 0 || index >= CardSystem.Instance.HandCount) return false;

            var gameManager = GameManager.Instance;
            if (gameManager.State == GameState.Ready) gameManager.StartGame();
            return gameManager.IsPlaying && CardSystem.Instance.SelectCard(index);
        }

        static int GetPressedIndex()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return -1;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 4;
            if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame) return 5;
            if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame) return 6;
            if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame) return 7;
            if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame) return 8;
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) return 7;
            if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) return 8;
#endif
            return -1;
        }
    }
}
