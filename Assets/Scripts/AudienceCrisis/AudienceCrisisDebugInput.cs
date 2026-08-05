using GameJamKit;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class AudienceCrisisDebugInput : MonoBehaviour
    {
        [SerializeField] NearbyConcertCrisisDirector director;
        [SerializeField] bool showDebugButtons = true;

        bool _crisisButtonUsed;
        bool _comebackButtonUsed;

        void OnEnable()
        {
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (director == null) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!_crisisButtonUsed && keyboard.cKey.wasPressedThisFrame)
                    _crisisButtonUsed = director.TryForceStartCrisis();
                if (!_comebackButtonUsed && keyboard.vKey.wasPressedThisFrame)
                    _comebackButtonUsed = director.TryForceStartComeback();
            }
#else
            if (!_crisisButtonUsed && Input.GetKeyDown(KeyCode.C))
                _crisisButtonUsed = director.TryForceStartCrisis();
            if (!_comebackButtonUsed && Input.GetKeyDown(KeyCode.V))
                _comebackButtonUsed = director.TryForceStartComeback();
#endif
#endif
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showDebugButtons || director == null) return;

            const float width = 220f;
            const float height = 92f;
            Rect panel = new Rect(
                Screen.width - width - 16f,
                Screen.height - height - 16f,
                width,
                height);
            GUI.Box(panel, "EVENT DEBUG (ONE SHOT)");

            bool previousEnabled = GUI.enabled;
            GUI.enabled = director.CanForceEvent && !_crisisButtonUsed;
            if (GUI.Button(
                    new Rect(panel.x + 8f, panel.y + 24f, 204f, 26f),
                    _crisisButtonUsed ? "CRISIS USED" : "FORCE CRISIS [C]"))
                _crisisButtonUsed = director.TryForceStartCrisis();

            GUI.enabled = director.CanForceEvent && !_comebackButtonUsed;
            if (GUI.Button(
                    new Rect(panel.x + 8f, panel.y + 56f, 204f, 26f),
                    _comebackButtonUsed ? "COMEBACK USED" : "FORCE COMEBACK [V]"))
                _comebackButtonUsed = director.TryForceStartComeback();
            GUI.enabled = previousEnabled;
#endif
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current != GameState.Ready) return;
            _crisisButtonUsed = false;
            _comebackButtonUsed = false;
        }
    }
}
