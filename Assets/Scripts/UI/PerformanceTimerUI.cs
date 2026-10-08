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

        UIPixelBurstEmitter _pixelVfx;
        float _nextDustAt;
        AugmentCardVFX _extensionVfx;
        RectTransform _pulseRoot;
        Vector3 _baseScale;
        Quaternion _baseRotation;
        Color _baseFillColor;
        Color _baseTextColor;
        float _pulseStart = -1f;
        bool _pulsing;
        [Header("마지막 시간 경고 (BGM 속도는 유지)")]
        [SerializeField] bool urgencyVisual = true;
        float _remaining;
        bool _urgentCaptured;
        Vector3 _urgentScale;
        Color _urgentFillColor;
        Color _urgentTextColor;
        public RectTransform VisualAnchor => valueText != null
            ? valueText.rectTransform : handleRect != null ? handleRect : transform as RectTransform;

        void OnEnable()
        {
            if (!_urgentCaptured)
            {
                _urgentScale = transform.localScale;
                _urgentFillColor = fillImage != null ? fillImage.color : Color.white;
                _urgentTextColor = valueText != null ? valueText.color : Color.white;
                _urgentCaptured = true;
            }
            if (_pixelVfx == null)
            {
                _pixelVfx = GetComponent<UIPixelBurstEmitter>();
                if (_pixelVfx == null)
                    _pixelVfx = gameObject.AddComponent<UIPixelBurstEmitter>();
            }
            _nextDustAt = 0f;

            EventBus.Subscribe<PerformanceTimeChanged>(OnTimeChanged);
            EventBus.Subscribe<PerformanceTimeExtended>(OnTimeExtended);
            EventBus.Subscribe<GameStateChanged>(OnVisualStateChanged);

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

        void OnDisable()
        {
            EventBus.Unsubscribe<PerformanceTimeChanged>(OnTimeChanged);
            EventBus.Unsubscribe<PerformanceTimeExtended>(OnTimeExtended);
            EventBus.Unsubscribe<GameStateChanged>(OnVisualStateChanged);
            ResetExtensionVisual();
        }

        void OnDestroy()
        {
            if (_extensionVfx != null) Destroy(_extensionVfx.gameObject);
        }

        void OnVisualStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) ResetExtensionVisual();
        }

        void OnTimeExtended(PerformanceTimeExtended e)
        {
            if (e.Seconds <= 0f) return;
            RestorePulse();
            RestoreUrgency();
            if (_extensionVfx == null) _extensionVfx = AugmentCardVFX.Create(transform);
            if (_extensionVfx != null)
                _extensionVfx.FloatPluses(VisualAnchor, Mathf.RoundToInt(e.Seconds), 0.48f);
            if (!_pulsing)
            {
                _pulseRoot = transform as RectTransform;
                if (_pulseRoot == null) return;
                _baseScale = _pulseRoot.localScale;
                _baseRotation = _pulseRoot.localRotation;
                if (fillImage != null) _baseFillColor = fillImage.color;
                if (valueText != null) _baseTextColor = valueText.color;
            }
            _pulsing = true;
            _pulseStart = Time.unscaledTime + 0.48f;
        }

        void Update()
        {
            if (!_pulsing || _pulseRoot == null)
            {
                UpdateUrgency();
                return;
            }
            float elapsed = Time.unscaledTime - _pulseStart;
            if (elapsed < 0f) return;
            float t = Mathf.Clamp01(elapsed / 0.85f);
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 2f)) * (1f - t);
            _pulseRoot.localScale = _baseScale * (1f + envelope * 0.2f);
            _pulseRoot.localRotation = _baseRotation * Quaternion.Euler(0f, 0f,
                Mathf.Sin(t * Mathf.PI * 8f) * 4f * (1f - t));
            Color flash = Color.Lerp(Color.white, AugmentCardVFX.Gold,
                0.5f + Mathf.Sin(elapsed * 24f) * 0.5f);
            if (fillImage != null) fillImage.color = Color.Lerp(_baseFillColor, flash, (1f - t) * 0.8f);
            if (valueText != null) valueText.color = Color.Lerp(_baseTextColor, flash, 1f - t);
            if (t >= 1f) RestorePulse();
        }

        void RestorePulse()
        {
            if (!_pulsing) return;
            if (_pulseRoot != null)
            {
                _pulseRoot.localScale = _baseScale;
                _pulseRoot.localRotation = _baseRotation;
            }
            if (fillImage != null) fillImage.color = _baseFillColor;
            if (valueText != null) valueText.color = _baseTextColor;
            _pulsing = false;
        }

        void ResetExtensionVisual()
        {
            RestorePulse();
            RestoreUrgency();
            if (_extensionVfx != null) _extensionVfx.Clear();
        }

        void RestoreUrgency()
        {
            if (!_urgentCaptured) return;
            transform.localScale = _urgentScale;
            if (fillImage != null) fillImage.color = _urgentFillColor;
            if (valueText != null) valueText.color = _urgentTextColor;
        }

        void UpdateUrgency()
        {
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;
            if (!urgencyVisual || _remaining <= 0f || _remaining > 15f || TutorialFlow.IsRunning)
            {
                RestoreUrgency();
                return;
            }
            bool critical = _remaining <= 5f;
            float pulse = 0.5f + Mathf.Sin(Time.time * (critical ? 12f : 6f)) * 0.5f;
            transform.localScale = _urgentScale * (1f + pulse * (critical ? 0.09f : 0.035f));
            Color warning = critical ? new Color32(0xEA, 0x4F, 0x36, 255) : new Color32(0xF9, 0xC2, 0x2B, 255);
            if (fillImage != null) fillImage.color = Color.Lerp(_urgentFillColor, warning, pulse * 0.7f);
            if (valueText != null) valueText.color = Color.Lerp(_urgentTextColor, warning, pulse * 0.85f);
        }

        void OnTimeChanged(PerformanceTimeChanged e) => Refresh(e.Elapsed, e.Duration, e.Normalized);

        void Refresh(float elapsed, float duration, float normalized)
        {
            _remaining = Mathf.Max(0f, duration - elapsed);
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

            EmitTimerDust(elapsed, duration);
        }

        void EmitTimerDust(float elapsed, float duration)
        {
            if (_pixelVfx == null || handleRect == null || duration <= 0f) return;
            if (!GameManager.HasInstance || !GameManager.Instance.IsPlaying) return;
            if (TutorialFlow.IsRunning) return;

            float remaining = Mathf.Max(0f, duration - elapsed);
            if (remaining <= 0f || Time.unscaledTime < _nextDustAt) return;

            float interval = remaining <= 5f ? 0.06f : remaining <= 15f ? 0.08f : 0.12f;
            _nextDustAt = Time.unscaledTime + interval;

            Color color = remaining <= 5f
                ? new Color32(0xEA, 0x4F, 0x36, 0xE6)
                : remaining <= 15f
                    ? new Color32(0xF9, 0xC2, 0x2B, 0xD9)
                    : new Color32(0xC7, 0xDC, 0xD0, 0xA6);
            _pixelVfx.EmitDust(handleRect, color, remaining <= 5f ? 2 : 1);
        }
    }
}
