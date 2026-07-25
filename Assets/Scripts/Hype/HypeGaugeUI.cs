using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 호응도 게이지 UI. HypeChanged 이벤트만 구독하므로 HypeSystem 을 직접 참조하지 않는다.
    ///
    /// - 메인 바(fillImage): 현재 호응도를 즉시 반영
    /// - 지연 바(delayedImage): 뒤따라오며 감소량을 보여줌 (GameJamKit HealthBar 의 delayed bar 패턴)
    /// - 숫자 텍스트: 프로토타입에서는 밸런스 확인을 위해 숫자를 반드시 표시 (기획 §13)
    /// - 구간별 명도: 그레이스케일 프로토타입이므로 색이 아니라 밝기로 위험도를 전달 (기획 §8)
    ///
    /// 씬 배치는 Tools/Hype/Setup Hype Scene 메뉴가 자동으로 해준다.
    /// </summary>
    public class HypeGaugeUI : MonoBehaviour
    {
        [Header("연결 (Setup 메뉴가 자동 지정)")]
        [SerializeField] Image fillImage;
        [SerializeField, Tooltip("지연되어 따라오는 뒷배경 바")] Image delayedImage;
        [SerializeField] Text valueText;
        [SerializeField, Tooltip("현재 열기 점수 배율(×N) 표시. 비워두면 표시 안 함")] Text multiplierText;

        [Header("연출")]
        [SerializeField, Tooltip("지연 바가 따라오는 속도 (fillAmount/초)")]
        float delayedSpeed = 0.6f;

        [Header("구간별 명도 (그레이스케일. 아트 확정 후 교체 가능)")]
        [SerializeField, Min(0f), Tooltip("Chill 단계 안에서 위기 색을 사용할 상한")]
        float crisisUpperBound = 21f;
        [SerializeField, Tooltip("1~20 위기")] Color crisisColor = new Color(0.35f, 0.35f, 0.35f);
        [SerializeField, Tooltip("21~49 냉담")] Color coldColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField, Tooltip("50~79 열광")] Color hotColor = new Color(0.78f, 0.78f, 0.78f);
        [SerializeField, Tooltip("80~100 폭발 직전")] Color peakColor = Color.white;

        float _targetFill;   // 메인 바 목표값 (이벤트로 갱신)
        float _delayedFill;  // 지연 바 현재값 (Update 에서 목표를 따라감)

        void OnEnable()
        {
            EventBus.Subscribe<HypeChanged>(OnHypeChanged);

            // 씬 로드 직후 이벤트가 오기 전에도 현재값과 동기화 (HasInstance: 없는데 억지로 만들지 않기)
            if (HypeSystem.HasInstance)
                Refresh(HypeSystem.Instance.Current, HypeSystem.Instance.Normalized, true);
        }

        void OnDisable() => EventBus.Unsubscribe<HypeChanged>(OnHypeChanged);

        void OnHypeChanged(HypeChanged e) => Refresh(e.Value, e.Normalized, false);

        void Refresh(float value, float normalized, bool immediate)
        {
            _targetFill = normalized;
            if (immediate) _delayedFill = normalized;

            if (fillImage != null)
            {
                fillImage.fillAmount = normalized;
                fillImage.color = ZoneColor(value);
            }
            if (valueText != null)
                valueText.text = Mathf.RoundToInt(value).ToString();
            if (multiplierText != null)
                multiplierText.text = "×" + Hype.Multiplier.ToString("0.##"); // 현재 열기 배율
        }

        void Update()
        {
            if (delayedImage == null) return;

            // 지연 바는 즉시 점프하지 않고 따라온다 → 큰 감소가 눈에 보인다
            _delayedFill = Mathf.MoveTowards(_delayedFill, _targetFill, delayedSpeed * Time.deltaTime);
            delayedImage.fillAmount = _delayedFill;
        }

        /// <summary>호응도 구간 → 바 명도. 숫자를 안 봐도 밝기만으로 위험을 느끼게 한다.</summary>
        Color ZoneColor(float value)
        {
            if (value < crisisUpperBound) return crisisColor;

            HypeConfig config = HypeSystem.HasInstance ? HypeSystem.Instance.Config : null;
            if (config == null) return coldColor;

            return config.ResolveStage(value).Select(coldColor, hotColor, peakColor);
        }
    }
}
