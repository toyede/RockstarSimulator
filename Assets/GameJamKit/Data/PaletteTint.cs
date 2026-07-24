using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// SpriteRenderer / UI Graphic 에 팔레트 색을 적용한다.
    /// 에디터에서도 즉시 반영되므로 팀원이 색을 직접 고를 일이 없다.
    /// 색을 바꾸고 싶으면 ColorPalette 에셋의 hex 만 수정하면 전부 따라온다.
    /// </summary>
    [ExecuteAlways]
    public class PaletteTint : MonoBehaviour
    {
        [SerializeField] string colorKey = "primary";
        [SerializeField, Range(0f, 1f)] float alphaMultiplier = 1f;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void SetKey(string key)
        {
            colorKey = key;
            Apply();
        }

        public void Apply()
        {
            var asset = Palette.Asset;
            if (asset == null || !asset.TryGet(colorKey, out var color)) return;

            color.a *= alphaMultiplier;

            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.color = color;

            var graphic = GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null) graphic.color = color;
        }
    }
}
