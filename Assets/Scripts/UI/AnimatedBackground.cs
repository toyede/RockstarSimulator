using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 배경 프레임 애니메이션을 계속 돌린다. (타이틀 화면 배경 BG_0000 ~ BG_0028)
    ///
    /// 프로젝트 공용 <see cref="SpriteAnimationClip"/> 을 그대로 쓴다 —
    /// Animator·AnimationClip 에셋을 만들지 않으므로 씬/에셋 병합 충돌이 없고,
    /// 프레임을 늘리거나 줄일 때 인스펙터의 frames 배열만 바꾸면 된다.
    ///
    /// 출력 대상은 SpriteRenderer(월드)와 Image(UI) 둘 다 지원한다.
    /// 비워두면 같은 오브젝트에서 알아서 찾는다.
    ///
    /// 프레임 채우기는 손으로 하지 않는다:
    /// <c>Tools/UI/Setup Animated Background</c> 가 폴더에서 번호순으로 읽어 채운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatedBackground : MonoBehaviour
    {
        [Header("출력 대상 (비워두면 이 오브젝트에서 찾는다)")]
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Image targetImage;

        [Header("프레임")]
        [SerializeField, Tooltip("Tools/UI/Setup Animated Background 로 채운다")]
        SpriteAnimationClip clip = new SpriteAnimationClip
        {
            clipName = "Background",
            fps = 12f,
            loop = true,
            pingPong = false,
        };

        [Header("재생")]
        [SerializeField, Tooltip(
            "켜면 Time.timeScale 의 영향을 받지 않는다. " +
            "일시정지·게임오버 중에도 배경은 계속 흐르는 편이 자연스럽다")]
        bool useUnscaledTime = true;

        [SerializeField, Tooltip(
            "시작 시 이 시간(초)만큼 앞에서 재생을 시작한다. " +
            "배경을 여러 겹 깔 때 서로 같은 프레임으로 시작하지 않게 한다")]
        float startOffsetSeconds = 0f;

        readonly SpriteAnimationPlayer _player = new SpriteAnimationPlayer();
        bool _warnedMissingFrames;

        public SpriteAnimationClip Clip => clip;

        void Awake() => BindTargets();

        void BindTargets()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (targetImage == null) targetImage = GetComponent<Image>();
            _player.Bind(spriteRenderer, targetImage);
        }

        void OnEnable()
        {
            BindTargets();

            if (clip == null || !clip.IsValid)
            {
                WarnMissingFramesOnce();
                return;
            }

            if (spriteRenderer == null && targetImage == null)
            {
                Debug.LogError(
                    "[AnimatedBackground] SpriteRenderer 도 Image 도 없습니다. " +
                    "둘 중 하나를 이 오브젝트에 붙이거나 인스펙터에서 연결하세요.",
                    this);
                return;
            }

            // 꺼졌다 켜져도 항상 같은 지점에서 다시 시작한다 (이전 잔상 없음)
            _player.Play(clip, restart: true);
            if (startOffsetSeconds > 0f) _player.Tick(startOffsetSeconds);
        }

        void OnDisable() => _player.Stop();

        void Update()
        {
            _player.Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);
        }

        void WarnMissingFramesOnce()
        {
            if (_warnedMissingFrames) return;

            _warnedMissingFrames = true;
            Debug.LogWarning(
                "[AnimatedBackground] 프레임이 비어 있습니다. " +
                "Tools/UI/Setup Animated Background 를 실행하세요.",
                this);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (clip != null && clip.fps < 0f) clip.fps = 0f;
        }
#endif
    }
}
