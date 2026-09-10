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
        [SerializeField] AudienceHoverDialogueView dialogueView;

        AudienceMemberActor _hoveredActor;
        float _hoverElapsed;
        bool _revealed;
        bool _dialogueShown;
        bool _revealAllPreferences;
        AudienceId _lastDialogueActor;
        int _lastDialogueIndex = -1;

        /// <summary>[튜토리얼 전용] 현재 테두리가 표시된 대상. 없으면 null.</summary>
        public AudienceMemberActor RevealedActor => _revealed ? _hoveredActor : null;

        /// <summary>[연출 전용] false 인 동안 호버로 성향을 볼 수 없다 (정전). 연출이 끝나면 반드시 true 로.</summary>
        public bool RevealAllowed { get; set; } = true;

        void Awake()
        {
            if (presenter == null)
                presenter = GetComponentInParent<AudienceRosterPresenter>();
            if (worldCamera == null)
                worldCamera = Camera.main;
            if (dialogueView == null)
                dialogueView = GetComponent<AudienceHoverDialogueView>();
            if (dialogueView == null)
                dialogueView = gameObject.AddComponent<AudienceHoverDialogueView>();
            dialogueView.Configure(config, worldCamera);

            if (config != null && presenter != null)
            {
                _revealAllPreferences =
                    AugmentRuntime.Current.RevealAudiencePreferences;
                presenter.SetAllPreferencesRevealed(
                    _revealAllPreferences,
                    config);
                return;
            }

            Debug.LogError(
                "[AudiencePreferenceHover] Config and AudienceRosterPresenter are required.",
                this);
            enabled = false;
        }

        void OnDisable()
        {
            if (presenter != null)
                presenter.SetAllPreferencesRevealed(false, config);
            ClearHover();
            if (dialogueView != null) dialogueView.Hide(immediate: true);
        }

        void Update()
        {
            if (!CanInspectAudience() ||
                !TryReadPointerPosition(
                    out Vector2 screenPosition,
                    out int pointerId,
                    out bool isTouch) ||
                IsPointerOverUI(pointerId, isTouch) ||
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

            _hoverElapsed += Time.deltaTime;
            if (!_revealed && _hoverElapsed >= config.RevealDelay)
            {
                _revealed = true;
                if (!_revealAllPreferences)
                {
                    _hoveredActor.SetPreferenceReveal(
                        true,
                        config.GetColor(_hoveredActor.Snapshot.Preference),
                        config.OutlineThickness);
                }
            }

            if (_dialogueShown || _hoverElapsed < config.DialogueDelay) return;

            int previousIndex = _lastDialogueActor == _hoveredActor.BoundId
                ? _lastDialogueIndex
                : -1;
            string dialogue = config.GetRandomDialogue(
                _hoveredActor.Snapshot.Preference,
                _hoveredActor.Snapshot.Stage,
                previousIndex,
                out int selectedIndex);
            _dialogueShown = true;
            _lastDialogueActor = _hoveredActor.BoundId;
            _lastDialogueIndex = selectedIndex;
            dialogueView.Show(
                _hoveredActor,
                dialogue,
                config.GetColor(_hoveredActor.Snapshot.Preference));
        }

        bool CanInspectAudience()
        {
            if (!RevealAllowed) return false;

            if (!GameJamKit.GameManager.HasInstance ||
                !GameJamKit.GameManager.Instance.IsPlaying)
                return false;

            if (CardDragHandler.Current != null)
                return false;

            return true;
        }

        static bool IsPointerOverUI(int pointerId, bool isTouch)
        {
            if (EventSystem.current == null) return false;

            return isTouch
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        void ClearHover()
        {
            if (_hoveredActor != null && !_revealAllPreferences)
                _hoveredActor.SetPreferenceReveal(false, default, 0f);

            _hoveredActor = null;
            _hoverElapsed = 0f;
            _revealed = false;
            _dialogueShown = false;
            if (dialogueView != null) dialogueView.Hide(immediate: false);
        }

        static bool TryReadPointerPosition(
            out Vector2 position,
            out int pointerId,
            out bool isTouch)
        {
#if ENABLE_INPUT_SYSTEM
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null &&
                touchscreen.primaryTouch.press.isPressed)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                pointerId = touchscreen.primaryTouch.touchId.ReadValue();
                isTouch = true;
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                pointerId = -1;
                isTouch = false;
                return true;
            }

            position = default;
            pointerId = -1;
            isTouch = false;
            return false;
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase != TouchPhase.Ended &&
                    touch.phase != TouchPhase.Canceled)
                {
                    position = touch.position;
                    pointerId = touch.fingerId;
                    isTouch = true;
                    return true;
                }
            }

            position = Input.mousePosition;
            pointerId = -1;
            isTouch = false;
            return true;
#endif
        }
    }
}
