using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// MonoBehaviour 가 없는 정적 클래스에서도 코루틴을 돌리기 위한 전역 러너.
    /// 직접 씬에 배치할 필요 없이 필요할 때 자동 생성된다.
    /// </summary>
    public class TimerRunner : MonoSingleton<TimerRunner>
    {
        protected override void OnAwake() => gameObject.hideFlags = HideFlags.HideInHierarchy;
    }
}
