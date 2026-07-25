using GameJamKit;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextStage
{
    /// <summary>
    /// 피버타임 동안 화면 중앙 상단에서 아래를 향해 깜빡이는 노란 픽셀 조명.
    ///
    /// 무대 조명과 <b>같은 표현</b>을 쓴다 — Light2D(Point) + <see cref="PixelSpotlight2D"/> 조합에
    /// StageLightController 와 같은 픽셀 격자·밴드 수·디더 값을 넣는다. 그래서 이질감이 없다.
    ///
    /// 무대 조명(StageLightController)을 전혀 건드리지 않는다. 별도 Light2D 를 하나 더 두고
    /// 피버 동안에만 켜므로, 피버가 끝나면 무대 색이 원래대로 남는다.
    ///
    /// 깜빡임은 코루틴이 아니라 경과 시간으로 매 프레임 강도를 다시 만든다.
    /// 피버가 중간에 끊겨도 강도가 켜진 채로 남지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light2D))]
    public sealed class FeverSpotlight : MonoBehaviour
    {
        [Header("색·밝기")]
        [SerializeField, Tooltip("피버 조명 색")]
        Color color = new Color(1f, 0.85f, 0.15f, 1f);

        [SerializeField, Min(0f), Tooltip("깜빡임의 최대 밝기")]
        float peakIntensity = 1.6f;

        [SerializeField, Min(0f), Tooltip("깜빡임의 최소 밝기. 0이면 완전히 꺼졌다 켜진다")]
        float minIntensity = 0f;

        [Header("깜빡임")]
        [SerializeField, Min(0.01f), Tooltip("초당 깜빡이는 횟수")]
        float blinksPerSecond = 3.5f;

        [SerializeField, Tooltip(
            "체크하면 딱딱 끊어지게 깜빡이고(픽셀아트에 어울린다), " +
            "끄면 사인파로 부드럽게 밝아졌다 어두워진다")]
        bool hardBlink = true;

        [SerializeField, Range(0.05f, 0.95f), Tooltip("딱딱한 깜빡임에서 켜져 있는 비율")]
        float dutyCycle = 0.5f;

        [SerializeField, Min(0.01f), Tooltip("피버 시작·종료 시 밝기가 붙고 떨어지는 시간(초)")]
        float rampDuration = 0.15f;

        [Header("배치")]
        [SerializeField, Tooltip(
            "켜면 시작할 때 카메라 화면 상단 중앙으로 스스로 이동하고 아래를 향한다. " +
            "끄면 씬에 놓은 위치·회전을 그대로 쓴다")]
        bool autoPlaceAtCameraTop = true;

        [SerializeField, Tooltip("화면 상단에서 얼마나 더 위에 둘지(월드 유닛)")]
        float aboveScreenMargin = 0.5f;

        [Header("픽셀 표현 (무대 조명과 같은 값)")]
        [SerializeField, Range(1f, 256f)] float pixelsPerUnit = 100f;
        [SerializeField, Range(2, 16)] int bandCount = 6;
        [SerializeField, Range(0f, 1f)] float ditherStrength = 0.18f;

        [SerializeField, Range(0f, 2f), Tooltip("픽셀 조명 패스의 강도 배율")]
        float pixelIntensityScale = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("실제 Light2D 가 담당할 밝기 비율")]
        float smoothLightContribution = 0.85f;

        Light2D _light;
        PixelSpotlight2D _pixelSpotlight;

        bool _active;
        float _stateElapsed;   // 켜지거나 꺼지기 시작한 뒤 경과 시간
        float _blinkTime;      // 깜빡임 위상. 피버마다 0에서 시작해 항상 밝게 들어온다
        float _ramp;           // 0(꺼짐) ~ 1(완전히 켜짐)

        public bool IsActive => _active;

        void Awake()
        {
            _light = GetComponent<Light2D>();
            EnsurePixelSpotlight();
            ApplyLight(0f);
        }

        void OnEnable()
        {
            EventBus.Subscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);

            EnsurePixelSpotlight();
            if (autoPlaceAtCameraTop) PlaceAtCameraTop();

            // 늦게 켜져도 현재 피버 상태에 맞춘다
            _active = FeverSystem.HasInstance && FeverSystem.Instance.IsActive;
            _ramp = _active ? 1f : 0f;
            _blinkTime = 0f;
            ApplyLight(_active ? 1f : 0f);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<FeverStateChanged>(OnFeverStateChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);

            _active = false;
            _ramp = 0f;
            ApplyLight(0f);
        }

        void OnFeverStateChanged(FeverStateChanged e) => SetActive(e.IsActive);

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver)
                SetActive(false);
        }

        void SetActive(bool active)
        {
            if (_active == active) return;

            _active = active;
            _stateElapsed = 0f;
            if (active) _blinkTime = 0f; // 항상 밝게 시작한다
        }

        void EnsurePixelSpotlight()
        {
            if (_light == null) _light = GetComponent<Light2D>();
            if (_light == null || _light.lightType != Light2D.LightType.Point) return;

            if (_pixelSpotlight == null) _pixelSpotlight = GetComponent<PixelSpotlight2D>();
            if (_pixelSpotlight == null)
                _pixelSpotlight = gameObject.AddComponent<PixelSpotlight2D>();

            _pixelSpotlight.Configure(pixelsPerUnit, bandCount, ditherStrength);
        }

        /// <summary>
        /// 카메라 화면 상단 중앙으로 옮기고 아래를 향하게 한다.
        /// PixelSpotlight2D 는 <c>transform.up</c> 을 조사 방향으로 쓰므로 z 를 180도 돌린다.
        /// </summary>
        void PlaceAtCameraTop()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            float halfHeight = camera.orthographic
                ? camera.orthographicSize
                : Mathf.Abs(camera.transform.position.z) *
                  Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            Vector3 center = camera.transform.position;
            transform.position = new Vector3(
                center.x,
                center.y + halfHeight + aboveScreenMargin,
                0f);
            transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        }

        void Update()
        {
            _stateElapsed += Time.deltaTime;

            // 켜질 때·꺼질 때 밝기가 툭 튀지 않도록 짧게 붙였다 뗀다
            float target = _active ? 1f : 0f;
            _ramp = rampDuration <= 0f
                ? target
                : Mathf.MoveTowards(_ramp, target, Time.deltaTime / rampDuration);

            if (_ramp <= 0.0001f && !_active)
            {
                ApplyLight(0f);
                return;
            }

            _blinkTime += Time.deltaTime;
            ApplyLight(_ramp);
        }

        void ApplyLight(float ramp)
        {
            if (_light == null) return;

            float blend = ramp <= 0f ? 0f : EvaluateBlink();
            float intensity = Mathf.Lerp(minIntensity, peakIntensity, blend) * ramp;

            _light.color = color;
            _light.intensity = intensity * smoothLightContribution;

            // Unity 의 == 오버로드로 판정한다. ?. 는 파괴된 오브젝트의 fake-null 을 통과시킨다
            if (_pixelSpotlight != null)
            {
                // 강도가 0이면 PixelSpotlight2D 가 렌더러를 스스로 끈다
                _pixelSpotlight.SetVisualLight(color, intensity * pixelIntensityScale);
            }
        }

        /// <summary>0~1. 딱딱한 깜빡임이면 0 아니면 1로만 나온다.</summary>
        float EvaluateBlink()
        {
            float phase = (_blinkTime * blinksPerSecond) % 1f;
            if (!hardBlink) return 0.5f + 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
            return phase < dutyCycle ? 1f : 0f;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Toggle Fever Spotlight")]
        void DebugToggle()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Fever] Play Mode 에서 실행하세요.", this);
                return;
            }
            SetActive(!_active);
        }

        [ContextMenu("Debug/Place At Camera Top")]
        void DebugPlace() => PlaceAtCameraTop();

        void OnValidate()
        {
            if (minIntensity > peakIntensity) minIntensity = peakIntensity;
        }
#endif
    }
}
