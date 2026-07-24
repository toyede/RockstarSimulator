using System;
using System.Collections;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 코루틴 헬퍼. this.DelayCall(2f, () =&gt; ...) 형태로 사용.
    /// 대상 MonoBehaviour 가 비활성/파괴 상태면 전역 러너로 대체 실행되므로 예외가 나지 않는다.
    /// </summary>
    public static class TimerUtils
    {
        static MonoBehaviour Host(MonoBehaviour mb)
        {
            if (mb != null && mb.isActiveAndEnabled && mb.gameObject.activeInHierarchy) return mb;
            return TimerRunner.Instance;
        }

        /// <summary>delay 초 뒤에 action 실행.</summary>
        public static Coroutine DelayCall(this MonoBehaviour mb, float delay, Action action, bool unscaledTime = false)
        {
            var host = Host(mb);
            return host == null ? null : host.StartCoroutine(DelayRoutine(delay, action, unscaledTime));
        }

        /// <summary>frames 프레임 뒤에 action 실행. (레이아웃/물리 갱신 대기용)</summary>
        public static Coroutine DelayFrames(this MonoBehaviour mb, int frames, Action action)
        {
            var host = Host(mb);
            return host == null ? null : host.StartCoroutine(FrameRoutine(frames, action));
        }

        /// <summary>interval 마다 반복 실행. count 가 -1 이면 무한.</summary>
        public static Coroutine Repeat(this MonoBehaviour mb, float interval, Action action, int count = -1, bool immediate = false)
        {
            var host = Host(mb);
            return host == null ? null : host.StartCoroutine(RepeatRoutine(interval, action, count, immediate));
        }

        /// <summary>condition 이 true 가 되면 action 실행.</summary>
        public static Coroutine WaitUntilThen(this MonoBehaviour mb, Func<bool> condition, Action action)
        {
            var host = Host(mb);
            return host == null ? null : host.StartCoroutine(WaitRoutine(condition, action));
        }

        /// <summary>from → to 로 duration 동안 보간하며 onUpdate 호출. 간단한 트윈 대용.</summary>
        public static Coroutine TweenFloat(this MonoBehaviour mb, float from, float to, float duration,
            Action<float> onUpdate, Action onComplete = null, bool unscaledTime = false)
        {
            var host = Host(mb);
            return host == null ? null : host.StartCoroutine(TweenRoutine(from, to, duration, onUpdate, onComplete, unscaledTime));
        }

        /// <summary>코루틴 안전 취소 후 null 대입. this.Cancel(ref _myRoutine);</summary>
        public static void Cancel(this MonoBehaviour mb, ref Coroutine routine)
        {
            if (routine == null) return;
            if (mb != null) mb.StopCoroutine(routine);
            routine = null;
        }

        // ---- 정적 버전: MonoBehaviour 없이 호출 ----
        public static Coroutine Delay(float delay, Action action, bool unscaledTime = false)
            => TimerRunner.Instance.StartCoroutine(DelayRoutine(delay, action, unscaledTime));

        public static void StopAll() { if (TimerRunner.HasInstance) TimerRunner.Instance.StopAllCoroutines(); }

        // ---- 루틴 구현 ----
        static IEnumerator DelayRoutine(float delay, Action action, bool unscaled)
        {
            if (delay > 0f)
            {
                if (unscaled) yield return new WaitForSecondsRealtime(delay);
                else yield return new WaitForSeconds(delay);
            }
            action?.Invoke();
        }

        static IEnumerator FrameRoutine(int frames, Action action)
        {
            for (int i = 0; i < frames; i++) yield return null;
            action?.Invoke();
        }

        static IEnumerator RepeatRoutine(float interval, Action action, int count, bool immediate)
        {
            if (immediate) action?.Invoke();
            var wait = new WaitForSeconds(interval);
            int done = 0;
            while (count < 0 || done < count)
            {
                yield return wait;
                action?.Invoke();
                done++;
            }
        }

        static IEnumerator WaitRoutine(Func<bool> condition, Action action)
        {
            yield return new WaitUntil(condition);
            action?.Invoke();
        }

        static IEnumerator TweenRoutine(float from, float to, float duration, Action<float> onUpdate, Action onComplete, bool unscaled)
        {
            if (duration <= 0f)
            {
                onUpdate?.Invoke(to);
                onComplete?.Invoke();
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                onUpdate?.Invoke(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            onUpdate?.Invoke(to);
            onComplete?.Invoke();
        }
    }
}
