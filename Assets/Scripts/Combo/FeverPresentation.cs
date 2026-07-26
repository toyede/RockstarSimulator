using System.Collections.Generic;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 피버타임 진입 연출. <c>FeverStateChanged(IsActive = true)</c> 하나에 반응해
    /// 배너·사운드·캐릭터 속도를 한꺼번에 올린다.
    ///
    /// <list type="number">
    /// <item>fevertime 배너를 화면 중앙에 페이드 인 → 유지 → 페이드 아웃</item>
    /// <item>지정한 캐릭터 애니메이터의 재생 속도를 배수로 올린다 (Friends 2배)</item>
    /// <item>진입 사운드를 한 번 재생한다</item>
    /// </list>
    ///
    /// <b>FeverSystem 을 수정하지 않는다.</b> 이미 발행 중인 이벤트만 구독한다.
    /// 조명 쪽 깜빡임은 <see cref="FeverSpotlight"/> 가 따로 담당한다 —
    /// 무대 조명 오브젝트에 붙어야 해서 이 컴포넌트와 위치가 다르다.
    ///
    /// 배너는 피버 지속시간과 <b>무관하게</b> 제 길이만큼만 재생한다.
    /// 진입을 알리는 연출이지 피버 내내 화면을 가릴 이유가 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeverPresentation : MonoBehaviour
    {
        [Header("배너")]
        [SerializeField, Tooltip("fevertime 스프라이트를 표시할 UI Image. 셋업 메뉴가 만들어 연결한다")]
        Image bannerImage;

        [SerializeField, Min(0.01f), Tooltip("나타나는 시간(초)")]
        float fadeInDuration = 0.25f;

        [SerializeField, Min(0f), Tooltip("완전히 보이는 시간(초)")]
        float holdDuration = 1.1f;

        [SerializeField, Min(0.01f), Tooltip("사라지는 시간(초)")]
        float fadeOutDuration = 0.5f;

        [SerializeField, Tooltip("등장할 때 살짝 커졌다가 제자리로. 1이면 크기 변화 없음")]
        [Min(0.01f)] float popPeakScale = 1.18f;

        [Header("사운드")]
        [SerializeField, Tooltip("피버 진입 사운드. Assets/Audio/OneShot/fevertime_intro.wav")]
        AudioClip introClip;

        [SerializeField, Range(0f, 1f), Tooltip("SFX 채널 볼륨에 곱해진다")]
        float introVolume = 1f;

        [Header("캐릭터 가속")]
        [SerializeField, Tooltip("피버 동안 빨라질 애니메이터. 셋업 메뉴가 Friends 를 연결한다")]
        List<SpriteSheetAnimator> acceleratedAnimators = new List<SpriteSheetAnimator>();

        [SerializeField, Min(0.01f), Tooltip("재생 속도 배율. 2면 두 배 빨라진다")]
        float animatorSpeedMultiplier = 2f;

        // ---------------- 상태 ----------------

        /// <summary>가속 전 원래 배율. 다른 시스템이 미리 바꿔 둔 값이 있어도 그대로 되돌린다.</summary>
        readonly List<float> _originalSpeeds = new List<float>();
        bool _accelerated;

        bool _bannerPlaying;
        float _bannerElapsed;
        Vector3 _bannerBaseScale = Vector3.one;

        float BannerTotal => fadeInDuration + holdDuration + fadeOutDuration;

        void Awake()
        {
            if (bannerImage != null) _bannerBaseScale = bannerImage.rectTransform.localScale;
            HideBanner();
        }

        void OnEnable()
        {
            EventBus.Subscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            RestoreSpeeds();
            HideBanner();
        }

        void OnFeverStateChanged(FeverStateChanged e)
        {
            if (e.IsActive) BeginFever();
            else RestoreSpeeds(); // 배너는 이미 제 길이대로 끝났거나 끝나는 중이다
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연·게임오버에 연출이 남아 있으면 즉시 정리한다
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver)
            {
                RestoreSpeeds();
                HideBanner();
            }
        }

        void BeginFever()
        {
            PlayBanner();
            AccelerateAnimators();

            // 피버 진입은 공통 임팩트다 — 공용 SFX 보이스로 나가므로 버스를 명시한다
            if (introClip != null) Sound.PlayClip(introClip, AudioBus.ImpactSFX, introVolume);
        }

        // ---------------- 캐릭터 가속 ----------------

        void AccelerateAnimators()
        {
            if (_accelerated) return; // 중복 진입 시 원래 배율을 덮어쓰지 않는다

            _originalSpeeds.Clear();
            for (int i = 0; i < acceleratedAnimators.Count; i++)
            {
                SpriteSheetAnimator animator = acceleratedAnimators[i];
                if (animator == null) { _originalSpeeds.Add(1f); continue; }

                _originalSpeeds.Add(animator.SpeedMultiplier);
                animator.SpeedMultiplier = animator.SpeedMultiplier * animatorSpeedMultiplier;
            }

            _accelerated = true;
        }

        void RestoreSpeeds()
        {
            if (!_accelerated) return;

            for (int i = 0; i < acceleratedAnimators.Count && i < _originalSpeeds.Count; i++)
            {
                SpriteSheetAnimator animator = acceleratedAnimators[i];
                if (animator != null) animator.SpeedMultiplier = _originalSpeeds[i];
            }

            _accelerated = false;
        }

        // ---------------- 배너 ----------------

        void PlayBanner()
        {
            if (bannerImage == null)
            {
                Debug.LogWarning(
                    "[Fever] 배너 Image 가 없습니다. " +
                    "Tools/Feedback/Setup Fever Presentation 을 실행하세요.",
                    this);
                return;
            }

            _bannerPlaying = true;
            _bannerElapsed = 0f;
            bannerImage.enabled = true;
            ApplyBanner();
        }

        void Update()
        {
            if (!_bannerPlaying) return;

            // 히트스톱으로 timeScale 이 떨어져도 배너는 정상 속도로 끝난다
            _bannerElapsed += Time.unscaledDeltaTime;
            if (_bannerElapsed >= BannerTotal) { HideBanner(); return; }

            ApplyBanner();
        }

        void ApplyBanner()
        {
            float alpha;
            float scale;

            if (_bannerElapsed < fadeInDuration)
            {
                float t = _bannerElapsed / fadeInDuration;
                alpha = t;
                scale = Mathf.Lerp(popPeakScale, 1f, t); // 크게 들어와 제자리로
            }
            else if (_bannerElapsed < fadeInDuration + holdDuration)
            {
                alpha = 1f;
                scale = 1f;
            }
            else
            {
                float t = (_bannerElapsed - fadeInDuration - holdDuration) /
                          Mathf.Max(0.01f, fadeOutDuration);
                alpha = 1f - Mathf.Clamp01(t);
                scale = 1f;
            }

            Color color = bannerImage.color;
            color.a = Mathf.Clamp01(alpha);
            bannerImage.color = color;
            bannerImage.rectTransform.localScale = _bannerBaseScale * scale;
        }

        void HideBanner()
        {
            _bannerPlaying = false;
            _bannerElapsed = 0f;
            if (bannerImage == null) return;

            Color color = bannerImage.color;
            color.a = 0f;
            bannerImage.color = color;
            bannerImage.rectTransform.localScale = _bannerBaseScale;
            bannerImage.enabled = false;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Play Fever Presentation")]
        void DebugPlay()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Fever] Play Mode 에서 실행하세요.", this);
                return;
            }
            BeginFever();
        }

        [ContextMenu("Debug/Restore Animator Speed")]
        void DebugRestore() => RestoreSpeeds();
#endif
    }
}
