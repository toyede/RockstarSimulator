using UnityEngine;
using UnityEngine.UI;
using GameJamKit;

namespace ContextStage
{
    /// <summary>카드 아트의 알파를 그대로 쓰는 봉인 음영 + 폰트에 의존하지 않는 X.</summary>
    public sealed class CardSealVisual : MonoBehaviour
    {
        Image _shade;
        RectTransform _cross;
        readonly Image[] _strokes = new Image[2];
        bool _sealed;
        float _age = 1f, _fade;
        public bool IsSealed => _sealed;

        public void SetSealed(bool active, Image artwork)
        {
            if (active && _shade == null) Build(artwork);
            if (_shade != null) _shade.sprite = artwork != null ? artwork.sprite : null;
            if (active && !_sealed) Pulse();
            _sealed = active;
            if (!gameObject.activeInHierarchy) { _fade = 0f; Apply(0f); }
        }
        public void Pulse() { _age = 0f; }
        void Build(Image artwork)
        {
            var go = new GameObject("SealShade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            _shade = go.GetComponent<Image>(); _shade.raycastTarget = false; _shade.preserveAspect = true;
            var cross = new GameObject("SealCross", typeof(RectTransform));
            cross.transform.SetParent(go.transform, false);
            _cross = (RectTransform)cross.transform;
            _cross.anchorMin = _cross.anchorMax = _cross.pivot = new Vector2(.5f,.5f);
            _cross.sizeDelta = Vector2.one * 100f;
            for (int i = 0; i < 2; i++)
            {
                var line = new GameObject("Stroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                line.transform.SetParent(cross.transform, false);
                var rect = (RectTransform)line.transform;
                rect.sizeDelta = new Vector2(118f, 8f);
                rect.localRotation = Quaternion.Euler(0,0,i == 0 ? 48 : -48);
                var image = line.GetComponent<Image>();
                _strokes[i] = image;
                image.color = new Color32(255, 93, 103, 255); image.raycastTarget = false;
            }
            Apply(0f);
        }
        void Update()
        {
            if (_shade == null || (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused)) return;
            _age += Time.deltaTime;
            _fade = Mathf.MoveTowards(_fade, _sealed ? 1f : 0f, Time.deltaTime * 7f);
            Apply(_fade);
        }
        void Apply(float visibility)
        {
            if (_shade == null) return;
            _shade.enabled = visibility > 0f;
            _cross.gameObject.SetActive(visibility > 0f);
            float flash = Mathf.Clamp01(1f - _age / .18f);
            _shade.color = new Color(0,0,0,visibility * Mathf.Lerp(.28f, .8f, flash));
            _cross.localScale = Vector3.one * (1f + flash * .12f);
            _cross.anchoredPosition = new Vector2(Mathf.Sin(_age * 80f) * flash * 3f,0);
            foreach (var image in _strokes)
            { var color = image.color; color.a = visibility; image.color = color; }
        }
        void OnDisable() { _sealed = false; _fade = 0f; Apply(0f); }
    }
}
