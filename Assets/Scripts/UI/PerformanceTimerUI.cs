using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 공연 시간(제한시간) 바 UI. PerformanceTimeChanged 이벤트만 구독하므로
    /// PerformanceTimerSystem 을 직접 참조하지 않는다.
    ///
    /// Slider 컴포넌트를 쓰지 않는다 — Fill Area/Handle Slide Area 중첩 구조가 커스텀 스프라이트와
    /// 계속 기준점이 어긋나서, fillImage 와 handleRect 를 코드로 직접 갱신하는 방식으로 대체했다.
    ///
    /// - fillImage: Background 와 동일하게 부모에 꽉 채워 겹쳐두고, Image Type 을 Filled/Horizontal/Origin Left 로 설정.
    ///   fillAmount 만 갱신하므로 앵커가 어긋날 여지가 없다.
    /// - handleRect: 기타 아이콘. fillImage 와 같은 부모 밑에 두고 anchorMin/Max.x 를 진행률로 직접 옮긴다
    ///   (0 = 부모 좌측 끝, 1 = 부모 우측 끝. Slider 의 handleRect 이동 방식과 동일한 원리).
    /// - valueText: 남은 시간을 m:ss 형식으로 표시
    /// </summary>
    public class PerformanceTimerUI : MonoBehaviour
    {
        [SerializeField] Image fillImage;
        [SerializeField] RectTransform handleRect;
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
            float t = countDown ? 1f - normalized : normalized;

            if (fillImage != null) fillImage.fillAmount = t;

            if (handleRect != null)
            {
                handleRect.anchorMin = new Vector2(t, handleRect.anchorMin.y);
                handleRect.anchorMax = new Vector2(t, handleRect.anchorMax.y);
            }

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
