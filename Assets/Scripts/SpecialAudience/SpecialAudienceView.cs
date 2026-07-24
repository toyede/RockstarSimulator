using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 특별 관객의 화면 표현만 담당한다. 판정·타이머 로직은 전혀 모른다.
    ///
    /// 레퍼런스가 하나도 연결되지 않아도 컴파일·실행된다 (전부 null 체크).
    /// Animator 가 없으면 GameObject 활성/비활성만으로 동작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpecialAudienceView : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField, Tooltip("특별 관객 전체를 켜고 끄는 루트. 비워두면 이 오브젝트를 사용한다")]
        GameObject specialAudienceRoot;

        [Header("요구별 스프라이트 (프레임 1장 = 정지 이미지, 여러 장 = 애니메이션)")]
        [SerializeField, Tooltip("Chill 요구")] SpriteAnimationClip chillClip = new SpriteAnimationClip { clipName = "Chill" };
        [SerializeField, Tooltip("Singalong 요구")] SpriteAnimationClip singalongClip = new SpriteAnimationClip { clipName = "Singalong" };
        [SerializeField, Tooltip("Mosh 요구")] SpriteAnimationClip moshClip = new SpriteAnimationClip { clipName = "Mosh" };

        [Header("상태 연출 클립 (선택. 비우면 요구 클립을 계속 재생한다)")]
        [SerializeField, Tooltip("Special Hit 성공 연출")] SpriteAnimationClip specialHitClip = new SpriteAnimationClip { clipName = "SpecialHit", loop = false };
        [SerializeField, Tooltip("시간 초과 퇴장 연출")] SpriteAnimationClip expireClip = new SpriteAnimationClip { clipName = "Expire", loop = false };

        [Header("출력 대상")]
        [SerializeField, Tooltip("UI 로 표시할 때")]
        Image requestImage;

        [SerializeField, Tooltip("무대 위(월드)에 세울 때")]
        SpriteRenderer requestRenderer;

        [SerializeField, Tooltip("일시정지 중에도 애니메이션을 돌릴지")]
        bool useUnscaledTime = false;

        readonly SpriteAnimationPlayer _player = new SpriteAnimationPlayer();

        [Header("요구 아이콘 오브젝트 (스프라이트 대신 오브젝트를 켜고 끄고 싶을 때)")]
        [SerializeField] GameObject chillIcon;
        [SerializeField] GameObject singalongIcon;
        [SerializeField] GameObject moshIcon;

        [Header("타이머")]
        [SerializeField, Tooltip("남은 시간을 fillAmount 로 표시할 Image (Image Type = Filled)")]
        Image timerFillImage;

        [Header("애니메이션")]
        [SerializeField, Tooltip("없어도 동작한다. 있으면 Appear/SpecialHit/Expire/Hide 트리거를 쏜다")]
        Animator animator;

        [SerializeField, Tooltip("시간 초과 연출을 보여주고 숨기기까지의 시간(초)")]
        float expireHideDelay = 0.4f;

        // Animator 트리거 이름은 문자열 비교 비용을 없애려고 해시로 캐싱한다
        static readonly int AppearHash = Animator.StringToHash("Appear");
        static readonly int SpecialHitHash = Animator.StringToHash("SpecialHit");
        static readonly int ExpireHash = Animator.StringToHash("Expire");
        static readonly int HideHash = Animator.StringToHash("Hide");

        float _hideAt = -1f; // 예약된 숨김 시각 (Time.time 기준). -1 이면 예약 없음

        GameObject Root => specialAudienceRoot != null ? specialAudienceRoot : gameObject;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            _player.Bind(requestRenderer, requestImage);
            SetRootActive(false);
        }

        void Update()
        {
            _player.Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

            if (_hideAt < 0f || Time.time < _hideAt) return;
            _hideAt = -1f;
            Hide(instant: true);
        }

        // ---------------- 매니저가 호출하는 API ----------------

        /// <summary>특별 관객 등장.</summary>
        public void Show(SpecialAudienceRequestType requestType, float duration)
        {
            _hideAt = -1f;
            SetRootActive(true);
            ApplyIcon(requestType);
            SetRemaining(duration, duration);
            Trigger(AppearHash);
        }

        /// <summary>남은 시간 갱신. 게이지가 없으면 아무 일도 하지 않는다.</summary>
        public void SetRemaining(float remaining, float total)
        {
            if (timerFillImage == null) return;
            timerFillImage.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }

        /// <summary>Special Hit 성공 연출. 숨기는 시점은 매니저가 결정한다.</summary>
        public void PlaySpecialHit()
        {
            Trigger(SpecialHitHash);
            if (specialHitClip.IsValid) _player.Play(specialHitClip, restart: true);
        }

        /// <summary>시간 초과 퇴장 연출. 잠깐 보여준 뒤 스스로 숨는다.</summary>
        public void PlayExpire()
        {
            Trigger(ExpireHash);
            if (expireClip.IsValid) _player.Play(expireClip, restart: true);
            _hideAt = Time.time + Mathf.Max(0f, expireHideDelay);
        }

        /// <summary>숨김. instant 가 false 면 Hide 트리거를 쏘고 곧바로 비활성화한다.</summary>
        public void Hide(bool instant = false)
        {
            _hideAt = -1f;
            if (!instant) Trigger(HideHash);
            SetRootActive(false);
        }

        // ---------------- 내부 ----------------

        void ApplyIcon(SpecialAudienceRequestType requestType)
        {
            // (1) 클립 방식 — 프레임 1장이면 정지 이미지, 여러 장이면 애니메이션
            var clip = ResolveClip(requestType);
            if (clip != null && clip.IsValid) _player.Play(clip, restart: true);
            else _player.SetSprite(null);

            // (2) 오브젝트 방식 — 타입별로 다른 오브젝트를 켜고 끈다 (그레이박스 텍스트 등)
            SetActiveSafe(chillIcon, requestType == SpecialAudienceRequestType.Chill);
            SetActiveSafe(singalongIcon, requestType == SpecialAudienceRequestType.Singalong);
            SetActiveSafe(moshIcon, requestType == SpecialAudienceRequestType.Mosh);
        }

        /// <summary>요구 타입 → 클립. 비어 있으면 null (그때는 오브젝트 방식만 동작).</summary>
        SpriteAnimationClip ResolveClip(SpecialAudienceRequestType requestType)
        {
            switch (requestType)
            {
                case SpecialAudienceRequestType.Chill:     return chillClip;
                case SpecialAudienceRequestType.Singalong: return singalongClip;
                case SpecialAudienceRequestType.Mosh:      return moshClip;
                default:                                   return null;
            }
        }

        void SetRootActive(bool active)
        {
            var root = Root;
            // 루트를 지정하지 않아 이 오브젝트 자신이 루트라면, 꺼버리면 Update 가 멈춰
            // 예약된 숨김이 처리되지 않는다. 이 경우 자식만 정리한다.
            if (root == gameObject && !active)
            {
                SetActiveSafe(chillIcon, false);
                SetActiveSafe(singalongIcon, false);
                SetActiveSafe(moshIcon, false);
                if (timerFillImage != null) timerFillImage.fillAmount = 0f;
                return;
            }
            if (root.activeSelf != active) root.SetActive(active);
        }

        static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        void Trigger(int hash)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.SetTrigger(hash);
        }
    }
}
