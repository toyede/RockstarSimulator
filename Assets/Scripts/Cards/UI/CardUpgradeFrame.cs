using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>원본 픽셀 비율로 카드 그림 바깥에 겹치는 정적 강화 프레임.</summary>
    public sealed class CardUpgradeFrame : Image
    {
        Image _artwork;
        Vector2 _lastSize;
        Sprite _lastArtwork;

        public static CardUpgradeFrame Create(Image artwork)
        {
            if (artwork == null) return null;
            var go = new GameObject("UpgradeFrame", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(artwork.transform, false);
            var frame = go.AddComponent<CardUpgradeFrame>();
            frame.raycastTarget = false;
            frame.enabled = false;
            return frame;
        }

        public void Bind(Image artwork, Sprite frame, bool visible)
        {
            _artwork = artwork;
            sprite = frame;
            color = Color.white;
            raycastTarget = false;
            enabled = visible && artwork != null && artwork.sprite != null && frame != null;
            if (enabled) FitToArtwork();
        }

        void LateUpdate()
        {
            if (_artwork != null && (_lastSize != _artwork.rectTransform.rect.size || _lastArtwork != _artwork.sprite))
                FitToArtwork();
        }

        void FitToArtwork()
        {
            if (_artwork == null || _artwork.sprite == null || sprite == null) return;
            _lastSize = _artwork.rectTransform.rect.size;
            _lastArtwork = _artwork.sprite;
            Vector2 cardPixels = _lastArtwork.rect.size;
            float scale = Mathf.Min(_lastSize.x / cardPixels.x, _lastSize.y / cardPixels.y);
            rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sprite.rect.size * scale;
            // 스프라이트가 투명 여백을 잘라 임포트됐더라도 원본 캔버스 중심에 맞춘다.
            Vector2 canvasCenter = new Vector2(sprite.texture.width, sprite.texture.height) * 0.5f;
            rectTransform.anchoredPosition = (sprite.rect.center - canvasCenter) * scale;
        }
    }
}
