using System.Collections;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 트라우마 기반 카메라 흔들림. 외부 패키지(Cinemachine) 의존성 없음.
    /// 카메라를 직접 움직이는 팔로우 스크립트와도 충돌하지 않도록,
    /// 매 LateUpdate 마다 이전 프레임의 오프셋을 되돌린 뒤 새 오프셋을 더한다.
    ///
    /// CameraShake.Shake(0.3f);            // 한 방
    /// CameraShake.ShakeFor(0.4f, 0.6f);   // 0.6초 동안 지속
    /// </summary>
    public class CameraShake : MonoSingleton<CameraShake>
    {
        protected override bool Persistent => false; // 카메라는 씬마다 다르다

        [SerializeField, Tooltip("비우면 Camera.main 을 사용")] Transform target;
        [SerializeField] float maxOffset = 0.5f;
        [SerializeField] float maxAngle = 5f;
        [SerializeField] float frequency = 22f;
        [SerializeField, Tooltip("초당 트라우마 감쇠량")] float decay = 1.8f;

        float _trauma;
        float _seed;
        Vector3 _appliedPos;
        float _appliedAngle;

        Transform Target
        {
            get
            {
                if (target == null && Camera.main != null) target = Camera.main.transform;
                return target;
            }
        }

        protected override void OnAwake() => _seed = Random.value * 100f;

        // ---------------- 정적 API ----------------

        /// <summary>amount: 0.2 약함 / 0.4 보통 / 0.7 강함 (0~1)</summary>
        public static void Shake(float amount = 0.35f)
        {
            if (!Application.isPlaying) return;
            var inst = Instance;
            if (inst != null) inst.AddTrauma(amount);
        }

        public static void ShakeFor(float amount, float duration)
        {
            if (!Application.isPlaying) return;
            var inst = Instance;
            if (inst != null) inst.StartCoroutine(inst.SustainRoutine(amount, duration));
        }

        public static void StopShake()
        {
            if (HasInstance) Instance._trauma = 0f;
        }

        // ---------------- 구현 ----------------

        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + Mathf.Abs(amount));

        public void SetTarget(Transform t) => target = t;

        IEnumerator SustainRoutine(float amount, float duration)
        {
            float end = Time.unscaledTime + duration;
            while (Time.unscaledTime < end)
            {
                _trauma = Mathf.Max(_trauma, Mathf.Clamp01(amount));
                yield return null;
            }
        }

        void LateUpdate()
        {
            var t = Target;
            if (t == null) return;

            // 이전 프레임 오프셋 제거 (팔로우 스크립트가 이동시킨 위치를 보존)
            t.localPosition -= _appliedPos;
            if (!Mathf.Approximately(_appliedAngle, 0f))
                t.localRotation = Quaternion.AngleAxis(-_appliedAngle, Vector3.forward) * t.localRotation;

            _appliedPos = Vector3.zero;
            _appliedAngle = 0f;

            if (_trauma <= 0f) return;

            float shake = _trauma * _trauma;             // 제곱 감쇠 = 자연스러운 느낌
            float time = Time.unscaledTime * frequency;

            _appliedPos = new Vector3(
                (Mathf.PerlinNoise(_seed, time) * 2f - 1f) * maxOffset * shake,
                (Mathf.PerlinNoise(_seed + 1.7f, time) * 2f - 1f) * maxOffset * shake,
                0f);
            _appliedAngle = (Mathf.PerlinNoise(_seed + 3.4f, time) * 2f - 1f) * maxAngle * shake;

            t.localPosition += _appliedPos;
            t.localRotation = Quaternion.AngleAxis(_appliedAngle, Vector3.forward) * t.localRotation;

            _trauma = Mathf.Max(0f, _trauma - decay * Time.unscaledDeltaTime);
        }
    }
}
