using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ContextStage
{
    /// <summary>
    /// Reads the pointer once per frame and reveals the preference of the
    /// front-most regular audience member after a continuous hover.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudiencePreferenceHoverController : MonoBehaviour
    {
        [SerializeField] AudiencePreferenceHoverConfig config;
        [SerializeField] AudienceRosterPresenter presenter;
        [SerializeField] Camera worldCamera;

        AudienceMemberActor _hoveredActor;
        float _hoverElapsed;
        bool _revealed;

        void Awake()
        {
            if (presenter == null)
                presenter = GetComponentInParent<AudienceRosterPresenter>();
            if (worldCamera == null)
                worldCamera = Camera.main;

            if (config != null && presenter != null) return;

            Debug.LogError(
                "[AudiencePreferenceHover] Config and AudienceRosterPresenter are required.",
                this);
            enabled = false;
        }

        void OnDisable() => ClearHover();

        void Update()
        {
            if (!CanInspectAudience() ||
                !TryReadPointerPosition(out Vector2 screenPosition) ||
                worldCamera == null)
            {
                ClearHover();
                return;
            }

            float depth = worldCamera.WorldToScreenPoint(
                presenter.MemberRoot.position).z;
            if (depth <= 0f)
            {
                ClearHover();
                return;
            }

            Vector3 worldPoint3 = worldCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, depth));
            if (!presenter.TryGetPreferenceHoverTarget(
                    worldPoint3,
                    out AudienceMemberActor target))
            {
                ClearHover();
                return;
            }

            if (target != _hoveredActor)
            {
                ClearHover();
                _hoveredActor = target;
            }

            if (_revealed) return;

            _hoverElapsed += Time.deltaTime;
            if (_hoverElapsed < config.RevealDelay) return;

            _revealed = true;
            _hoveredActor.SetPreferenceReveal(
                true,
                config.GetColor(_hoveredActor.Snapshot.Preference),
                config.OutlineThickness);
        }

        bool CanInspectAudience()
        {
            if (!GameJamKit.GameManager.HasInstance ||
                !GameJamKit.GameManager.Instance.IsPlaying)
                return false;

            if (CardDragHandler.Current != null)
                return false;

            return EventSystem.current == null ||
                   !EventSystem.current.IsPointerOverGameObject();
        }

        void ClearHover()
        {
            if (_hoveredActor != null)
                _hoveredActor.SetPreferenceReveal(false, default, 0f);

            _hoveredActor = null;
            _hoverElapsed = 0f;
            _revealed = false;
        }

        static bool TryReadPointerPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
#else
            position = Input.mousePosition;
            return true;
#endif
        }
    }
}
