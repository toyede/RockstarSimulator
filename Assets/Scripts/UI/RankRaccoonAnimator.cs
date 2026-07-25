using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 결과 화면에서 랭크 알파벳 옆에 붙는 너구리 2프레임 루프.
    /// 클리어면 <c>raccoon_clear</c>, 실패면 <c>raccoon_gameover</c> 를 재생한다.
    ///
    /// 팝업이 열릴 때 <see cref="UIPopup"/> 이 루트를 SetActive 하므로 이 오브젝트의
    /// <c>OnEnable</c> 이 같은 프레임에 뜬다 — 그때 결과를 읽어 클립을 고른다.
    /// 팝업이나 GameOverPopup 을 수정하지 않는다.
    ///
    /// 프로젝트 공용 <see cref="SpriteAnimationClip"/> 을 쓰므로 프레임이 2장에서 늘어나도
    /// 인스펙터의 frames 배열만 바꾸면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class RankRaccoonAnimator : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, Tooltip("비워두면 이 오브젝트에서 찾는다")]
        Image targetImage;

        [Header("클립")]
        [SerializeField, Tooltip("목표 점수를 달성했을 때")]
        SpriteAnimationClip clearClip = new SpriteAnimationClip
        {
            clipName = "Clear", fps = 4f, loop = true,
        };

        [SerializeField, Tooltip("목표 점수에 못 미쳤을 때")]
        SpriteAnimationClip gameOverClip = new SpriteAnimationClip
        {
            clipName = "GameOver", fps = 3f, loop = true,
        };

        [Header("재생")]
        [SerializeField, Tooltip(
            "결과 화면은 보통 timeScale 이 0이므로 켜 두어야 애니메이션이 돈다")]
        bool useUnscaledTime = true;

        readonly SpriteAnimationPlayer _player = new SpriteAnimationPlayer();
        bool _warnedMissingFrames;

        void Awake()
        {
            if (targetImage == null) targetImage = GetComponent<Image>();
            _player.Bind(null, targetImage);
        }

        void OnEnable()
        {
            if (targetImage == null) targetImage = GetComponent<Image>();
            _player.Bind(null, targetImage);

            // 결과는 팝업이 열리는 시점에 확정돼 있다 (GameOverPopup.OnOpen 과 같은 값을 읽는다)
            SpriteAnimationClip clip = PerformanceTimer.Failed ? gameOverClip : clearClip;
            if (clip == null || !clip.IsValid)
            {
                WarnMissingFramesOnce();
                targetImage.enabled = false;
                return;
            }

            targetImage.enabled = true;
            _player.Play(clip, restart: true);
        }

        void OnDisable() => _player.Stop();

        void Update() =>
            _player.Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        void WarnMissingFramesOnce()
        {
            if (_warnedMissingFrames) return;

            _warnedMissingFrames = true;
            Debug.LogWarning(
                "[RankRaccoon] 프레임이 비어 있습니다. " +
                "Tools/UI/Setup Rank Raccoon 을 실행하세요.",
                this);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Play Clear")]
        void DebugClear() => PlayForDebug(clearClip);

        [ContextMenu("Debug/Play Game Over")]
        void DebugGameOver() => PlayForDebug(gameOverClip);

        void PlayForDebug(SpriteAnimationClip clip)
        {
            if (targetImage == null) targetImage = GetComponent<Image>();
            _player.Bind(null, targetImage);
            if (clip == null || !clip.IsValid) { WarnMissingFramesOnce(); return; }

            targetImage.enabled = true;
            _player.Play(clip, restart: true);
        }
#endif
    }
}
