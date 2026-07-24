using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 카드가 사라질 때의 픽셀 디졸브 연출. <b>카드 로직을 전혀 모른다.</b>
    ///
    /// - 카드를 살짝 키운 뒤(0.05초) 픽셀 블록 단위로 지워지고, 경계에 지정한 색이 남는다
    /// - 텍스트는 후반 구간에만 페이드해서 끝까지 읽히게 한다
    /// - 끝나면 콜백만 부른다. <b>오브젝트를 파괴하거나 비활성화하지 않고</b>,
    ///   부모·위치도 건드리지 않는다. 실제 제거·반환·드로우는 카드 담당 시스템의 몫이다.
    ///
    /// UI Image 는 MaterialPropertyBlock 을 쓸 수 없어 카드별 Material 인스턴스를 만든다.
    /// (손패 최대 4장이라 부담이 없다) 만든 인스턴스는 Reset·OnDestroy 에서 정리하고,
    /// 원본 Material 은 절대 건드리지 않으므로 다른 카드에 값이 번지지 않는다.
    ///
    /// [카드 담당이 나중에 호출할 API]
    ///   effect.SetEdgeColor(color);
    ///   effect.PlayDissolve(() => { /* 여기서 카드 반환·드로우 */ });
    ///   effect.ResetEffect();   // 카드를 재사용(풀링)할 때
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardDissolveEffect : MonoBehaviour
    {
        const string ShaderName = "ContextStage/UI/Pixel Dissolve";

        [Header("디졸브 대상")]
        [SerializeField, Tooltip("셰이더를 적용할 Image 들. 비워두면 자식에서 자동으로 모은다")]
        Image[] dissolveTargets;

        [SerializeField, Tooltip("비워두면 셰이더를 이름으로 찾아 Material 을 만든다")]
        Material sourceMaterial;

        [Header("텍스트 / 아이콘 페이드")]
        [SerializeField, Tooltip("있으면 이걸로 페이드한다. 없으면 아래 목록을 쓴다")]
        CanvasGroup textCanvasGroup;

        [SerializeField, Tooltip("CanvasGroup 이 없을 때 알파를 내릴 대상 (Text 등)")]
        Graphic[] fadeGraphics;

        [SerializeField, Range(0f, 1f), Tooltip("디졸브 진행률이 이 값을 넘은 뒤부터 텍스트가 사라지기 시작한다")]
        float textFadeStart = 0.6f;

        [Header("연출 수치")]
        [SerializeField, Tooltip("살짝 커지는 시간(초)")]
        float scalePunchDuration = 0.05f;

        [SerializeField, Tooltip("커지는 배율")]
        float scaleMultiplier = 1.05f;

        [SerializeField, Tooltip("레이아웃을 흔들지 않도록 확대할 대상. 비워두면 이 오브젝트")]
        Transform scaleTarget;

        [SerializeField, Tooltip("디졸브 진행 시간(초)")]
        float dissolveDuration = 0.35f;

        [SerializeField, Range(0f, 1f)] float startDissolve = 0f;
        [SerializeField, Range(0f, 1f)] float endDissolve = 1f;

        [Header("셰이더 값")]
        [SerializeField, Range(0f, 0.5f), Tooltip("경계 폭")]
        float edgeWidth = 0.08f;

        [SerializeField, Tooltip("경계 색. SetEdgeColor 로 런타임에 바꾼다")]
        Color edgeColor = new Color32(0xF0, 0x1F, 0x1F, 0xFF);

        [SerializeField, Range(4f, 128f), Tooltip("가로·세로 픽셀 블록 수. 작을수록 큰 픽셀")]
        float pixelBlockCount = 24f;

        [SerializeField, Range(0.1f, 8f), Tooltip("노이즈 패턴 크기")]
        float noiseScale = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("1에 가까울수록 아래에서 위로 사라진다")]
        float direction = 0.5f;

        [SerializeField, Tooltip("카드마다 다른 모양으로 사라지도록 시작 시 시드를 랜덤화")]
        bool randomizeSeed = true;

        // ---------------- 셰이더 프로퍼티 ID (문자열 조회 비용 제거) ----------------
        static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
        static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        static readonly int DirectionId = Shader.PropertyToID("_Direction");
        static readonly int NoiseOffsetId = Shader.PropertyToID("_NoiseOffset");

        Image[] _targets;
        Material[] _instances;      // 이 카드 전용 Material. 다른 카드와 공유하지 않는다
        Material[] _originalMaterials;
        Vector3 _originalScale;
        float[] _originalAlphas;
        float _originalGroupAlpha = 1f;

        Coroutine _routine;
        Action _onCompleted;
        Vector4 _seed;
        bool _initialized;

        public bool IsPlaying => _routine != null;

        /// <summary>현재 디졸브 진행도(0~1). 카드마다 독립적이다.</summary>
        public float DissolveAmount { get; private set; }

        void Awake() => Initialize();

        void OnDestroy() => DestroyInstances();

        void OnDisable()
        {
            // 코루틴은 여기서 확실히 끊는다. 남은 콜백은 부르지 않는다 (중복 완료 방지)
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _onCompleted = null;
        }

        void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (scaleTarget == null) scaleTarget = transform;
            _originalScale = scaleTarget.localScale;

            _targets = dissolveTargets != null && dissolveTargets.Length > 0
                ? dissolveTargets
                : GetComponentsInChildren<Image>(true);

            _originalMaterials = new Material[_targets.Length];
            _instances = new Material[_targets.Length];
            for (int i = 0; i < _targets.Length; i++)
                if (_targets[i] != null) _originalMaterials[i] = _targets[i].material;

            if (textCanvasGroup != null) _originalGroupAlpha = textCanvasGroup.alpha;
            else if (fadeGraphics != null)
            {
                _originalAlphas = new float[fadeGraphics.Length];
                for (int i = 0; i < fadeGraphics.Length; i++)
                    if (fadeGraphics[i] != null) _originalAlphas[i] = fadeGraphics[i].color.a;
            }

            _seed = Vector4.zero;
        }

        // ---------------- 공개 API ----------------

        /// <summary>경계에 나타날 색을 지정한다. (카드 타입 색 등 — 이 컴포넌트는 의미를 모른다)</summary>
        public void SetEdgeColor(Color color)
        {
            edgeColor = color;
            if (_instances == null) return;
            for (int i = 0; i < _instances.Length; i++)
                if (_instances[i] != null) _instances[i].SetColor(EdgeColorId, edgeColor);
        }

        public void PlayDissolve() => PlayDissolve(null);

        /// <summary>디졸브를 재생한다. 이미 재생 중이면 무시한다 (중복 호출 방지).</summary>
        public void PlayDissolve(Action onCompleted)
        {
            Initialize();

            if (_routine != null)
            {
                Debug.LogWarning("[CardDissolve] 이미 재생 중이라 요청을 무시했습니다.", this);
                return;
            }

            if (!isActiveAndEnabled)
            {
                // 비활성 상태에서는 코루틴을 돌릴 수 없다. 호출부가 멈추지 않도록 즉시 완료 처리한다.
                ApplyProgress(1f);
                onCompleted?.Invoke();
                return;
            }

            _onCompleted = onCompleted;
            ApplyMaterials();
            _routine = StartCoroutine(DissolveRoutine());
        }

        /// <summary>Material·크기·알파를 모두 원래대로 되돌린다. (카드 재사용 시 호출)</summary>
        public void ResetEffect()
        {
            Initialize();

            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _onCompleted = null;

            RestoreMaterials();
            DissolveAmount = 0f;

            scaleTarget.localScale = _originalScale;

            if (textCanvasGroup != null) textCanvasGroup.alpha = _originalGroupAlpha;
            else if (fadeGraphics != null && _originalAlphas != null)
            {
                for (int i = 0; i < fadeGraphics.Length; i++)
                {
                    if (fadeGraphics[i] == null) continue;
                    var c = fadeGraphics[i].color;
                    c.a = _originalAlphas[i];
                    fadeGraphics[i].color = c;
                }
            }
        }

        // ---------------- 내부 ----------------

        IEnumerator DissolveRoutine()
        {
            // 1) 살짝 확대 — 레이아웃을 흔들지 않도록 scaleTarget 만 건드린다
            float t = 0f;
            while (t < scalePunchDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = scalePunchDuration <= 0f ? 1f : Mathf.Clamp01(t / scalePunchDuration);
                scaleTarget.localScale = Vector3.Lerp(_originalScale, _originalScale * scaleMultiplier, k);
                yield return null;
            }
            scaleTarget.localScale = _originalScale * scaleMultiplier;

            // 2) 디졸브
            t = 0f;
            while (t < dissolveDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = dissolveDuration <= 0f ? 1f : Mathf.Clamp01(t / dissolveDuration);
                ApplyProgress(Mathf.Lerp(startDissolve, endDissolve, k));
                yield return null;
            }
            ApplyProgress(endDissolve);

            // 3) 완료 — 오브젝트는 그대로 두고 알림만 보낸다
            _routine = null;
            var callback = _onCompleted;
            _onCompleted = null;
            callback?.Invoke();   // 한 요청당 정확히 한 번
        }

        void ApplyProgress(float amount)
        {
            DissolveAmount = amount;

            if (_instances != null)
            {
                for (int i = 0; i < _instances.Length; i++)
                    if (_instances[i] != null) _instances[i].SetFloat(DissolveAmountId, amount);
            }

            // 텍스트는 후반부에만 사라진다 (끝까지 읽히도록)
            float fade = textFadeStart >= 1f
                ? 1f
                : 1f - Mathf.Clamp01((amount - textFadeStart) / (1f - textFadeStart));

            if (textCanvasGroup != null) textCanvasGroup.alpha = _originalGroupAlpha * fade;
            else if (fadeGraphics != null && _originalAlphas != null)
            {
                for (int i = 0; i < fadeGraphics.Length; i++)
                {
                    if (fadeGraphics[i] == null) continue;
                    var c = fadeGraphics[i].color;
                    c.a = _originalAlphas[i] * fade;
                    fadeGraphics[i].color = c;
                }
            }
        }

        /// <summary>카드 전용 Material 인스턴스를 만들어 붙인다. 공용 Material 은 건드리지 않는다.</summary>
        void ApplyMaterials()
        {
            var shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[CardDissolve] 셰이더 '{ShaderName}' 를 찾지 못했습니다. " +
                                 "Assets/Shaders/PixelDissolveUI.shader 가 있는지 확인하세요.", this);
                return;
            }

            if (randomizeSeed) _seed = new Vector4(UnityEngine.Random.value * 100f, UnityEngine.Random.value * 100f, 0f, 0f);

            for (int i = 0; i < _targets.Length; i++)
            {
                var image = _targets[i];
                if (image == null) continue;

                if (_instances[i] == null)
                {
                    _instances[i] = sourceMaterial != null ? new Material(sourceMaterial) : new Material(shader);
                    _instances[i].hideFlags = HideFlags.HideAndDontSave;
                }

                var mat = _instances[i];
                mat.SetFloat(EdgeWidthId, edgeWidth);
                mat.SetColor(EdgeColorId, edgeColor);
                mat.SetFloat(PixelSizeId, pixelBlockCount);
                mat.SetFloat(NoiseScaleId, noiseScale);
                mat.SetFloat(DirectionId, direction);
                mat.SetVector(NoiseOffsetId, _seed);
                mat.SetFloat(DissolveAmountId, startDissolve);

                image.material = mat;
            }

            ApplyProgress(startDissolve);
        }

        void RestoreMaterials()
        {
            if (_targets == null) return;
            for (int i = 0; i < _targets.Length; i++)
                if (_targets[i] != null) _targets[i].material = _originalMaterials[i];
        }

        void DestroyInstances()
        {
            if (_instances == null) return;
            for (int i = 0; i < _instances.Length; i++)
            {
                if (_instances[i] == null) continue;
                if (Application.isPlaying) Destroy(_instances[i]);
                else DestroyImmediate(_instances[i]);
                _instances[i] = null;
            }
        }

        // ---------------- 테스트 ----------------

        [ContextMenu("Debug/Play Dissolve")]
        void DebugPlayDissolve()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[CardDissolve] Play Mode 에서만 동작합니다.", this);
                return;
            }
            PlayDissolve(() => Debug.Log("[CardDissolve] 완료 콜백", this));
        }

        [ContextMenu("Debug/Reset Dissolve")]
        void DebugResetDissolve() => ResetEffect();
    }
}
