using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>동료의 무대 반응을 기존 프레임 플레이어의 세 클립으로 전환한다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteSheetAnimator))]
    public sealed class CompanionPerformanceAnimator : MonoBehaviour
    {
        public const string BasicClip = "Basic";
        public const string FeverClip = "Fever";
        public const string FlusterClip = "Fluster";
        const float PlaybackScale = 0.75f;

        SpriteSheetAnimator _animator;
        bool _fever;

        void Awake()
        {
            _animator = GetComponent<SpriteSheetAnimator>();
            _animator.SpeedMultiplier *= PlaybackScale;
        }

        void OnEnable()
        {
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
            _fever = FeverSystem.HasInstance && FeverSystem.Instance.IsActive;
            PlayLoop();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnCardResolved(CardResolved result)
        {
            if (result.Role != CardRole.Utility && result.GainedScore < 0) PlayFluster();
        }

        void OnComboChanged(ComboChanged combo)
        {
            if (combo.WasLost && combo.PreviousCombo > 0) PlayFluster();
        }

        void OnFeverChanged(FeverStateChanged fever)
        {
            _fever = fever.IsActive;
            PlayLoop();
        }

        void OnGameStateChanged(GameStateChanged state)
        {
            if (state.Current != GameState.Ready && state.Current != GameState.GameOver) return;
            _fever = false;
            PlayLoop();
        }

        void PlayFluster()
        {
            // 한 카드에서 콤보 끊김과 마이너스 점수가 함께 와도 같은 클립을 재시작하지 않는다.
            if (_fever || !GameManager.HasInstance || !GameManager.Instance.IsPlaying ||
                (_animator.CurrentClipName == FlusterClip && _animator.IsPlaying)) return;
            _animator.PlayOnce(FlusterClip, PlayLoop);
        }

        void PlayLoop()
        {
            if (isActiveAndEnabled) _animator.Play(_fever ? FeverClip : BasicClip);
        }
    }
}
