using System.Collections;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 타격 순간 시간을 아주 잠깐 멈춰 타격감을 올리는 헬퍼.
    /// HitStop.Do(0.06f); // 60ms 정지
    /// 일시정지(timeScale == 0) 중에는 무시된다.
    /// </summary>
    public static class HitStop
    {
        static Coroutine s_routine;
        static float s_previousScale = 1f;
        static float s_appliedScale = 1f;

        public static void Do(float duration = 0.05f, float timeScale = 0f)
        {
            if (duration <= 0f) return;
            if (Mathf.Approximately(Time.timeScale, 0f) && s_routine == null) return; // 일시정지 중이면 무시

            var runner = TimerRunner.Instance;
            if (runner == null) return;

            if (s_routine != null)
            {
                runner.StopCoroutine(s_routine);
                RestoreIfOwned();
            }
            s_routine = runner.StartCoroutine(Routine(duration, timeScale));
        }

        static IEnumerator Routine(float duration, float scale)
        {
            s_previousScale = Time.timeScale;
            s_appliedScale = Mathf.Clamp01(scale);
            Time.timeScale = s_appliedScale;
            yield return new WaitForSecondsRealtime(duration);
            RestoreIfOwned();
            s_routine = null;
        }

        static void RestoreIfOwned()
        {
            if (Mathf.Approximately(Time.timeScale, s_appliedScale))
                Time.timeScale = s_previousScale;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            s_routine = null;
            s_previousScale = 1f;
            s_appliedScale = 1f;
        }
    }
}
