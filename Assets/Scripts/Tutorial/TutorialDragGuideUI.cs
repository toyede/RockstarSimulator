using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>시각 안내만 담당한다. 실제 카드/포인터/카드 사용 이벤트를 조작하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialDragGuideUI : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float pointSeconds = 0.3f;
        [SerializeField, Min(0.1f)] float pressSeconds = 0.2f;
        [SerializeField, Min(0.1f)] float dragSeconds = 1.1f;
        [SerializeField, Min(0.1f)] float releaseSeconds = 0.4f;
        [SerializeField, Min(0.1f)] float restSeconds = 0.7f;

        RectTransform _root, _hand, _target;
        Image _ghost, _press;
        Text _hint;
        Canvas _canvas;
        CardDragHandler _source;
        AudienceMemberActor _inspectionActor;
        AudienceId _inspectionId;
        Camera _inspectionCamera;
        float _inspectionSeconds;
        Coroutine _binding;
        float _clock;
        bool _showing, _wasDragging, _inside;
        readonly Vector3[] _corners = new Vector3[4];

        public void Show(TutorialOverlayUI overlay)
        {
            Hide();
            if (_root == null) Build(overlay);
            if (_root == null) return;
            _showing = true;
            _clock = 0f;
            _wasDragging = false;
            _hint.text = "여기로 전달";
            _press.rectTransform.localScale = Vector3.one;
            _root.gameObject.SetActive(true);
            _binding = StartCoroutine(BindCard());
        }

        public void Hide()
        {
            _showing = false;
            if (_binding != null) StopCoroutine(_binding);
            _binding = null;
            _source = null;
            _inspectionActor = null;
            _inspectionId = default;
            _inspectionCamera = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void OnDisable() => Hide();

        /// <summary>일반 관객에게 이동한 뒤 머무르는 시각 안내. 실제 입력은 생성하지 않는다.</summary>
        public void ShowInspection(TutorialOverlayUI overlay, AudienceMemberActor actor, float seconds)
        {
            Hide();
            if (_root == null) Build(overlay);
            if (_root == null || actor == null) return;
            _inspectionActor = actor;
            _inspectionId = actor.BoundId;
            _inspectionCamera = Camera.main;
            _inspectionSeconds = Mathf.Max(.5f, seconds);
            _clock = 0f;
            _showing = true;
            _hint.text = $"여기서 {_inspectionSeconds:0.#}초 확인";
            _root.gameObject.SetActive(true);
        }
        void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }

        IEnumerator BindCard()
        {
            // SetHandがUIへ反映されるのを待つ。シーン検索は開始時の最大3回だけ。
            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return null;
                foreach (var handler in FindObjectsByType<CardDragHandler>(FindObjectsSortMode.None))
                {
                    if (handler.HandIndex < 0 || handler.Card == null ||
                        handler.Card.Role != CardRole.Special ||
                        handler.Card.TargetPreference != CrowdPreference.Singalong) continue;
                    _source = handler;
                    _ghost.sprite = handler.Card.Artwork;
                    _ghost.preserveAspect = true;
                    _binding = null;
                    yield break;
                }
            }
            _binding = null;
        }

        void Build(TutorialOverlayUI overlay)
        {
            var parentCanvas = overlay.GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;
            _canvas = parentCanvas.rootCanvas;
            _root = new GameObject("TutorialDragGuide (visual only)", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            _root.SetParent(_canvas.transform, false);
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            var group = _root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false; group.interactable = false;
            _ghost = MakeImage("GhostCard", _root, new Vector2(90f, 140f), new Color(1f, 1f, 1f, 0.5f));
            _press = MakeImage("PressPulse", _root, new Vector2(22f, 22f), new Color(1f, 0.85f, 0.3f, 0.7f));
            _hand = new GameObject("PixelHand", typeof(RectTransform)).GetComponent<RectTransform>();
            _hand.SetParent(_root, false);
            // 작은 UI 사각형으로 그린 손: 별도 텍스처/폰트 글리프 불필요.
            string[] pixels = { "..##....", "..#W#...", "..#W#...", "..#W###.", "###WWWW#", "#WWWWWW#", ".#WWWWW#", "..#WWW#.", "..#####." };
            for (int y = 0; y < pixels.Length; y++)
                for (int x = 0; x < pixels[y].Length; x++)
                    if (pixels[y][x] != '.')
                    {
                        var dot = MakeImage("Pixel", _hand, Vector2.one * 4f, pixels[y][x] == 'W' ? Color.white : new Color32(46, 34, 47, 255));
                        dot.rectTransform.anchoredPosition = new Vector2((x - 3) * 4f, -y * 4f);
                    }
            _target = new GameObject("TargetCorners", typeof(RectTransform)).GetComponent<RectTransform>();
            _target.SetParent(_root, false);
            foreach (var sign in new[] { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(1, 1) })
            {
                var corner = MakeImage("Corner", _target, new Vector2(9f, 9f), new Color32(249, 194, 43, 230));
                corner.rectTransform.anchoredPosition = sign * 34f;
            }
            _hint = new GameObject("DropHint", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            _hint.transform.SetParent(_target, false);
            _hint.font = overlay.GuideFont;
            _hint.fontSize = 23; _hint.alignment = TextAnchor.MiddleCenter;
            _hint.color = new Color32(249, 194, 43, 255);
            _hint.raycastTarget = false;
            _hint.rectTransform.sizeDelta = new Vector2(230f, 36f);
            _hint.rectTransform.anchoredPosition = new Vector2(0f, 65f);
            _hint.text = "여기로 전달";
        }

        static Image MakeImage(string name, Transform parent, Vector2 size, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.rectTransform.sizeDelta = size;
            image.color = color; image.raycastTarget = false;
            return image;
        }

        Vector2 Local(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen,
                _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera, out var point);
            return point;
        }

        void LateUpdate()
        {
            if (!_showing) return;
            if (_inspectionActor != null)
            {
                UpdateInspection();
                return;
            }
            var target = SpecialAudience.CurrentDropTarget;
            bool valid = _source != null && target != null && target.IsActive && target.HitCollider != null && target.WorldCamera != null;
            _ghost.enabled = valid;
            _hand.gameObject.SetActive(valid);
            _target.gameObject.SetActive(valid);
            _press.enabled = false;
            if (!valid) return;
            Vector2 end = Local(target.WorldCamera.WorldToScreenPoint(target.HitCollider.bounds.center));
            _target.anchoredPosition = end;
            _target.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.04f);
            var dragging = CardDragHandler.Current;
            bool isDragging = dragging != null;
            bool inside = isDragging && target.ContainsScreenCircle(dragging.PointerScreenPosition, dragging.DropRadiusPixels);
            if (_inside != inside) _hint.text = inside ? "여기서 놓으세요!" : "여기로 전달";
            _inside = inside;
            if (isDragging)
            {
                _ghost.enabled = false;
                _hand.gameObject.SetActive(false);
                _wasDragging = true;
                return;
            }
            if (_wasDragging) { _clock = -restSeconds; _wasDragging = false; }
            _clock += Time.unscaledDeltaTime;
            float cycle = pointSeconds + pressSeconds + dragSeconds + releaseSeconds + restSeconds;
            float t = _clock % cycle;
            bool visible = t >= 0f && t < cycle - restSeconds;
            _hand.gameObject.SetActive(visible);
            _ghost.enabled = visible && t >= pointSeconds;
            var rect = (RectTransform)_source.transform;
            var sourceCanvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = sourceCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : sourceCanvas.worldCamera;
            Vector2 start = Local(RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)));
            rect.GetWorldCorners(_corners);
            Vector2 size = Local(RectTransformUtility.WorldToScreenPoint(camera, _corners[2])) - Local(RectTransformUtility.WorldToScreenPoint(camera, _corners[0]));
            _ghost.rectTransform.sizeDelta = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.8f;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - pointSeconds - pressSeconds) / dragSeconds));
            Vector2 position = Vector2.Lerp(start, end, progress);
            _ghost.rectTransform.anchoredPosition = position;
            _hand.anchoredPosition = position;
            _hand.localScale = Vector3.one * (t >= pointSeconds && t < pointSeconds + pressSeconds ? 0.8f : 1f);
            float fade = 1f - Mathf.Clamp01((t - pointSeconds - pressSeconds - dragSeconds) / releaseSeconds);
            _ghost.color = new Color(1f, 1f, 1f, 0.5f * fade);
            _press.enabled = visible && t >= pointSeconds && t < pointSeconds + pressSeconds;
            _press.rectTransform.anchoredPosition = start;
        }

        void UpdateInspection()
        {
            if (!_inspectionActor.IsBound || _inspectionActor.BoundId != _inspectionId)
            {
                Hide();
                return;
            }
            if (_inspectionCamera == null) { Hide(); return; }
            Vector2 end = Local(_inspectionCamera.WorldToScreenPoint(_inspectionActor.PreferenceHoverCenter));
            _target.anchoredPosition = end;
            _target.gameObject.SetActive(true);
            _target.localScale = Vector3.one;
            _ghost.enabled = false;
            _clock += Time.unscaledDeltaTime;
            float travel = .9f;
            float t = _clock % (travel + _inspectionSeconds + restSeconds);
            bool visible = t < travel + _inspectionSeconds;
            _hand.gameObject.SetActive(visible);
            _hand.localScale = Vector3.one;
            _hand.anchoredPosition = Vector2.Lerp(end + new Vector2(100f, -80f), end,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / travel)));
            _press.enabled = visible && t >= travel;
            _press.rectTransform.anchoredPosition = end;
            _press.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 4f) * .15f);
        }
    }
}
