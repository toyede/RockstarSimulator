using UnityEngine;
using UnityEngine.UI;

namespace GameJamKit
{
    /// <summary>
    /// Health 를 Image(Filled) 에 연결하는 체력바.
    /// fillImage 의 Image Type 을 Filled 로 설정해 두고 target 에 Health 를 넣으면 끝.
    /// 월드 스페이스 캔버스(적 머리 위 바)에도 그대로 사용 가능.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] Health target;
        [SerializeField] Image fillImage;
        [SerializeField, Tooltip("지연되어 따라오는 뒷배경 바 (선택)")] Image delayedImage;
        [SerializeField] float delayedSpeed = 1.5f;
        [SerializeField, Tooltip("가득 찼을 때 바를 숨긴다")] bool hideWhenFull = false;

        float _delayed = 1f;

        void Awake()
        {
            if (target == null) target = GetComponentInParent<Health>();
        }

        void OnEnable()
        {
            if (target == null) return;
            target.OnDamaged += HandleChanged;
            target.OnHealed += HandleHealed;
            Refresh(true);
        }

        void OnDisable()
        {
            if (target == null) return;
            target.OnDamaged -= HandleChanged;
            target.OnHealed -= HandleHealed;
        }

        void HandleChanged(DamageInfo _) => Refresh(false);
        void HandleHealed(float _) => Refresh(false);

        void Refresh(bool immediate)
        {
            if (target == null || fillImage == null) return;

            float value = target.Normalized;
            fillImage.fillAmount = value;
            if (immediate) _delayed = value;
            if (hideWhenFull) fillImage.enabled = value < 0.999f;
        }

        void Update()
        {
            if (target == null) return;

            // Health 를 거치지 않는 직접 수정에도 대응하도록 매 프레임 동기화
            if (fillImage != null && !Mathf.Approximately(fillImage.fillAmount, target.Normalized))
                Refresh(false);

            if (delayedImage == null) return;
            _delayed = Mathf.MoveTowards(_delayed, target.Normalized, delayedSpeed * Time.deltaTime);
            delayedImage.fillAmount = _delayed;
        }
    }
}
