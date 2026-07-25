using System;
using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 떠오르며 사라지는 월드 텍스트 한 장의 움직임 값.
    /// 표시하는 쪽(관객 입퇴장·저격 보상 등)이 인스펙터로 각자 조절한다.
    /// </summary>
    [Serializable]
    public struct FloatingWorldTextStyle
    {
        [Tooltip("나타나서 사라질 때까지의 시간(초)")]
        [Min(0.05f)] public float duration;

        [Tooltip("떠오르는 높이(월드 유닛)")]
        [Min(0f)] public float riseDistance;

        [Tooltip("등장 시작 크기 배율")]
        [Min(0.01f)] public float startScale;

        [Tooltip("등장 직후 최대 크기 배율. 1보다 크면 톡 튀어나온다")]
        [Min(0.01f)] public float peakScale;

        [Tooltip("이 진행도부터 서서히 투명해진다")]
        [Range(0f, 1f)] public float fadeStart;

        [Tooltip("글자 크기(TMP fontSize)")]
        [Min(0.1f)] public float fontSize;

        public FontStyles fontStyle;

        public static FloatingWorldTextStyle Default => new FloatingWorldTextStyle
        {
            duration = 1.1f,
            riseDistance = 0.9f,
            startScale = 0.6f,
            peakScale = 1.25f,
            fadeStart = 0.6f,
            fontSize = 3f,
            fontStyle = FontStyles.Normal,
        };

        /// <summary>
        /// 0으로 두면 나눗셈이 깨지거나 아무것도 안 보이는 값만 기본값으로 되돌린다.
        /// riseDistance 0(제자리) · fadeStart 0(처음부터 페이드)은 의도한 설정일 수 있으므로 그대로 둔다.
        /// </summary>
        public FloatingWorldTextStyle Sanitized()
        {
            FloatingWorldTextStyle fallback = Default;
            return new FloatingWorldTextStyle
            {
                duration = duration >= 0.05f ? duration : fallback.duration,
                riseDistance = Mathf.Max(0f, riseDistance),
                startScale = startScale >= 0.01f ? startScale : fallback.startScale,
                peakScale = peakScale >= 0.01f ? peakScale : fallback.peakScale,
                fadeStart = Mathf.Clamp01(fadeStart),
                fontSize = fontSize >= 0.1f ? fontSize : fallback.fontSize,
                fontStyle = fontStyle,
            };
        }
    }

    /// <summary>
    /// 월드 공간에 잠깐 떠올랐다 사라지는 텍스트 한 장.
    ///
    /// <see cref="AudienceReactionPopup"/> 과 같은 연출(상승 + 스케일 펀치 + 페이드)이지만
    /// 관객 개체에 묶여 있지 않아 어디에나 띄울 수 있다.
    /// 인스턴스는 보통 <see cref="FloatingWorldTextPool"/> 이 런타임에 만들어 재사용한다 —
    /// 프리팹 에셋이 필요 없으므로 셋업 메뉴가 텍스트 프리팹을 관리할 필요가 없다.
    ///
    /// 코루틴을 쓰지 않고 Update 에서 경과 시간만으로 매 프레임 값을 다시 만든다.
    /// 재생 중에 다시 Play 가 들어와도 타이머만 리셋되므로 상태가 꼬이지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingWorldText : MonoBehaviour
    {
        TextMeshPro _text;
        FloatingWorldTextStyle _style = FloatingWorldTextStyle.Default;
        Vector3 _basePosition;
        Color _color = Color.white;
        float _elapsed;
        bool _playing;

        public bool IsPlaying => _playing;

        /// <summary>재생을 시작한 시각. 풀이 "가장 오래된 것"을 재활용할 때 쓴다.</summary>
        public float StartedAt { get; private set; }

        void Awake() => EnsureText();

        void EnsureText()
        {
            if (_text != null) return;

            _text = GetComponent<TextMeshPro>();
            if (_text == null) _text = gameObject.AddComponent<TextMeshPro>();

            _text.alignment = TextAlignmentOptions.Center;
            _text.enableWordWrapping = false;
            _text.raycastTarget = false;
            _text.enabled = false;
        }

        /// <summary>글꼴·정렬 레이어·움직임 값을 한 번에 설정한다. 풀이 생성 직후 호출한다.</summary>
        public void Initialize(
            TMP_FontAsset font,
            string sortingLayer,
            FloatingWorldTextStyle style)
        {
            EnsureText();

            _style = style.Sanitized();
            if (font != null) _text.font = font;
            _text.fontSize = _style.fontSize;
            _text.fontStyle = _style.fontStyle;
            if (!string.IsNullOrEmpty(sortingLayer))
                _text.renderer.sortingLayerName = sortingLayer;

            Stop();
        }

        /// <summary>부모 기준 로컬 위치에서 텍스트를 띄운다.</summary>
        public void Play(string text, Color color, Vector3 localPosition, int sortingOrder)
        {
            EnsureText();
            if (string.IsNullOrEmpty(text)) { Stop(); return; }

            _basePosition = localPosition;
            _color = color;
            _elapsed = 0f;
            _playing = true;
            StartedAt = Time.unscaledTime;

            _text.text = text;
            _text.color = color;
            _text.renderer.sortingOrder = sortingOrder;
            _text.enabled = true;

            transform.localPosition = _basePosition;
            transform.localScale = Vector3.one * _style.startScale;
        }

        public void Stop()
        {
            _playing = false;
            _elapsed = 0f;
            if (_text == null) return;

            _text.text = string.Empty;
            _text.enabled = false;
        }

        void Update()
        {
            if (!_playing) return;

            // 게임오버 팝업 등으로 timeScale 이 0 이 돼도 연출은 끝까지 재생돼야 한다
            _elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_elapsed / _style.duration);

            float easedRise = 1f - (1f - progress) * (1f - progress);
            transform.localPosition = _basePosition + Vector3.up * (_style.riseDistance * easedRise);

            float scale = progress < 0.25f
                ? Mathf.Lerp(_style.startScale, _style.peakScale, progress / 0.25f)
                : Mathf.Lerp(_style.peakScale, 1f, (progress - 0.25f) / 0.75f);
            transform.localScale = Vector3.one * scale;

            Color color = _color;
            color.a = progress <= _style.fadeStart
                ? 1f
                : 1f - Mathf.InverseLerp(_style.fadeStart, 1f, progress);
            _text.color = color;

            if (progress >= 1f) Stop();
        }
    }
}
