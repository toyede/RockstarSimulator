using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>도트 스프라이트의 필터만 통일한다. 폰트/일반 텍스처 설정은 변경하지 않는다.</summary>
    public sealed class PointSpriteImportPolicy : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/", System.StringComparison.Ordinal)) return;
            var importer = assetImporter as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.Sprite)
                importer.filterMode = FilterMode.Point;
        }
    }
}
