using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 공연 시간(제한시간) 바 UI. PerformanceTimeChanged 이벤트만 구독하므로
    /// PerformanceTimerSystem 을 직접 참조하지 않는다.
    ///
    /// - fillImage: 경과 시간 비율만큼 차오르거나(기본) 줄어든다 — countDown 으로 인스펙터에서 전환
    /// - valueText: 남은 시간을 m:ss 형식으로 표시
    ///
    /// 씬 배치는 Tools/Hype/Setup Hype Scene 메뉴가 자동으로 해준다.
    /// </summary>
    public class PerformanceTimerUI : MonoBehaviour
    {
        [SerializeField] Image fillImage;
        [SerializeField] Text valueText;

        [Header("표시 방식")]
        [SerializeField, Tooltip("켜면 가득 찬 상태로 시작해 시간이 지날수록 줄어든다. 끄면(기본) 빈 상태로 시작해 차오른다.")]
        bool countDown = false;

        void OnEnable()
        {
            EventBus.Subscribe<PerformanceTimeChanged>(OnTimeChanged);

            // 씬 로드 직후 이벤트가 오기 전에도 현재값과 동기화
            if (PerformanceTimerSystem.HasInstance)
            {
                var t = PerformanceTimerSystem.Instance;
                Refresh(t.Elapsed, t.Duration, t.Normalized);
            }
            else
            {
                Refresh(0f, 0f, 0f);
            }
        }

        void OnDisable() => EventBus.Unsubscribe<PerformanceTimeChanged>(OnTimeChanged);

        void OnTimeChanged(PerformanceTimeChanged e) => Refresh(e.Elapsed, e.Duration, e.Normalized);

        void Refresh(float elapsed, float duration, float normalized)
        {
            if (fillImage != null) fillImage.fillAmount = countDown ? 1f - normalized : normalized;

            if (valueText != null)
            {
                float remaining = Mathf.Max(0f, duration - elapsed);
                int minutes = Mathf.FloorToInt(remaining / 60f);
                int seconds = Mathf.FloorToInt(remaining % 60f);
                valueText.text = $"{minutes}:{seconds:00}";
            }
        }
    }
}
