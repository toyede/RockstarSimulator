using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamKit
{
    /// <summary>
    /// 화면 페이드 인/아웃. 씬에 아무것도 배치하지 않아도 런타임에 캔버스를 스스로 만든다.
    ///
    /// ScreenFader.Instance.FadeOut(0.4f, () => ...);  // 화면 어두워짐
    /// ScreenFader.Instance.FadeIn(0.4f);              // 화면 밝아짐
    /// ScreenFader.Instance.Transition(0.3f, () => 씬전환처리());
    /// </summary>
    public class ScreenFader : MonoSingleton<ScreenFader>
    {
        [SerializeField] Color fadeColor = Color.black;
        [SerializeField, Tooltip("캔버스 정렬 순서. 모든 UI 위에 오도록 크게 잡는다")] int sortingOrder = 30000;

        Image _image;
        CanvasGroup _group;
        Coroutine _routine;

        public bool IsFading => _routine != null;
        public float Alpha => _group != null ? _group.alpha : 0f;

        protected override void OnAwake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            gameObject.AddComponent<GraphicRaycaster>();

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var imageGo = new GameObject("Fade", typeof(RectTransform));
            imageGo.transform.SetParent(transform, false);

            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _image = imageGo.AddComponent<Image>();
            _image.color = fadeColor;
            _image.raycastTarget = true;
        }

        public void SetColor(Color color)
        {
            fadeColor = color;
            if (_image != null) _image.color = color;
        }

        /// <summary>화면을 어둡게 (알파 0 → 1).</summary>
        public void FadeOut(float duration = 0.3f, Action onComplete = null) => FadeTo(1f, duration, onComplete);

        /// <summary>화면을 밝게 (알파 1 → 0).</summary>
        public void FadeIn(float duration = 0.3f, Action onComplete = null) => FadeTo(0f, duration, onComplete);

        public void FadeTo(float targetAlpha, float duration, Action onComplete = null)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(FadeRoutine(Mathf.Clamp01(targetAlpha), duration, onComplete));
        }

        /// <summary>페이드 아웃 → onBlack 실행 → 페이드 인. 씬 전환/리스폰 연출에 사용.</summary>
        public void Transition(float duration, Action onBlack, float holdSeconds = 0f)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(TransitionRoutine(duration, onBlack, holdSeconds));
        }

        /// <summary>애니메이션 없이 즉시 설정.</summary>
        public void SetAlphaImmediate(float alpha)
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            ApplyAlpha(Mathf.Clamp01(alpha));
        }

        void ApplyAlpha(float alpha)
        {
            _group.alpha = alpha;
            _group.blocksRaycasts = alpha > 0.001f; // 완전히 투명할 땐 클릭을 막지 않는다
        }

        IEnumerator FadeRoutine(float target, float duration, Action onComplete)
        {
            float start = _group.alpha;
            float t = 0f;

            _group.blocksRaycasts = true; // 페이드 중엔 입력 차단

            while (t < duration && duration > 0f)
            {
                t += Time.unscaledDeltaTime; // 일시정지 중에도 동작해야 한다
                ApplyAlpha(Mathf.Lerp(start, target, Mathf.Clamp01(t / duration)));
                yield return null;
            }

            ApplyAlpha(target);
            _routine = null;
            onComplete?.Invoke();
        }

        IEnumerator TransitionRoutine(float duration, Action onBlack, float hold)
        {
            yield return FadeRoutine(1f, duration, null);
            onBlack?.Invoke();
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            yield return FadeRoutine(0f, duration, null);
        }
    }
}
