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

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (director == null) return;
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.cKey.wasPressedThisFrame)
                director.ForceStartCrisis();
#else
            if (Input.GetKeyDown(KeyCode.C))
                director.ForceStartCrisis();
#endif
#endif
        }
    }
}
