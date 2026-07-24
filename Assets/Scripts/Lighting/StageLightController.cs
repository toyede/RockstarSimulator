using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextStage
{
    /// <summary>
    /// 열기 단계(HeatStage) 하나에 대한 조명 성격.
    /// 색상뿐 아니라 강도·펄스까지 묶어서 "이 단계는 이렇게 보인다"를 한 덩어리로 관리한다.
    /// </summary>
    [System.Serializable]
    public class StageLightPreset
    {
        [Tooltip("이 단계의 조명 색")]
        public Color color = Color.white;

        [Tooltip("Global Light 강도. 화면 전체 밝기라 단계 차이를 크게 주지 않는다")]
        [Range(0f, 2f)] public float globalIntensity = 0.7f;

        [Tooltip("좌우 무대 조명의 기준 강도")]
        [Range(0f, 3f)] public float stageBaseIntensity = 1f;

        [Tooltip("무대 조명 펄스 속도. 클수록 빠르게 뛴다")]
        public float pulseSpeed = 1.5f;

        [Tooltip("무대 조명 펄스 폭. 클수록 밝기 차이가 크다")]
        public float pulseAmplitude = 0.1f;
    }

    /// <summary>
    /// 무대 조명 담당. URP 2D Light 3개(Global 1 + 좌우 2)만 사용한다.
    ///
    /// - 열기 단계에 따라 색·강도·펄스가 바뀌고, 단계 전환은 0.35초 동안 보간된다
    /// - 좌우 조명은 위상을 어긋나게 해 같은 타이밍으로 뛰지 않는다
    /// - Special Hit 순간 Global Light 를 짧게 번쩍인다 (별도 조명을 만들지 않는다)
    ///
    /// 이 클래스는 <b>어떤 시스템도 구독하지 않는다.</b> 열기·특별 관객과의 연결은
    /// StageLightEventBridge 가 담당하므로, 이 컴포넌트만 두고 ContextMenu 로 단독 테스트할 수 있다.
    ///
    /// 코루틴을 쓰지 않고 Update 에서 상태를 계산한다:
    /// 매 프레임 "현재 단계 값"으로부터 최종 색·강도를 다시 만들기 때문에
    /// 플래시가 중간에 겹치거나 컴포넌트가 꺼져도 강도가 영구적으로 남는 일이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageLightController : MonoBehaviour
    {
        [Header("조명 참조 (없으면 경고만 하고 계속 동작)")]
        [SerializeField, Tooltip("화면 전체를 덮는 Global Light 2D. 플래시도 이 조명을 쓴다")]
        Light2D globalLight;

        [SerializeField, Tooltip("무대 왼쪽 조명")]
        Light2D leftStageLight;

        [SerializeField, Tooltip("무대 오른쪽 조명")]
        Light2D rightStageLight;

        [Header("픽셀 라이트")]
        [SerializeField, Tooltip("좌우 Point Light에 저해상도 Point 필터 쿠키를 적용한다")]
        bool usePixelLightStyle = true;

        [SerializeField, Tooltip("비워두면 Resources/Lighting/PixelStageLightCookie를 자동으로 사용한다")]
        Sprite pixelLightCookie;

        [SerializeField, Range(0f, 1f), Tooltip("높을수록 빛 경계가 단단해져 픽셀 단계가 선명하다")]
        float pixelLightFalloff = 0.95f;

        [SerializeField, Range(0f, 1f), Tooltip("픽셀 라이트용 그림자 부드러움. 낮을수록 경계가 또렷하다")]
        float pixelShadowSoftness = 0.05f;

        [Header("픽셀 스포트라이트 셰이더")]
        [SerializeField, Tooltip("월드 픽셀 격자에서 밝기와 조사각을 계산하는 스포트라이트를 사용한다")]
        bool usePixelSpotlightShader = true;

        [SerializeField, Range(1f, 256f), Tooltip("월드 1유닛당 픽셀 수. 현재 스프라이트 PPU에 맞춘다")]
        float spotlightPixelsPerUnit = 100f;

        [SerializeField, Range(2, 16), Tooltip("스포트라이트 밝기 단계 수")]
        int spotlightBandCount = 6;

        [SerializeField, Range(0f, 1f), Tooltip("픽셀 밝기 단계 사이를 월드 고정 디더로 섞는 정도")]
        float spotlightDitherStrength = 0.18f;

        [SerializeField, Range(0f, 2f), Tooltip("픽셀 조명 패스의 최종 강도")]
        float pixelSpotlightIntensity = 0.06f;

        [SerializeField, Range(0f, 1f), Tooltip("그림자를 위해 남겨둘 기존 Light2D 조명의 비율")]
        float smoothLightContribution = 0.15f;

        [Header("스포트라이트 움직임")]
        [SerializeField, Tooltip("좌우 조명이 각자의 기준 방향을 중심으로 무대를 훑는다")]
        bool animateSpotlights = true;

        [SerializeField, Range(0f, 45f), Tooltip("기준 방향에서 좌우로 움직이는 최대 각도")]
        float spotlightSweepDegrees = 15f;

        [SerializeField, Range(0.01f, 2f), Tooltip("기본 회전 속도. 열기 단계에 따라 자동으로 빨라진다")]
        float spotlightSweepSpeed = 0.22f;

        [SerializeField, Tooltip("Spot Light의 최소·최대 Outer Angle")]
        Vector2 spotlightOuterAngleRange = new Vector2(36f, 50f);

        [SerializeField, Range(0.1f, 0.95f), Tooltip("Outer Angle에 대한 Inner Angle 비율")]
        float spotlightInnerAngleRatio = 0.55f;

        [Header("단계별 프리셋")]
        [SerializeField, Tooltip("차분한 단계")]
        StageLightPreset chill = new StageLightPreset
        {
            color = new Color32(0x31, 0xDF, 0xEA, 0xFF),
            globalIntensity = 0.55f, stageBaseIntensity = 0.75f,
            pulseSpeed = 0.6f, pulseAmplitude = 0.04f,
        };

        [SerializeField, Tooltip("함께 부르는 단계")]
        StageLightPreset singalong = new StageLightPreset
        {
            color = new Color32(0x64, 0x31, 0xEA, 0xFF),
            globalIntensity = 0.70f, stageBaseIntensity = 1.00f,
            pulseSpeed = 1.5f, pulseAmplitude = 0.10f,
        };

        [SerializeField, Tooltip("가장 격렬한 단계")]
        StageLightPreset mosh = new StageLightPreset
        {
            color = new Color32(0xF0, 0x1F, 0x1F, 0xFF),
            globalIntensity = 0.80f, stageBaseIntensity = 1.25f,
            pulseSpeed = 3.0f, pulseAmplitude = 0.18f,
        };

        [Header("전환")]
        [SerializeField, Tooltip("단계가 바뀔 때 보간되는 시간(초)")]
        float transitionDuration = 0.35f;

        [Header("펄스")]
        [SerializeField, Tooltip("오른쪽 조명의 위상 차이. 0이면 좌우가 완전히 같이 뛴다")]
        float rightPulsePhaseOffset = 1.5f;

        [SerializeField, Tooltip("스프라이트가 하얗게 타지 않도록 무대 조명 강도 상한")]
        [Range(0.5f, 4f)] float stageIntensityCeiling = 2f;

        [Header("Special Hit 플래시")]
        [SerializeField, Tooltip("플래시 정점의 Global Light 강도")]
        float flashPeakIntensity = 1.8f;

        [SerializeField, Tooltip("치솟는 시간(초)")] float flashInDuration = 0.04f;
        [SerializeField, Tooltip("유지 시간(초)")] float flashHoldDuration = 0.06f;
        [SerializeField, Tooltip("돌아오는 시간(초)")] float flashOutDuration = 0.15f;

        [SerializeField, Tooltip("색을 지정하지 않고 플래시할 때 쓰는 색")]
        Color defaultFlashColor = Color.white;

        [Header("디버그")]
        [SerializeField, Tooltip("7/8/9 로 단계 변경, 0 으로 플래시 (에디터·개발 빌드 전용)")]
        bool enableDebugKeys = false;

        // ---------------- 상태 ----------------

        HeatStage _stage = HeatStage.Chill;

        // 단계 전환: from → to 를 _blend(0~1)로 섞는다. 코루틴이 아니라 값 보간이라 중단·재시작이 안전하다.
        StageLightPreset _from;
        StageLightPreset _to;
        float _blend = 1f;

        // 플래시: 경과 시간만 들고 있고 매 프레임 강도를 다시 만든다 (겹쳐도 값이 남지 않는다)
        bool _flashing;
        float _flashElapsed;
        Color _flashColor = Color.white;

        bool _warnedMissingLights;
        bool _warnedMissingPixelCookie;
        PixelSpotlight2D _leftPixelSpotlight;
        PixelSpotlight2D _rightPixelSpotlight;
        Quaternion _leftSpotlightRestRotation;
        Quaternion _rightSpotlightRestRotation;
        float _leftSpotlightRestInnerAngle;
        float _leftSpotlightRestOuterAngle;
        float _rightSpotlightRestInnerAngle;
        float _rightSpotlightRestOuterAngle;
        bool _spotlightPoseCaptured;

        public HeatStage CurrentStage => _stage;
        public bool IsFlashing => _flashing;

        float FlashTotal => Mathf.Max(0.0001f, flashInDuration + flashHoldDuration + flashOutDuration);

        void Awake()
        {
            EnsurePresets();
            ApplyPixelLightStyle();
            CaptureSpotlightPose();
            EnsurePixelSpotlights();
        }

        /// <summary>
        /// 프리셋 참조를 보장한다.
        ///
        /// Awake 에서만 채우면 안 된다 — 같은 오브젝트에 붙은 다른 컴포넌트(StageLightEventBridge)의
        /// OnEnable 이 이 컴포넌트의 Awake 보다 먼저 도는 경우가 있어서, 그때 SetHeatStage 가
        /// 들어오면 null 참조가 난다. 값을 쓰기 직전마다 여기를 거치게 해서 순서에 의존하지 않는다.
        /// </summary>
        void EnsurePresets()
        {
            if (_to == null) { _to = PresetFor(_stage); _blend = 1f; }
            if (_from == null) _from = _to;
        }

        void OnEnable()
        {
            // 꺼졌다 켜져도 현재 단계 상태로 즉시 복구한다 (플래시 잔상 없음)
            _flashing = false;
            _flashElapsed = 0f;
            CaptureSpotlightPose();
            EnsurePixelSpotlights();
            ApplyLights();
        }

        void OnDisable()
        {
            // Update 기반이라 정리할 코루틴이 없다. 조명만 현재 단계 값으로 되돌려 둔다.
            _flashing = false;
            _blend = 1f;
            _from = _to = PresetFor(_stage);
            RestoreSpotlightPose();
            ApplyLights();
        }

        // ---------------- 공개 API ----------------

        /// <summary>열기 단계를 바꾼다. 같은 단계면 아무 일도 하지 않는다.</summary>
        public void SetHeatStage(HeatStage stage) => SetHeatStage(stage, instant: false);

        /// <summary>instant 를 켜면 보간 없이 즉시 적용한다. (공연 시작·리셋용)</summary>
        public void SetHeatStage(HeatStage stage, bool instant)
        {
            EnsurePresets();
            var next = PresetFor(stage);

            // 같은 단계로 다시 들어오면 전환을 새로 시작하지 않는다
            if (_stage == stage && !instant && _blend >= 1f) return;

            // 전환 도중에 새 단계가 오면 "지금 보이는 값"에서 출발해 튀지 않게 한다
            _from = _blend >= 1f ? _to : Lerp(_from, _to, _blend);
            _to = next;
            _stage = stage;
            _blend = instant || transitionDuration <= 0f ? 1f : 0f;

            ApplyLights();
        }

        /// <summary>Special Hit 플래시. 기본 색(흰색)으로 번쩍인다.</summary>
        public void PlaySpecialHitFlash() => PlaySpecialHitFlash(defaultFlashColor);

        /// <summary>요청 타입 색으로 번쩍인다. 이미 플래시 중이면 처음부터 다시 시작한다.</summary>
        public void PlaySpecialHitFlash(Color requestColor)
        {
            _flashColor = requestColor;
            _flashElapsed = 0f;
            _flashing = true; // 중복 호출은 타이머만 리셋 → 강도가 누적되지 않는다
        }

        /// <summary>열기 단계 색을 그대로 써서 플래시한다. (Special Hit 요청 타입 연출용)</summary>
        public void PlaySpecialHitFlash(HeatStage requestStage) => PlaySpecialHitFlash(PresetFor(requestStage).color);

        /// <summary>런타임에 조명을 갈아끼울 때. (셋업 툴·씬 재구성용)</summary>
        public void BindLights(Light2D global, Light2D left, Light2D right)
        {
            globalLight = global;
            leftStageLight = left;
            rightStageLight = right;
            _warnedMissingLights = false;
            ApplyPixelLightStyle();
            _spotlightPoseCaptured = false;
            CaptureSpotlightPose();
            EnsurePixelSpotlights();
            ApplyLights();
        }

        /// <summary>
        /// 좌우 Point Light의 기존 각도·범위·색은 유지하고 픽셀 쿠키와 단단한 경계만 적용한다.
        /// </summary>
        public void ApplyPixelLightStyle()
        {
            if (!usePixelLightStyle) return;

            if (pixelLightCookie == null)
                pixelLightCookie = Resources.Load<Sprite>("Lighting/PixelStageLightCookie");

            if (pixelLightCookie == null)
            {
                if (!_warnedMissingPixelCookie)
                {
                    _warnedMissingPixelCookie = true;
                    Debug.LogWarning(
                        "[StageLight] 픽셀 라이트 쿠키가 없습니다. " +
                        "Tools/Lighting/Apply Pixel Stage Light Style을 실행하세요.",
                        this);
                }
                return;
            }

            _warnedMissingPixelCookie = false;
            ApplyPixelStyle(leftStageLight);
            ApplyPixelStyle(rightStageLight);
        }

        void ApplyPixelStyle(Light2D light)
        {
            if (light == null || light.lightType != Light2D.LightType.Point) return;

            light.lightCookieSprite = pixelLightCookie;
            light.falloffIntensity = pixelLightFalloff;
            light.shadowSoftness = pixelShadowSoftness;
            light.shadowSoftnessFalloffIntensity = pixelShadowSoftness;
        }

        void EnsurePixelSpotlights()
        {
            if (!Application.isPlaying || !usePixelSpotlightShader) return;

            _leftPixelSpotlight = EnsurePixelSpotlight(leftStageLight);
            _rightPixelSpotlight = EnsurePixelSpotlight(rightStageLight);
        }

        PixelSpotlight2D EnsurePixelSpotlight(Light2D light)
        {
            if (light == null || light.lightType != Light2D.LightType.Point) return null;

            var pixelSpotlight = light.GetComponent<PixelSpotlight2D>();
            if (pixelSpotlight == null)
                pixelSpotlight = light.gameObject.AddComponent<PixelSpotlight2D>();

            pixelSpotlight.Configure(
                spotlightPixelsPerUnit,
                spotlightBandCount,
                spotlightDitherStrength);
            return pixelSpotlight;
        }

        void CaptureSpotlightPose()
        {
            if (_spotlightPoseCaptured || leftStageLight == null || rightStageLight == null) return;

            _leftSpotlightRestRotation = leftStageLight.transform.localRotation;
            _rightSpotlightRestRotation = rightStageLight.transform.localRotation;
            _leftSpotlightRestInnerAngle = leftStageLight.pointLightInnerAngle;
            _leftSpotlightRestOuterAngle = leftStageLight.pointLightOuterAngle;
            _rightSpotlightRestInnerAngle = rightStageLight.pointLightInnerAngle;
            _rightSpotlightRestOuterAngle = rightStageLight.pointLightOuterAngle;
            _spotlightPoseCaptured = true;
        }

        void RestoreSpotlightPose()
        {
            if (!_spotlightPoseCaptured) return;

            if (leftStageLight != null)
            {
                leftStageLight.transform.localRotation = _leftSpotlightRestRotation;
                leftStageLight.pointLightInnerAngle = _leftSpotlightRestInnerAngle;
                leftStageLight.pointLightOuterAngle = _leftSpotlightRestOuterAngle;
            }

            if (rightStageLight != null)
            {
                rightStageLight.transform.localRotation = _rightSpotlightRestRotation;
                rightStageLight.pointLightInnerAngle = _rightSpotlightRestInnerAngle;
                rightStageLight.pointLightOuterAngle = _rightSpotlightRestOuterAngle;
            }
        }

        void UpdateSpotlightMotion()
        {
            if (!animateSpotlights || !_spotlightPoseCaptured) return;

            float stageSpeedMultiplier = _stage switch
            {
                HeatStage.Mosh => 1.65f,
                HeatStage.Singalong => 1f,
                _ => 0.6f,
            };
            float time = Time.time * spotlightSweepSpeed * stageSpeedMultiplier * Mathf.PI * 2f;

            float leftWave =
                Mathf.Sin(time) * 0.78f +
                Mathf.Sin(time * 0.43f + 1.1f) * 0.22f;
            float rightWave =
                Mathf.Sin(time * 0.91f + Mathf.PI) * 0.76f +
                Mathf.Sin(time * 0.37f + 2.4f) * 0.24f;

            if (leftStageLight != null)
            {
                leftStageLight.transform.localRotation =
                    _leftSpotlightRestRotation *
                    Quaternion.Euler(0f, 0f, leftWave * spotlightSweepDegrees);
            }

            if (rightStageLight != null)
            {
                rightStageLight.transform.localRotation =
                    _rightSpotlightRestRotation *
                    Quaternion.Euler(0f, 0f, rightWave * spotlightSweepDegrees);
            }

            float minimumOuterAngle = Mathf.Clamp(
                Mathf.Min(spotlightOuterAngleRange.x, spotlightOuterAngleRange.y),
                1f,
                360f);
            float maximumOuterAngle = Mathf.Clamp(
                Mathf.Max(spotlightOuterAngleRange.x, spotlightOuterAngleRange.y),
                minimumOuterAngle,
                360f);
            float leftWidth = 0.5f + Mathf.Sin(time * 0.58f + 0.4f) * 0.5f;
            float rightWidth = 0.5f + Mathf.Sin(time * 0.63f + 2.2f) * 0.5f;

            ApplySpotlightWidth(leftStageLight, Mathf.Lerp(minimumOuterAngle, maximumOuterAngle, leftWidth));
            ApplySpotlightWidth(rightStageLight, Mathf.Lerp(minimumOuterAngle, maximumOuterAngle, rightWidth));
        }

        void ApplySpotlightWidth(Light2D light, float outerAngle)
        {
            if (light == null) return;

            light.pointLightOuterAngle = outerAngle;
            light.pointLightInnerAngle = outerAngle * spotlightInnerAngleRatio;
        }

        // ---------------- 매 프레임 ----------------

        void Update()
        {
            if (_blend < 1f && transitionDuration > 0f)
                _blend = Mathf.Min(1f, _blend + Time.deltaTime / transitionDuration);

            if (_flashing)
            {
                _flashElapsed += Time.deltaTime;
                if (_flashElapsed >= FlashTotal) { _flashing = false; _flashElapsed = 0f; }
            }

            HandleDebugKeys();
            UpdateSpotlightMotion();
            ApplyLights();
        }

        /// <summary>
        /// 매 프레임 현재 단계 값으로부터 최종 색·강도를 <b>새로 만든다.</b>
        /// 이전 프레임 값을 누적하지 않기 때문에 플래시가 끝나면 반드시 단계 색으로 정확히 돌아온다.
        /// </summary>
        void ApplyLights()
        {
            EnsurePresets();

            if (globalLight == null && leftStageLight == null && rightStageLight == null)
            {
                WarnMissingLightsOnce();
                return;
            }

            float blend = Mathf.Clamp01(_blend);
            Color stageColor = Color.Lerp(_from.color, _to.color, blend);
            float globalIntensity = Mathf.Lerp(_from.globalIntensity, _to.globalIntensity, blend);
            float baseIntensity = Mathf.Lerp(_from.stageBaseIntensity, _to.stageBaseIntensity, blend);
            float pulseSpeed = Mathf.Lerp(_from.pulseSpeed, _to.pulseSpeed, blend);
            float pulseAmplitude = Mathf.Lerp(_from.pulseAmplitude, _to.pulseAmplitude, blend);

            // --- Global: 단계 값 + (플래시 중이면) 덮어쓰기. 펄스는 넣지 않는다(UI 가독성) ---
            if (globalLight != null)
            {
                Color color = stageColor;
                float intensity = globalIntensity;

                if (_flashing)
                {
                    float k = FlashCurve(_flashElapsed);
                    color = Color.Lerp(stageColor, _flashColor, k);
                    intensity = Mathf.Lerp(globalIntensity, flashPeakIntensity, k);
                }

                globalLight.color = color;
                globalLight.intensity = intensity;
            }

            // --- 좌우 무대 조명: 강도만 펄스. 오른쪽은 위상을 어긋나게 한다 ---
            ApplyStageLight(leftStageLight, stageColor, baseIntensity, pulseSpeed, pulseAmplitude, 0f);
            ApplyStageLight(rightStageLight, stageColor, baseIntensity, pulseSpeed, pulseAmplitude, rightPulsePhaseOffset);
        }

        void ApplyStageLight(Light2D light, Color color, float baseIntensity, float speed, float amplitude, float phase)
        {
            if (light == null) return;

            float pulse = Mathf.Sin(Time.time * speed + phase);
            float finalIntensity = Mathf.Clamp(
                baseIntensity + pulse * amplitude,
                0f,
                stageIntensityCeiling);
            light.color = color;
            light.intensity = usePixelSpotlightShader
                ? finalIntensity * smoothLightContribution
                : finalIntensity;

            PixelSpotlight2D pixelSpotlight =
                light == leftStageLight ? _leftPixelSpotlight :
                light == rightStageLight ? _rightPixelSpotlight :
                null;
            pixelSpotlight?.SetVisualLight(
                color,
                usePixelSpotlightShader ? finalIntensity * pixelSpotlightIntensity : 0f);
        }

        /// <summary>플래시 곡선. 0 → 1(치솟음) → 1(유지) → 0(복귀).</summary>
        float FlashCurve(float elapsed)
        {
            if (elapsed < flashInDuration)
                return flashInDuration <= 0f ? 1f : elapsed / flashInDuration;

            float held = elapsed - flashInDuration;
            if (held < flashHoldDuration) return 1f;

            float out_ = held - flashHoldDuration;
            if (flashOutDuration <= 0f) return 0f;
            return Mathf.Clamp01(1f - out_ / flashOutDuration);
        }

        StageLightPreset PresetFor(HeatStage stage)
        {
            switch (stage)
            {
                case HeatStage.Singalong: return singalong;
                case HeatStage.Mosh:      return mosh;
                default:                  return chill;
            }
        }

        /// <summary>전환 도중 새 단계가 들어왔을 때 "지금 보이는 값"을 출발점으로 만든다.</summary>
        static StageLightPreset Lerp(StageLightPreset a, StageLightPreset b, float t) => new StageLightPreset
        {
            color = Color.Lerp(a.color, b.color, t),
            globalIntensity = Mathf.Lerp(a.globalIntensity, b.globalIntensity, t),
            stageBaseIntensity = Mathf.Lerp(a.stageBaseIntensity, b.stageBaseIntensity, t),
            pulseSpeed = Mathf.Lerp(a.pulseSpeed, b.pulseSpeed, t),
            pulseAmplitude = Mathf.Lerp(a.pulseAmplitude, b.pulseAmplitude, t),
        };

        void WarnMissingLightsOnce()
        {
            if (_warnedMissingLights) return;
            _warnedMissingLights = true;
            Debug.LogWarning("[StageLight] Light2D 참조가 하나도 없습니다. " +
                             "Tools/Lighting/Setup Stage Lighting 을 실행하거나 인스펙터에서 연결하세요.");
        }

        // ---------------- 디버그 ----------------

        void HandleDebugKeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!enableDebugKeys) return;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.digit7Key.wasPressedThisFrame) SetHeatStage(HeatStage.Chill);
            if (kb.digit8Key.wasPressedThisFrame) SetHeatStage(HeatStage.Singalong);
            if (kb.digit9Key.wasPressedThisFrame) SetHeatStage(HeatStage.Mosh);
            if (kb.digit0Key.wasPressedThisFrame) PlaySpecialHitFlash();
#else
            if (Input.GetKeyDown(KeyCode.Alpha7)) SetHeatStage(HeatStage.Chill);
            if (Input.GetKeyDown(KeyCode.Alpha8)) SetHeatStage(HeatStage.Singalong);
            if (Input.GetKeyDown(KeyCode.Alpha9)) SetHeatStage(HeatStage.Mosh);
            if (Input.GetKeyDown(KeyCode.Alpha0)) PlaySpecialHitFlash();
#endif
#endif
        }

        // Play 중이 아니면 Update 가 돌지 않아 보간이 진행되지 않는다 → 에디터에서는 즉시 적용한다.
        // (0.35초 전환을 눈으로 보려면 Play Mode 에서 눌러야 한다)
        bool DebugInstant => !Application.isPlaying;

        [ContextMenu("Debug/Set Chill")]
        void DebugSetChill() => SetHeatStage(HeatStage.Chill, DebugInstant);

        [ContextMenu("Debug/Set Singalong")]
        void DebugSetSingalong() => SetHeatStage(HeatStage.Singalong, DebugInstant);

        [ContextMenu("Debug/Set Mosh")]
        void DebugSetMosh() => SetHeatStage(HeatStage.Mosh, DebugInstant);

        [ContextMenu("Debug/Play Special Hit Flash")]
        void DebugPlaySpecialHitFlash() => PlaySpecialHitFlash();
    }
}
