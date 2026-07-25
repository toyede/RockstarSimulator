using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 주인공(너구리)의 상태 애니메이션. Idle 루프에서 대기하다가 카드를 내면
    /// Stroke 또는 GuitarSolo 를 1회 재생하고, <b>마지막 프레임이 끝나면 자동으로 Idle 로 돌아온다.</b>
    ///
    /// 재생 자체는 공용 SpriteSheetAnimator 가 담당하고(프레임·fps·loop 는 그쪽 인스펙터),
    /// 이 컴포넌트는 "어떤 카드에 어떤 클립을 틀지"만 결정한다.
    ///
    /// 카드 시스템을 전혀 건드리지 않는다 — CardResolved 이벤트만 구독한다.
    /// (CardSelected 가 아니라 CardResolved 를 쓰는 이유: Role 과 CardId 를 둘 다 갖고 있다.
    ///  두 이벤트는 같은 프레임에 발행되므로 타이밍 차이는 없다)
    ///
    /// 클립 선택 우선순위:
    ///   1) cardClipOverrides 에 해당 CardId 가 있으면 그 클립  (예: guitar_solo → GuitarSolo)
    ///   2) 없으면 Role 별 기본 클립                            (Special → GuitarSolo, Normal → Stroke)
    ///   3) 클립 이름이 비어 있으면 아무것도 하지 않는다          (Utility 기본값 — Idle 유지)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteSheetAnimator))]
    public sealed class RaccoonAnimator : MonoBehaviour
    {
        /// <summary>연주 중에 새 카드가 들어왔을 때의 처리 방식.</summary>
        public enum InterruptPolicy
        {
            [Tooltip("항상 새 카드로 갈아탄다 (입력이 씹히지 않는다)")]
            AlwaysRestart,

            [Tooltip("GuitarSolo 는 Stroke 를 끊을 수 있지만, Stroke 는 GuitarSolo 를 끊지 못한다")]
            SoloHasPriority,

            [Tooltip("재생이 끝날 때까지 새 카드를 무시한다")]
            IgnoreWhilePlaying
        }

        [System.Serializable]
        public struct CardClipOverride
        {
            [Tooltip("CardDefinition 의 id (예: guitar_solo)")]
            public string cardId;

            [Tooltip("재생할 클립 이름. 비우면 아무것도 재생하지 않는다")]
            public string clipName;
        }

        [Header("클립 이름 (SpriteSheetAnimator 의 clipName 과 일치해야 한다)")]
        [SerializeField, Tooltip("평상시 루프 (loop = true 로 둘 것)")]
        string idleClip = "Idle";

        [SerializeField, Tooltip("일반 카드. loop = false 로 둬야 끝나고 Idle 로 돌아온다")]
        string strokeClip = "Stroke";

        [SerializeField, Tooltip("기타 솔로·스페셜 카드. loop = false 로 둘 것")]
        string soloClip = "GuitarSolo";

        [Header("Role 별 기본 클립 (비우면 Idle 유지)")]
        [SerializeField, Tooltip("Normal 역할 카드")] string normalRoleClip = "Stroke";
        [SerializeField, Tooltip("Special 역할 카드")] string specialRoleClip = "GuitarSolo";
        [SerializeField, Tooltip("Utility 역할 카드. 기본은 비움 = 연주하지 않고 Idle 유지")]
        string utilityRoleClip = "";

        [Header("카드 ID 개별 지정 (Role 보다 우선)")]
        [SerializeField]
        List<CardClipOverride> cardClipOverrides = new List<CardClipOverride>
        {
            // guitar_solo 는 Role 이 Normal 이지만 솔로 연주를 해야 하므로 여기서 덮어쓴다
            new CardClipOverride { cardId = "guitar_solo", clipName = "GuitarSolo" },
        };

        [Header("타이밍")]
        [SerializeField, Min(0f), Tooltip("카드를 낸 뒤 연주가 시작되기까지의 지연(초). 0 = 즉시")]
        float startDelay = 0f;

        [SerializeField, Tooltip("연주 중 새 카드가 들어왔을 때")]
        InterruptPolicy interruptPolicy = InterruptPolicy.AlwaysRestart;

        SpriteSheetAnimator _animator;
        string _pendingClip;      // 지연 재생 대기 중인 클립
        float _pendingAt;         // 재생할 시각 (unscaled)
        string _performingClip;   // 지금 재생 중인 연주 클립 (없으면 null)

        public string PerformingClip => _performingClip;

        void Awake() => _animator = GetComponent<SpriteSheetAnimator>();

        void OnEnable()
        {
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            _pendingClip = null;
            _performingClip = null;
        }

        void Update()
        {
            if (_pendingClip == null || Time.unscaledTime < _pendingAt) return;

            string clip = _pendingClip;
            _pendingClip = null;
            Perform(clip);
        }

        // ---------------- 이벤트 ----------------

        void OnCardResolved(CardResolved e)
        {
            string clip = ResolveClip(e);
            if (string.IsNullOrEmpty(clip)) return;   // Utility 등 — Idle 유지

            if (!CanInterrupt(clip)) return;

            if (startDelay <= 0f)
            {
                Perform(clip);
            }
            else
            {
                _pendingClip = clip;
                _pendingAt = Time.unscaledTime + startDelay;
            }
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연 준비·종료 시에는 연주를 끊고 평상 상태로 돌린다
            if (e.Current != GameState.Ready && e.Current != GameState.GameOver) return;

            _pendingClip = null;
            _performingClip = null;
            PlayIdle();
        }

        // ---------------- 클립 결정 ----------------

        string ResolveClip(CardResolved e)
        {
            // 1) CardId 개별 지정이 최우선
            for (int i = 0; i < cardClipOverrides.Count; i++)
            {
                var entry = cardClipOverrides[i];
                if (!string.IsNullOrEmpty(entry.cardId) && entry.cardId == e.CardId)
                    return entry.clipName;
            }

            // 2) Role 별 기본값
            switch (e.Role)
            {
                case CardRole.Special: return specialRoleClip;
                case CardRole.Utility: return utilityRoleClip;
                default:               return normalRoleClip;
            }
        }

        bool CanInterrupt(string incomingClip)
        {
            if (_performingClip == null) return true; // 연주 중이 아니면 언제든 가능

            switch (interruptPolicy)
            {
                case InterruptPolicy.IgnoreWhilePlaying:
                    return false;

                case InterruptPolicy.SoloHasPriority:
                    // 솔로 재생 중에는 솔로만 다시 시작할 수 있다
                    if (_performingClip == soloClip) return incomingClip == soloClip;
                    return true;

                default:
                    return true;
            }
        }

        // ---------------- 재생 ----------------

        void Perform(string clipName)
        {
            if (_animator == null) return;

            _performingClip = clipName;

            // 마지막 프레임이 끝나면 Idle 로 복귀.
            // (클립의 loop 가 true 면 완료 콜백이 오지 않으므로 Stroke/GuitarSolo 는 loop = false 여야 한다)
            _animator.PlayOnce(clipName, () =>
            {
                _performingClip = null;
                PlayIdle();
            });
        }

        void PlayIdle()
        {
            if (_animator == null || string.IsNullOrEmpty(idleClip)) return;
            _animator.Play(idleClip);
        }

        // ---------------- 디버그 ----------------

        [ContextMenu("Debug/Play Idle")]
        void DebugIdle() { _performingClip = null; PlayIdle(); }

        [ContextMenu("Debug/Play Stroke")]
        void DebugStroke() => Perform(strokeClip);

        [ContextMenu("Debug/Play Guitar Solo")]
        void DebugSolo() => Perform(soloClip);

#if UNITY_EDITOR
        void OnValidate()
        {
            // loop 가 켜진 연주 클립은 완료 콜백이 오지 않아 Idle 로 못 돌아온다 — 미리 잡아준다
            var animator = GetComponent<SpriteSheetAnimator>();
            if (animator == null) return;

            WarnIfLooping(animator, strokeClip);
            WarnIfLooping(animator, soloClip);
        }

        void WarnIfLooping(SpriteSheetAnimator animator, string clipName)
        {
            var clip = animator.GetClip(clipName);
            if (clip != null && clip.loop)
                Debug.LogWarning(
                    $"[RaccoonAnimator] '{clipName}' 클립의 loop 가 켜져 있어 Idle 로 복귀하지 않습니다. " +
                    "SpriteSheetAnimator 에서 loop 를 끄세요.", this);
        }
#endif
    }
}
