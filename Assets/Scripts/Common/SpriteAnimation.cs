using System;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 스프라이트 프레임 애니메이션 한 벌.
    ///
    /// 프레임이 1장뿐이면 그냥 정지 이미지로 동작하므로,
    /// 아트가 아직 한 장만 줬을 때도 그대로 쓰다가 나중에 프레임만 늘리면 된다.
    /// (Animator·AnimationClip 에셋을 만들지 않아 씬/에셋 병합 충돌이 생기지 않는다)
    /// </summary>
    [Serializable]
    public class SpriteAnimationClip
    {
        [Tooltip("코드에서 부를 이름. 예: \"Idle\", \"Jump\", \"SpecialHit\"")]
        public string clipName = "Idle";

        [Tooltip("프레임 순서대로. 1장만 넣으면 정지 이미지가 된다")]
        public Sprite[] frames;

        [Tooltip("초당 프레임 수. 0 이면 첫 프레임에서 멈춘다")]
        public float fps = 8f;

        [Tooltip("끝까지 재생한 뒤 처음으로 돌아갈지")]
        public bool loop = true;

        [Tooltip("끝 프레임에서 되감아 왕복할지 (관객 흔들림처럼 왕복이 자연스러운 동작용)")]
        public bool pingPong = false;

        public bool IsValid => frames != null && frames.Length > 0;
        public int FrameCount => frames != null ? frames.Length : 0;

        /// <summary>한 번 재생하는 데 걸리는 시간(초). fps 가 0이면 0.</summary>
        public float Duration => fps > 0f && FrameCount > 0 ? FrameCount / fps : 0f;

        /// <summary>
        /// 재생 시작 후 elapsed 초가 지난 시점의 프레임.
        /// finished 는 loop 가 아닌 클립이 끝까지 재생됐는지를 알려준다.
        /// </summary>
        public Sprite Evaluate(float elapsed, out bool finished)
        {
            finished = false;
            if (!IsValid) return null;

            int count = frames.Length;
            if (fps <= 0f || count == 1) { finished = true; return frames[0]; }

            int raw = Mathf.FloorToInt(Mathf.Max(0f, elapsed) * fps);

            if (pingPong)
            {
                // 0,1,2,1,0,1,2... 주기는 (count-1)*2
                int period = Mathf.Max(1, (count - 1) * 2);
                if (!loop && raw >= period) { finished = true; return frames[0]; }
                int p = raw % period;
                return frames[p < count ? p : period - p];
            }

            if (raw >= count)
            {
                if (!loop) { finished = true; return frames[count - 1]; }
                raw %= count;
            }
            return frames[raw];
        }

        /// <summary>인덱스로 직접 프레임을 꺼낸다. (관객처럼 개체별로 한 장씩 나눠 가질 때)</summary>
        public Sprite GetFrameAt(int index)
        {
            if (!IsValid) return null;
            int count = frames.Length;
            return frames[((index % count) + count) % count]; // 음수 인덱스도 안전
        }
    }

    /// <summary>
    /// 클립을 실제로 재생하는 최소 단위. MonoBehaviour 가 아니라서
    /// 어떤 컴포넌트든 필드로 하나 들고 Tick 만 불러주면 된다. (프레임당 할당 0)
    ///
    /// 출력 대상은 SpriteRenderer(월드)와 Image(UI) 둘 다 지원한다.
    /// </summary>
    [Serializable]
    public class SpriteAnimationPlayer
    {
        [SerializeField, Tooltip("월드에 그릴 때")] SpriteRenderer spriteRenderer;
        [SerializeField, Tooltip("UI 에 그릴 때")] Image image;

        SpriteAnimationClip _clip;
        float _elapsed;
        bool _finished;
        Action _onComplete;

        public SpriteAnimationClip CurrentClip => _clip;
        public bool IsPlaying => _clip != null && !_finished;
        public bool IsFinished => _finished;

        public SpriteAnimationPlayer() { }

        public SpriteAnimationPlayer(SpriteRenderer renderer, Image uiImage = null)
        {
            spriteRenderer = renderer;
            image = uiImage;
        }

        /// <summary>런타임에 출력 대상을 지정한다. (스포너가 만든 오브젝트 등)</summary>
        public void Bind(SpriteRenderer renderer, Image uiImage = null)
        {
            spriteRenderer = renderer;
            image = uiImage;
        }

        /// <summary>클립 재생. 같은 클립이면 restart 를 켜야 처음부터 다시 돈다.</summary>
        public void Play(SpriteAnimationClip clip, bool restart = false, Action onComplete = null)
        {
            if (clip == null || !clip.IsValid) return;
            if (_clip == clip && !restart && !_finished) return;

            _clip = clip;
            _elapsed = 0f;
            _finished = false;
            _onComplete = onComplete;
            Apply(clip.Evaluate(0f, out _));
        }

        /// <summary>매 프레임 호출. deltaTime 은 호출부가 결정한다 (일시정지 중에는 넘기지 않으면 된다).</summary>
        public void Tick(float deltaTime)
        {
            if (_clip == null || _finished) return;

            _elapsed += deltaTime;
            var sprite = _clip.Evaluate(_elapsed, out bool finished);
            Apply(sprite);

            if (!finished) return;

            _finished = true;
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke();
        }

        /// <summary>클립 없이 스프라이트 한 장만 꽂을 때. (정지 이미지)</summary>
        public void SetSprite(Sprite sprite)
        {
            _clip = null;
            _finished = true;
            Apply(sprite);
        }

        public void Stop()
        {
            _clip = null;
            _finished = true;
            _onComplete = null;
        }

        void Apply(Sprite sprite)
        {
            if (spriteRenderer != null) spriteRenderer.sprite = sprite;
            if (image != null)
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
            }
        }
    }
}
