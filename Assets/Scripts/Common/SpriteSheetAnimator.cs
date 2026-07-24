using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 이름으로 클립을 골라 재생하는 범용 스프라이트 애니메이터.
    /// Animator/AnimatorController 에셋 없이 인스펙터 리스트만으로 동작한다.
    ///
    /// 주인공(락스타)처럼 상태가 여러 개인 오브젝트에 붙여 쓰면 된다:
    ///   animator.Play("Idle");
    ///   animator.PlayOnce("Solo", onComplete: () =&gt; animator.Play("Idle"));
    ///
    /// 왜 Animator 를 안 쓰나: .controller/.anim 은 바이너리에 가까운 에셋이라
    /// 여러 명이 동시에 건드리면 병합이 안 되고, 잼 규모에서 얻는 이득(블렌드 트리·전이 조건)이
    /// 크지 않다. 상세 비교는 Assets/Scripts/Common/README.md 참고.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpriteSheetAnimator : MonoBehaviour
    {
        [SerializeField, Tooltip("클립 목록. clipName 으로 골라 재생한다")]
        List<SpriteAnimationClip> clips = new List<SpriteAnimationClip>();

        [SerializeField, Tooltip("시작 시 자동 재생할 클립 이름. 비우면 재생하지 않는다")]
        string defaultClip = "Idle";

        [SerializeField, Tooltip("체크하면 Time.unscaledDeltaTime 을 쓴다 (일시정지 중에도 움직임)")]
        bool useUnscaledTime = false;

        [SerializeField] SpriteAnimationPlayer player = new SpriteAnimationPlayer();

        /// <summary>이름 → 클립. 매번 리스트를 순회하지 않도록 한 번만 만든다.</summary>
        Dictionary<string, SpriteAnimationClip> _lookup;

        public SpriteAnimationPlayer Player => player;
        public bool IsPlaying => player.IsPlaying;
        public string CurrentClipName => player.CurrentClip?.clipName;

        void Awake()
        {
            // 출력 대상이 인스펙터에서 비어 있으면 같은 오브젝트에서 찾아 붙인다
            player.Bind(GetComponent<SpriteRenderer>(), GetComponent<Image>());
            BuildLookup();
        }

        void OnEnable()
        {
            if (!string.IsNullOrEmpty(defaultClip)) Play(defaultClip);
        }

        void Update() => player.Tick(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        void BuildLookup()
        {
            _lookup = new Dictionary<string, SpriteAnimationClip>(clips.Count);
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip != null && !string.IsNullOrEmpty(clip.clipName)) _lookup[clip.clipName] = clip;
            }
        }

        public SpriteAnimationClip GetClip(string clipName)
        {
            if (_lookup == null) BuildLookup();
            return !string.IsNullOrEmpty(clipName) && _lookup.TryGetValue(clipName, out var clip) ? clip : null;
        }

        /// <summary>클립을 재생한다. 없는 이름이면 아무 일도 하지 않는다.</summary>
        public void Play(string clipName, bool restart = false)
        {
            var clip = GetClip(clipName);
            if (clip == null) return;
            player.Play(clip, restart);
        }

        /// <summary>한 번만 재생하고 끝나면 콜백. (loop 설정과 무관하게 1회로 취급하려면 클립의 loop 를 꺼둘 것)</summary>
        public void PlayOnce(string clipName, System.Action onComplete = null)
        {
            var clip = GetClip(clipName);
            if (clip == null) { onComplete?.Invoke(); return; }
            player.Play(clip, restart: true, onComplete: onComplete);
        }

        public void Stop() => player.Stop();

        /// <summary>런타임에 클립을 추가/교체한다. (에디터 툴이나 데이터 주입용)</summary>
        public void SetClip(SpriteAnimationClip clip)
        {
            if (clip == null || string.IsNullOrEmpty(clip.clipName)) return;
            if (_lookup == null) BuildLookup();

            int index = clips.FindIndex(c => c != null && c.clipName == clip.clipName);
            if (index >= 0) clips[index] = clip;
            else clips.Add(clip);

            _lookup[clip.clipName] = clip;
        }
    }
}
