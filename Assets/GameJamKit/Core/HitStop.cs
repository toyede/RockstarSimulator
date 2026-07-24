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

        public static void Do(float duration = 0.05f, float timeScale = 0f)
        {
            if (Mathf.Approximately(Time.timeScale, 0f) && s_routine == null) return; // 일시정지 중이면 무시

            var runner = TimerRunner.Instance;
            if (runner == null) return;

            if (s_routine != null) runner.StopCoroutine(s_routine);
            s_routine = runner.StartCoroutine(Routine(duration, timeScale));
        }

        static IEnumerator Routine(float duration, float scale)
        {
            float original = 1f;
            Time.timeScale = Mathf.Clamp01(scale);
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = original;
            s_routine = null;
        }
    }
}
